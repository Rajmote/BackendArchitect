using BackendArchitect.Distributed.Messaging.Queues;

namespace BackendArchitect.Tests.Distributed;

public class QueuesTests
{
    [Fact]
    public void Receive_and_delete_loses_a_message_when_the_consumer_crashes()
    {
        var run = QueueScenarios.ConsumerCrashes(ReceiveMode.ReceiveAndDelete, messages: 50, crashEvery: 5);

        Assert.Equal(10, run.Lost);
        Assert.Equal(0, run.Redelivered);
    }

    [Fact]
    public void Peek_lock_redelivers_instead_of_losing_it()
    {
        var run = QueueScenarios.ConsumerCrashes(ReceiveMode.PeekLock, messages: 50, crashEvery: 5);

        Assert.Equal(0, run.Lost);
        Assert.Equal(10, run.Redelivered);
        Assert.Equal(50, run.SideEffectsApplied);
    }

    [Fact]
    public void An_expired_lock_returns_the_message_to_the_queue()
    {
        var queue = new MessageQueue(lockDurationTicks: 10, maxDeliveryCount: 5);
        queue.Send("msg-1", "body");

        var first = queue.Receive("consumer-a");
        queue.Advance(11);

        Assert.NotNull(first);
        Assert.Equal(1, first.DeliveryCount);
        Assert.Equal(1, queue.ActiveCount);
        Assert.Equal(0, queue.LockedCount);
    }

    [Fact]
    public void Completing_after_the_lock_is_lost_does_nothing()
    {
        var queue = new MessageQueue(lockDurationTicks: 10, maxDeliveryCount: 5);
        queue.Send("msg-1", "body");
        var message = queue.Receive("consumer-a")!;

        queue.Advance(11);
        var completed = queue.Complete(message.MessageId);

        // The handler finished, but another consumer already owns the message.
        Assert.False(completed);
        Assert.Equal(1, queue.ActiveCount);
    }

    [Fact]
    public void A_handler_slower_than_the_lock_applies_the_side_effect_twice()
    {
        var run = QueueScenarios.SlowHandler(renewLock: false, messages: 10, lockTicks: 30, handlerTicks: 45);

        Assert.True(run.DuplicateProcessing > 0, "the lock expired while the first consumer was still working");
    }

    [Fact]
    public void Renewing_the_lock_removes_the_duplicates()
    {
        var run = QueueScenarios.SlowHandler(renewLock: true, messages: 10, lockTicks: 30, handlerTicks: 45);

        Assert.Equal(0, run.DuplicateProcessing);
        Assert.Equal(10, run.TimesProcessed);
    }

    [Fact]
    public void Three_services_sharing_one_queue_each_see_only_a_third()
    {
        var run = QueueScenarios.ThreeServicesShareOneQueue(messages: 30);

        Assert.Equal(10, run.SeenByBilling);
        Assert.Equal(10, run.SeenByShipping);
        Assert.Equal(10, run.SeenByAnalytics);
        Assert.Equal(30, run.SeenByBilling + run.SeenByShipping + run.SeenByAnalytics);
    }

    [Fact]
    public void A_topic_gives_every_subscription_a_full_copy()
    {
        var run = QueueScenarios.TopicWithOneSubscriptionEach(messages: 30);

        Assert.Equal(30, run.SeenByBilling);
        Assert.Equal(30, run.SeenByShipping);
        Assert.Equal(30, run.SeenByAnalytics);
    }

    [Fact]
    public void Letting_a_malformed_message_retry_burns_the_whole_delivery_budget()
    {
        var run = QueueScenarios.MalformedMessage(classifyFailures: false, maxDeliveryCount: 10);

        Assert.Equal(10, run.HandlerRuns);
        Assert.Equal(1, run.DeadLettered);
    }

    [Fact]
    public void Classifying_the_failure_dead_letters_it_on_the_first_attempt()
    {
        var run = QueueScenarios.MalformedMessage(classifyFailures: true, maxDeliveryCount: 10);

        Assert.Equal(1, run.HandlerRuns);
        Assert.Equal(1, run.DeadLettered);
    }

    [Fact]
    public void Dead_lettering_records_why()
    {
        var queue = new MessageQueue(lockDurationTicks: 10, maxDeliveryCount: 5);
        queue.Send("msg-1", "{ no customerId }");
        var message = queue.Receive("consumer")!;

        queue.DeadLetter(message.MessageId, "InvalidSchema", "missing customerId");

        var dead = Assert.Single(queue.DeadLetterQueue);
        Assert.Equal("InvalidSchema", dead.Reason);
        Assert.Equal("missing customerId", dead.Description);
    }

    [Fact]
    public void Competing_consumers_process_a_keys_events_out_of_sequence()
    {
        var run = QueueScenarios.CompetingConsumers(keys: 12, eventsPerKey: 3, consumers: 5);

        Assert.True(run.KeysOutOfOrder > 0, "ordered dequeue is not ordered processing");
    }

    [Fact]
    public void Sessions_keep_each_keys_events_in_sequence()
    {
        var run = QueueScenarios.Sessions(keys: 12, eventsPerKey: 3, consumers: 5);

        Assert.Equal(0, run.KeysOutOfOrder);
    }
}
