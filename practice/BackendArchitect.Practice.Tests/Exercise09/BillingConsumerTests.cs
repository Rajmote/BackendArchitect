using BackendArchitect.Practice.Exercise09;

namespace BackendArchitect.Practice.Tests.Exercise09;

// Exercise 09 — three starter tests showing the target shape.
//
// YOUR JOB: make these pass, then add tests for:
//   * idempotency   — consuming the same partition twice applies each charge ONCE
//   * transient     — a failure that succeeds on attempt 2 is processed, not dead-lettered
//   * permanent     — an exhausted record is dead-lettered AND committed past
//   * resume        — a second ConsumeAll starts from the committed offset, not from 0
//
// 🌟 And VERIFY THE TESTS CAN FAIL: commit BEFORE processing and confirm "nothing is lost" goes red.
public class BillingConsumerTests
{
    private static OrderLog LogWith(params string[] bodies)
    {
        var log = new OrderLog(partitionCount: 1);
        foreach (var body in bodies)
            log.Append(partition: 0, "customer-1", body);

        return log;
    }

    [Fact]
    public void ItProcessesEveryRecordInThePartition()
    {
        var log = LogWith("ok", "ok", "ok");
        var consumer = new BillingConsumer(log, "billing", maxAttempts: 3, _ => true);

        var report = consumer.ConsumeAll(partition: 0);

        Assert.Equal(3, report.Processed);
        Assert.Equal(3, consumer.ChargesApplied);
        Assert.False(report.PartitionBlocked);
    }

    [Fact]
    public void ItCommitsPastEveryRecordItHandled()
    {
        var log = LogWith("ok", "ok", "ok");
        var consumer = new BillingConsumer(log, "billing", maxAttempts: 3, _ => true);

        consumer.ConsumeAll(partition: 0);

        Assert.Equal(3, consumer.CommittedOffset(partition: 0));
    }

    [Fact]
    public void APermanentFailureIsDeadLetteredAndTheOffsetMovesPastIt()
    {
        var log = LogWith("ok", "{ malformed }", "ok");
        var consumer = new BillingConsumer(log, "billing", maxAttempts: 3, record =>
            record.Body == "{ malformed }"
                ? throw new InvalidOperationException("missing customerId")
                : true);

        var report = consumer.ConsumeAll(partition: 0);

        Assert.Equal(1, report.DeadLettered);
        Assert.Equal(2, report.Processed);
        Assert.Equal(3, consumer.CommittedOffset(partition: 0));   // parking without committing fixes nothing
        Assert.False(report.PartitionBlocked);
    }

    // TODO (you): idempotency — ConsumeAll, then SeekToBeginning(0), then ConsumeAll again.
    //             ChargesApplied must still be 3, and DuplicatesSuppressed must be 3.

    // TODO (you): transient — a charge that throws TimeoutException once then succeeds must end up
    //             Processed, with nothing dead-lettered.

    // TODO (you): exhausted retries — a charge that always throws TimeoutException is dead-lettered
    //             after maxAttempts, and the offset still moves past it.

    // TODO (you): resume — consume, append two more records, consume again. The second call processes
    //             only the two new ones.
}
