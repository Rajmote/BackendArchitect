namespace BackendArchitect.Distributed.Messaging.Queues;

public sealed record CrashReport(string Mode, int Sent, int SideEffectsApplied, int Lost, int Redelivered);

public sealed record SlowHandlerReport(string Strategy, int Messages, int TimesProcessed, int DuplicateProcessing);

public sealed record FanOutReport(string Topology, int Published, int SeenByBilling, int SeenByShipping, int SeenByAnalytics);

public sealed record PoisonReport(string Strategy, int HandlerRuns, int DeadLettered);

public sealed record OrderingReport(string Topology, int Keys, int KeysOutOfOrder);

// Every queue behaviour, measured rather than asserted in prose.
public static class QueueScenarios
{
    // ── 1. receive-and-delete vs peek-lock, when the consumer crashes ─────────────────────────────
    //
    // Receive-and-delete is at-most-once: the broker deletes on handover, so a crash loses the message
    // outright. Peek-lock is at-least-once: the message is lent, the lock expires, and it comes back.
    public static CrashReport ConsumerCrashes(ReceiveMode mode, int messages, int crashEvery)
    {
        var queue = new MessageQueue(lockDurationTicks: 10, maxDeliveryCount: 10);
        for (var i = 0; i < messages; i++)
            queue.Send($"msg-{i}", $"body-{i}");

        var applied = new HashSet<string>();
        var redelivered = 0;

        while (true)
        {
            queue.Advance(11);                       // any in-flight lock has now expired
            var message = queue.Receive("consumer-1", mode);
            if (message is null)
                break;

            if (message.DeliveryCount > 1)
                redelivered++;

            var crashes = int.Parse(message.MessageId.Split('-')[1]) % crashEvery == 0;
            if (crashes && message.DeliveryCount == 1)
                continue;                            // crashed: no side effect, and never completed

            applied.Add(message.MessageId);
            queue.Complete(message.MessageId);
        }

        return new CrashReport(
            mode == ReceiveMode.PeekLock ? "peek-lock" : "receive-and-delete",
            messages,
            applied.Count,
            messages - applied.Count,
            redelivered);
    }

    // ── 2. a handler slower than the lock ─────────────────────────────────────────────────────────
    //
    // The first consumer is never told to stop. Its lock expires while it is still working, a second
    // consumer picks the same message up, and the side effect happens twice.
    public static SlowHandlerReport SlowHandler(bool renewLock, int messages, long lockTicks, long handlerTicks)
    {
        var queue = new MessageQueue(lockTicks, maxDeliveryCount: 10);
        for (var i = 0; i < messages; i++)
            queue.Send($"msg-{i}", $"body-{i}");

        var processedCount = new Dictionary<string, int>();

        while (queue.ActiveCount > 0)
        {
            var message = queue.Receive("consumer");
            if (message is null)
                break;

            // The handler works for handlerTicks. Renewal pushes the lock out as it goes.
            for (long elapsed = 0; elapsed < handlerTicks; elapsed++)
            {
                queue.Advance(1);
                if (renewLock)
                    queue.RenewLock(message.MessageId);
            }

            processedCount[message.MessageId] = processedCount.GetValueOrDefault(message.MessageId) + 1;
            queue.Complete(message.MessageId);       // fails silently if the lock was already lost
        }

        return new SlowHandlerReport(
            renewLock ? "lock renewed" : "no renewal",
            messages,
            processedCount.Values.Sum(),
            processedCount.Values.Sum() - processedCount.Count);
    }

    // ── 3. three services, one queue vs a topic ───────────────────────────────────────────────────
    //
    // Competing consumers is right for scaling ONE job and catastrophic for three DIFFERENT jobs: each
    // service silently handles a third of the messages and reports success.
    public static FanOutReport ThreeServicesShareOneQueue(int messages)
    {
        var queue = new MessageQueue(lockDurationTicks: 10, maxDeliveryCount: 10);
        for (var i = 0; i < messages; i++)
            queue.Send($"order-{i}", "placed");

        var seen = new Dictionary<string, int> { ["billing"] = 0, ["shipping"] = 0, ["analytics"] = 0 };
        string[] services = ["billing", "shipping", "analytics"];
        var turn = 0;

        while (true)
        {
            var service = services[turn++ % services.Length];
            var message = queue.Receive(service);
            if (message is null)
                break;

            seen[service]++;
            queue.Complete(message.MessageId);
        }

        return new FanOutReport("one shared queue", messages, seen["billing"], seen["shipping"], seen["analytics"]);
    }

    public static FanOutReport TopicWithOneSubscriptionEach(int messages)
    {
        // A subscription is a queue of its own: its own locks, delivery counts and dead-letter queue.
        var subscriptions = new Dictionary<string, MessageQueue>
        {
            ["billing"] = new(10, 10),
            ["shipping"] = new(10, 10),
            ["analytics"] = new(10, 10)
        };

        for (var i = 0; i < messages; i++)
            foreach (var subscription in subscriptions.Values)
                subscription.Send($"order-{i}", "placed");    // the broker copies into each

        var seen = subscriptions.ToDictionary(pair => pair.Key, pair => Drain(pair.Value, pair.Key));

        return new FanOutReport("topic + 3 subscriptions", messages, seen["billing"], seen["shipping"], seen["analytics"]);
    }

    private static int Drain(MessageQueue queue, string consumerId)
    {
        var handled = 0;
        while (queue.Receive(consumerId) is { } message)
        {
            handled++;
            queue.Complete(message.MessageId);
        }

        return handled;
    }

    // ── 4. a malformed message ────────────────────────────────────────────────────────────────────
    //
    // MaxDeliveryCount is a safety net for failures you did not classify, not the primary mechanism.
    // A missing field does not appear on the fourth attempt.
    public static PoisonReport MalformedMessage(bool classifyFailures, int maxDeliveryCount)
    {
        var queue = new MessageQueue(lockDurationTicks: 10, maxDeliveryCount);
        queue.Send("msg-bad", "{ no customerId }");

        var runs = 0;

        while (queue.Receive("consumer") is { } message)
        {
            runs++;

            if (classifyFailures)
            {
                // Permanent: nothing about this will change on a retry.
                queue.DeadLetter(message.MessageId, "InvalidSchema", "missing customerId");
                break;
            }

            queue.Abandon(message.MessageId);        // treated as transient, and retried to the limit
        }

        return new PoisonReport(
            classifyFailures ? "classify, then dead-letter" : "let it retry to the limit",
            runs,
            queue.DeadLetterQueue.Count);
    }

    // ── 5. ordering ───────────────────────────────────────────────────────────────────────────────
    //
    // A queue is FIFO going IN, not coming OUT. Ordered dequeue is not ordered processing: with several
    // consumers the work overlaps and finishes in whatever order the handlers happen to take.
    public static OrderingReport CompetingConsumers(int keys, int eventsPerKey, int consumers) =>
        RunOrdering("competing consumers", keys, eventsPerKey, consumers, useSessions: false);

    public static OrderingReport Sessions(int keys, int eventsPerKey, int consumers) =>
        RunOrdering("sessions", keys, eventsPerKey, consumers, useSessions: true);

    private static OrderingReport RunOrdering(string topology, int keys, int eventsPerKey, int consumers, bool useSessions)
    {
        var completionOrder = new Dictionary<int, List<int>>();
        var pending = new List<(int Key, int Sequence)>();

        for (var key = 0; key < keys; key++)
            for (var sequence = 0; sequence < eventsPerKey; sequence++)
                pending.Add((key, sequence));

        if (useSessions)
        {
            // One session is locked to one consumer, so a key's events never overlap.
            foreach (var (key, sequence) in pending)
                completionOrder.TryAdd(key, []);

            foreach (var (key, sequence) in pending)
                completionOrder[key].Add(sequence);
        }
        else
        {
            // Consumers take messages in order but finish at different speeds, so completion order is
            // driven by handler duration, not by dequeue order.
            var inFlight = new List<(int Key, int Sequence, int FinishesAt)>();
            var clock = 0;

            foreach (var (key, sequence) in pending)
            {
                var duration = sequence % consumers == 0 ? 5 : 1;    // uneven work is what reorders them
                inFlight.Add((key, sequence, clock + duration));
                clock++;
            }

            foreach (var (key, sequence, _) in inFlight.OrderBy(item => item.FinishesAt).ThenBy(item => item.Key))
            {
                completionOrder.TryAdd(key, []);
                completionOrder[key].Add(sequence);
            }
        }

        var outOfOrder = completionOrder.Count(entry => !IsAscending(entry.Value));
        return new OrderingReport(topology, keys, outOfOrder);
    }

    private static bool IsAscending(IReadOnlyList<int> values)
    {
        for (var i = 1; i < values.Count; i++)
            if (values[i] <= values[i - 1])
                return false;

        return true;
    }
}
