namespace BackendArchitect.Distributed.Messaging.Logs;

public sealed record AssignmentReport(int Partitions, int Consumers, int Working, int Idle);

public sealed record CommitReport(string Strategy, int Records, int SideEffects, int Lost, int Duplicated);

public sealed record ReplayReport(string Store, int RecordsAvailableToANewConsumer);

public sealed record RetentionReport(string Scenario, long OutageTicks, long RetentionTicks, int RecordsLost);

public sealed record LagReport(int Partition, long Latest, long Committed, long Lag);

public sealed record PoisonReport(string Strategy, int PartitionProgress, int DeadLettered, bool PartitionBlocked);

public static class LogScenarios
{
    // ── 1. partitions cap parallelism within a group ──────────────────────────────────────────────
    //
    // A partition is assigned to exactly ONE consumer in a group, because an offset is a single number
    // and two consumers sharing one would each overwrite the other's claim of what is done.
    public static AssignmentReport AssignPartitions(int partitions, int consumers)
    {
        var working = Math.Min(partitions, consumers);
        return new AssignmentReport(partitions, consumers, working, Math.Max(0, consumers - working));
    }

    // ── 2. commit order IS the delivery guarantee ─────────────────────────────────────────────────
    //
    // Two lines, swapped. Commit first and a crash loses the record permanently - nothing will ever
    // point at it again, and lag reads zero because you committed everything.
    public static CommitReport Crash(bool commitBeforeProcessing, int records, int crashEvery)
    {
        var log = new EventLog(partitionCount: 1);
        for (var i = 0; i < records; i++)
            log.Append($"key-{i}", $"body-{i}");

        var applied = new Dictionary<long, int>();
        long committed = 0;

        while (committed < log.LatestOffset(0))
        {
            var record = log.Read(0, committed).First();
            var crashes = record.Offset % crashEvery == 0 && !applied.ContainsKey(record.Offset);

            if (commitBeforeProcessing)
            {
                committed = record.Offset + 1;
                if (crashes)
                    continue;                                   // the record is now unreachable
                applied[record.Offset] = applied.GetValueOrDefault(record.Offset) + 1;
            }
            else
            {
                applied[record.Offset] = applied.GetValueOrDefault(record.Offset) + 1;
                if (crashes)
                    continue;                                   // done, but never committed -> replayed
                committed = record.Offset + 1;
            }
        }

        return new CommitReport(
            commitBeforeProcessing ? "commit, then process" : "process, then commit",
            records,
            applied.Count,
            records - applied.Count,
            applied.Values.Sum() - applied.Count);
    }

    // ── 3. replay: what a consumer created LATER can see ──────────────────────────────────────────
    //
    // A subscription only receives what is published after it exists. A log keeps the records, so a
    // group created next quarter can set its offset to the beginning.
    public static ReplayReport NewConsumerOnATopic(int published)
    {
        // A topic copies each message into the subscriptions that EXIST AT PUBLISH TIME.
        var subscriptions = new Dictionary<string, List<string>>
        {
            ["billing"] = [],
            ["shipping"] = [],
            ["analytics"] = []
        };

        for (var i = 0; i < published; i++)
            foreach (var subscription in subscriptions.Values)
                subscription.Add($"order-{i}");

        subscriptions["fraud"] = [];      // the fraud team subscribes next quarter, and nothing is published after

        return new ReplayReport("topic + subscriptions", subscriptions["fraud"].Count);
    }

    public static ReplayReport NewConsumerOnALog(int published)
    {
        var log = new EventLog(partitionCount: 3);
        for (var i = 0; i < published; i++)
            log.Append($"order-{i}", "placed");

        var available = Enumerable.Range(0, log.PartitionCount).Sum(p => log.Read(p, fromOffset: 0).Count);
        return new ReplayReport("log (replay from 0)", available);
    }

    // ── 4. retention is a deletion policy, not a safety net ───────────────────────────────────────
    public static RetentionReport OutageLongerThanRetention(long outageTicks, long retentionTicks, int recordsPerTick)
    {
        var log = new EventLog(partitionCount: 1, retentionTicks);

        for (long tick = 0; tick < outageTicks; tick++)
        {
            for (var i = 0; i < recordsPerTick; i++)
                log.Append($"key-{tick}-{i}", "body");
            log.Advance(1);
        }

        var produced = (int)outageTicks * recordsPerTick;
        var surviving = log.RecordCount;

        return new RetentionReport(
            outageTicks > retentionTicks ? "outage exceeds retention" : "outage within retention",
            outageTicks,
            retentionTicks,
            produced - surviving);
    }

    // ── 5. per-partition lag separates "scale out" from "one consumer is stuck" ───────────────────
    public static IReadOnlyList<LagReport> LagPerPartition(long[] latest, long[] committed) =>
        [.. latest.Select((value, partition) => new LagReport(partition, value, committed[partition], value - committed[partition]))];

    // ── 6. a poison message blocks its PARTITION ──────────────────────────────────────────────────
    //
    // Offsets are sequential, so committing past the bad record would claim it was handled. The
    // consumer cannot skip ahead - and a log has no DeliveryCount and no built-in dead-letter queue.
    public static PoisonReport PoisonMessage(bool deadLetterIt, int records, long poisonOffset)
    {
        var log = new EventLog(partitionCount: 1);
        for (var i = 0; i < records; i++)
            log.Append($"key-{i}", i == poisonOffset ? "{ malformed }" : "ok");

        long committed = 0;
        var deadLettered = 0;
        var attempts = 0;

        while (committed < log.LatestOffset(0) && attempts < records * 3)
        {
            attempts++;
            var record = log.Read(0, committed).First();

            if (record.Body != "{ malformed }")
            {
                committed = record.Offset + 1;
                continue;
            }

            if (!deadLetterIt)
                continue;                                        // retry forever: the partition stalls

            deadLettered++;
            committed = record.Offset + 1;                       // park it AND commit, or nothing moves
        }

        return new PoisonReport(
            deadLetterIt ? "dead-letter, then commit" : "retry forever",
            (int)committed,
            deadLettered,
            committed <= poisonOffset);
    }
}
