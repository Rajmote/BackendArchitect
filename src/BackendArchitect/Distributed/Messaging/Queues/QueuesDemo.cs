namespace BackendArchitect.Distributed.Messaging.Queues;

// Example runner: the lock, the lock expiring under a slow handler, fan-out, poison messages, ordering.
public class QueuesDemo
{
    public void Run()
    {
        // --- 1. what a crash costs in each receive mode ---
        const int messages = 50;
        var atMostOnce = QueueScenarios.ConsumerCrashes(ReceiveMode.ReceiveAndDelete, messages, crashEvery: 5);
        var atLeastOnce = QueueScenarios.ConsumerCrashes(ReceiveMode.PeekLock, messages, crashEvery: 5);

        Console.WriteLine($"{messages} messages, the consumer crashes on 1 in 5 before completing:");
        foreach (var run in new[] { atMostOnce, atLeastOnce })
            Console.WriteLine($"  {run.Mode,-20}: {run.SideEffectsApplied,2}/{run.Sent} applied, " +
                              $"{run.Lost,2} lost, {run.Redelivered,2} redelivered");
        Console.WriteLine("  -> peek-lock LENDS the message; the lock expires and it comes back");

        // --- 2. a handler slower than the lock ---
        var noRenewal = QueueScenarios.SlowHandler(renewLock: false, messages: 10, lockTicks: 30, handlerTicks: 45);
        var renewed = QueueScenarios.SlowHandler(renewLock: true, messages: 10, lockTicks: 30, handlerTicks: 45);

        Console.WriteLine();
        Console.WriteLine("10 messages, handler takes 45 ticks, lock lasts 30:");
        foreach (var run in new[] { noRenewal, renewed })
            Console.WriteLine($"  {run.Strategy,-14}: processed {run.TimesProcessed,2} times for {run.Messages} messages " +
                              $"-> {run.DuplicateProcessing,2} duplicate side effect(s)");
        Console.WriteLine("  -> the first consumer is never told to stop; it keeps working while another retries");

        // --- 3. three services ---
        const int orders = 30;
        var shared = QueueScenarios.ThreeServicesShareOneQueue(orders);
        var topic = QueueScenarios.TopicWithOneSubscriptionEach(orders);

        Console.WriteLine();
        Console.WriteLine($"{orders} OrderPlaced messages, needed by billing, shipping AND analytics:");
        foreach (var run in new[] { shared, topic })
            Console.WriteLine($"  {run.Topology,-24}: billing {run.SeenByBilling,2}, shipping {run.SeenByShipping,2}, analytics {run.SeenByAnalytics,2}");
        Console.WriteLine("  -> one shared queue: all three report success having each seen a THIRD of the orders");

        // --- 4. a malformed message ---
        var retried = QueueScenarios.MalformedMessage(classifyFailures: false, maxDeliveryCount: 10);
        var classified = QueueScenarios.MalformedMessage(classifyFailures: true, maxDeliveryCount: 10);

        Console.WriteLine();
        Console.WriteLine("One malformed message that can never succeed:");
        foreach (var run in new[] { retried, classified })
            Console.WriteLine($"  {run.Strategy,-28}: {run.HandlerRuns,2} handler run(s), {run.DeadLettered} dead-lettered");
        Console.WriteLine("  -> retry what might succeed; dead-letter what never will");

        // --- 5. ordering ---
        const int keys = 12;
        var competing = QueueScenarios.CompetingConsumers(keys, eventsPerKey: 3, consumers: 5);
        var sessions = QueueScenarios.Sessions(keys, eventsPerKey: 3, consumers: 5);

        Console.WriteLine();
        Console.WriteLine($"{keys} orders x 3 events (Created, Updated, Cancelled), 5 consumers:");
        foreach (var run in new[] { competing, sessions })
            Console.WriteLine($"  {run.Topology,-22}: {run.KeysOutOfOrder,2} of {run.Keys} orders processed out of sequence");
        Console.WriteLine("  -> ordering holds WITHIN a session; parallelism holds ACROSS sessions");
    }
}
