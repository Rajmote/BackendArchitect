using BackendArchitect.Distributed.Messaging.Logs;

namespace BackendArchitect.Tests.Distributed;

public class LogsTests
{
    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(2, 2, 0)]
    [InlineData(3, 3, 0)]
    [InlineData(5, 3, 2)]
    public void Consumers_beyond_the_partition_count_sit_idle(int consumers, int working, int idle)
    {
        var run = LogScenarios.AssignPartitions(partitions: 3, consumers);

        Assert.Equal(working, run.Working);
        Assert.Equal(idle, run.Idle);
    }

    [Fact]
    public void Committing_before_processing_loses_records_permanently()
    {
        var run = LogScenarios.Crash(commitBeforeProcessing: true, records: 50, crashEvery: 5);

        Assert.Equal(10, run.Lost);
        Assert.Equal(0, run.Duplicated);
    }

    [Fact]
    public void Committing_after_processing_duplicates_instead_of_losing()
    {
        var run = LogScenarios.Crash(commitBeforeProcessing: false, records: 50, crashEvery: 5);

        Assert.Equal(0, run.Lost);
        Assert.Equal(50, run.SideEffects);
        Assert.True(run.Duplicated > 0, "the work was done but never committed, so it replays");
    }

    [Fact]
    public void A_subscription_created_later_sees_none_of_the_history()
    {
        var run = LogScenarios.NewConsumerOnATopic(published: 30);

        Assert.Equal(0, run.RecordsAvailableToANewConsumer);
    }

    [Fact]
    public void A_consumer_group_created_later_can_replay_the_whole_log()
    {
        var run = LogScenarios.NewConsumerOnALog(published: 30);

        Assert.Equal(30, run.RecordsAvailableToANewConsumer);
    }

    [Fact]
    public void An_outage_within_the_retention_window_loses_nothing()
    {
        var run = LogScenarios.OutageLongerThanRetention(outageTicks: 24, retentionTicks: 48, recordsPerTick: 10);

        Assert.Equal(0, run.RecordsLost);
    }

    [Fact]
    public void An_outage_beyond_the_retention_window_loses_the_overflow()
    {
        var run = LogScenarios.OutageLongerThanRetention(outageTicks: 60, retentionTicks: 48, recordsPerTick: 10);

        // 60 hours of events, 48 hours kept: the first ~12 hours are deleted unread.
        Assert.True(run.RecordsLost > 0);
        Assert.InRange(run.RecordsLost, 100, 130);
    }

    [Fact]
    public void Retention_deletes_records_nobody_has_read()
    {
        var log = new EventLog(partitionCount: 1, retentionTicks: 10);
        log.Append("key-1", "body");

        log.Advance(11);

        // Nothing consumed it. The log does not care - retention is a deletion policy.
        Assert.Equal(0, log.RecordCount);
    }

    [Fact]
    public void Lag_on_one_partition_rules_out_a_capacity_problem()
    {
        var lag = LogScenarios.LagPerPartition(
            latest: [10_000, 10_000, 10_000, 10_000],
            committed: [9_980, 4_200, 9_995, 9_988]);

        var unhealthy = lag.Where(partition => partition.Lag > 1_000).ToArray();

        Assert.Single(unhealthy);
        Assert.Equal(1, unhealthy[0].Partition);
        Assert.All(lag.Where(p => p.Partition != 1), p => Assert.True(p.Lag < 100));
    }

    [Fact]
    public void Retrying_a_poison_message_forever_blocks_its_partition()
    {
        var run = LogScenarios.PoisonMessage(deadLetterIt: false, records: 20, poisonOffset: 7);

        Assert.True(run.PartitionBlocked);
        Assert.Equal(7, run.PartitionProgress);      // offsets 8..19 are fine, and unreachable
    }

    [Fact]
    public void Dead_lettering_and_committing_keeps_the_partition_moving()
    {
        var run = LogScenarios.PoisonMessage(deadLetterIt: true, records: 20, poisonOffset: 7);

        Assert.False(run.PartitionBlocked);
        Assert.Equal(20, run.PartitionProgress);
        Assert.Equal(1, run.DeadLettered);
    }

    [Fact]
    public void A_key_always_lands_on_the_same_partition()
    {
        var log = new EventLog(partitionCount: 4);

        for (var i = 0; i < 10; i++)
            log.Append("customer-42", $"event-{i}");

        var partitionsUsed = Enumerable.Range(0, log.PartitionCount)
            .Count(partition => log.Read(partition, 0).Count > 0);

        Assert.Equal(1, partitionsUsed);
    }

    [Fact]
    public void Different_keys_spread_across_partitions()
    {
        var log = new EventLog(partitionCount: 4);

        for (var i = 0; i < 400; i++)
            log.Append($"customer-{i}", "placed");

        var used = Enumerable.Range(0, log.PartitionCount).Select(p => log.Read(p, 0).Count).ToArray();

        Assert.All(used, count => Assert.True(count > 50, $"uneven spread: {string.Join(", ", used)}"));
    }
}
