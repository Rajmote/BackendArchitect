using BackendArchitect.Distributed.Theory;

namespace BackendArchitect.Tests.Distributed;

public class DistributedTheoryTests
{
    [Fact]
    public void A_plain_retry_charges_the_customer_twice_when_the_response_is_lost()
    {
        var run = RemoteCallOutcomes.RunWithRetries(useIdempotencyKey: false, orders: 100, everyNthLosesResponse: 10);

        Assert.Equal(10, run.DoubleCharged);
        Assert.Equal(110, run.ChargesApplied);      // 100 orders, 10 charged twice
    }

    [Fact]
    public void An_idempotency_key_makes_the_same_retry_harmless()
    {
        var run = RemoteCallOutcomes.RunWithRetries(useIdempotencyKey: true, orders: 100, everyNthLosesResponse: 10);

        Assert.Equal(0, run.DoubleCharged);
        Assert.Equal(100, run.ChargesApplied);
    }

    [Fact]
    public void Both_strategies_make_exactly_the_same_calls()
    {
        var plain = RemoteCallOutcomes.RunWithRetries(useIdempotencyKey: false, orders: 100, everyNthLosesResponse: 10);
        var keyed = RemoteCallOutcomes.RunWithRetries(useIdempotencyKey: true, orders: 100, everyNthLosesResponse: 10);

        // The network behaved identically. Only the server's interpretation of the second call differs.
        Assert.Equal(plain.CallsMade, keyed.CallsMade);
    }

    [Fact]
    public void A_cp_system_refuses_every_write_on_the_minority_side()
    {
        var run = QuorumCluster.RunSplitBrain(PartitionBehaviour.ConsistentAndPartitionTolerant, keys: 10);

        Assert.Equal(10, run.WritesAccepted);       // the majority side only
        Assert.Equal(10, run.WritesRefused);        // the 2-node side cannot reach a quorum of 3
        Assert.Equal(0, run.DivergentKeysAfterHeal);
    }

    [Fact]
    public void An_ap_system_accepts_everything_and_pays_for_it_when_the_split_heals()
    {
        var run = QuorumCluster.RunSplitBrain(PartitionBehaviour.AvailableAndPartitionTolerant, keys: 10);

        Assert.Equal(20, run.WritesAccepted);
        Assert.Equal(0, run.WritesRefused);
        Assert.Equal(10, run.DivergentKeysAfterHeal);   // every key now has two answers
    }

    [Fact]
    public void A_majority_of_five_nodes_is_three()
    {
        var cluster = new QuorumCluster(nodeCount: 5, minoritySize: 2);

        Assert.Equal(3, cluster.Quorum);
    }

    [Fact]
    public void Reading_your_own_write_from_a_replica_returns_nothing()
    {
        var run = ReplicatedStore.MeasureReadAfterWrite(ReadStrategy.FromReplica, writes: 50, delayTicks: 3);

        Assert.True(run.StaleReads > 0, "the replica has not applied the write yet");
    }

    [Fact]
    public void Read_your_writes_never_misses_your_own_write()
    {
        var run = ReplicatedStore.MeasureReadAfterWrite(ReadStrategy.ReadYourWrites, writes: 50, delayTicks: 3);

        Assert.Equal(0, run.StaleReads);
    }

    [Fact]
    public void Replicas_converge_only_once_the_writes_stop()
    {
        var stopped = ReplicatedStore.MeasureConvergence(writesEverStop: true, writes: 50, delayTicks: 3);

        Assert.True(stopped.Converged);
        Assert.Equal(0, stopped.LagAtEnd);
    }

    [Fact]
    public void While_writes_continue_the_lag_never_reaches_zero()
    {
        var ongoing = ReplicatedStore.MeasureConvergence(writesEverStop: false, writes: 50, delayTicks: 3);

        // "Eventually consistent" promises convergence IF writes stop. Live systems never stop.
        Assert.False(ongoing.Converged);
        Assert.Equal(3, ongoing.LagAtEnd);
    }
}
