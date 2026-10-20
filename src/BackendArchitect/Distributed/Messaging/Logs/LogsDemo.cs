namespace BackendArchitect.Distributed.Messaging.Logs;

// Example runner: the partition ceiling, commit order, replay, retention, lag, and a poison message.
public class LogsDemo
{
    public void Run()
    {
        // --- 1. partition count is the ceiling ---
        Console.WriteLine("One consumer group against a 3-partition topic:");
        foreach (var consumers in new[] { 1, 2, 3, 5 })
        {
            var run = LogScenarios.AssignPartitions(partitions: 3, consumers);
            Console.WriteLine($"  {run.Consumers} consumer(s): {run.Working} working, {run.Idle} idle");
        }
        Console.WriteLine("  -> a partition belongs to exactly ONE consumer, so extra consumers are standby only");

        // --- 2. commit order ---
        const int records = 50;
        var atMostOnce = LogScenarios.Crash(commitBeforeProcessing: true, records, crashEvery: 5);
        var atLeastOnce = LogScenarios.Crash(commitBeforeProcessing: false, records, crashEvery: 5);

        Console.WriteLine();
        Console.WriteLine($"{records} records, the process is killed on 1 in 5:");
        foreach (var run in new[] { atMostOnce, atLeastOnce })
            Console.WriteLine($"  {run.Strategy,-22}: {run.SideEffects,2}/{run.Records} applied, " +
                              $"{run.Lost,2} lost, {run.Duplicated,2} duplicated");
        Console.WriteLine("  -> at-most-once loses silently and lag reads ZERO; prefer the failure you can detect");

        // --- 3. replay ---
        const int published = 30;
        var topic = LogScenarios.NewConsumerOnATopic(published);
        var log = LogScenarios.NewConsumerOnALog(published);

        Console.WriteLine();
        Console.WriteLine($"{published} orders published, then the fraud team subscribes NEXT QUARTER:");
        foreach (var run in new[] { topic, log })
            Console.WriteLine($"  {run.Store,-22}: {run.RecordsAvailableToANewConsumer,2} record(s) available to them");
        Console.WriteLine("  -> a subscription only receives what is published after it exists; a log keeps the past");

        // --- 4. retention ---
        var within = LogScenarios.OutageLongerThanRetention(outageTicks: 24, retentionTicks: 48, recordsPerTick: 10);
        var beyond = LogScenarios.OutageLongerThanRetention(outageTicks: 60, retentionTicks: 48, recordsPerTick: 10);

        Console.WriteLine();
        Console.WriteLine("A consumer group is down, 10 events arriving per hour, retention 48h:");
        foreach (var run in new[] { within, beyond })
            Console.WriteLine($"  down {run.OutageTicks,2}h: {run.RecordsLost,3} record(s) deleted before anyone read them  ({run.Scenario})");
        Console.WriteLine("  -> past the retention window, lag stops being a performance problem and becomes DATA LOSS");

        // --- 5. per-partition lag ---
        var lag = LogScenarios.LagPerPartition(
            latest: [10_000, 10_000, 10_000, 10_000],
            committed: [9_980, 4_200, 9_995, 9_988]);

        Console.WriteLine();
        Console.WriteLine("Consumer lag, broken out per partition:");
        foreach (var partition in lag)
            Console.WriteLine($"  partition {partition.Partition}: lag {partition.Lag,5}{(partition.Lag > 1_000 ? "  <- hot key, or a stuck consumer" : "")}");
        Console.WriteLine($"  total lag {lag.Sum(p => p.Lag):N0} looks like 'scale out'; per-partition says 3 of 4 are healthy");
        Console.WriteLine("  -> adding consumers would cost money and fix nothing");

        // --- 6. a poison message ---
        var stalls = LogScenarios.PoisonMessage(deadLetterIt: false, records: 20, poisonOffset: 7);
        var parked = LogScenarios.PoisonMessage(deadLetterIt: true, records: 20, poisonOffset: 7);

        Console.WriteLine();
        Console.WriteLine("20 records in one partition, offset 7 is malformed:");
        foreach (var run in new[] { stalls, parked })
            Console.WriteLine($"  {run.Strategy,-26}: committed to offset {run.PartitionProgress,2}/20, " +
                              $"{run.DeadLettered} dead-lettered, blocked = {run.PartitionBlocked}");
        Console.WriteLine("  -> a log has no DeliveryCount and no built-in DLQ; park it AND commit, or nothing moves");
    }
}
