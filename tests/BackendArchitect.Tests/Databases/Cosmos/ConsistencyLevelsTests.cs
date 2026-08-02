using BackendArchitect.Databases.Cosmos.Consistency;

namespace BackendArchitect.Tests.Databases.Cosmos;

// Databases · Cosmos · Consistency levels — what each level lets a read see.
public class ConsistencyLevelsTests
{
    // Primary has v1..v5; the replica has only caught up to v3.
    private static (ReplicatedStore Store, int Token) LaggingStore()
    {
        var store = new ReplicatedStore();
        var token = 0;
        for (var v = 1; v <= 5; v++)
            token = store.Write($"v{v}");
        store.Replicate(3);
        return (store, token);
    }

    [Fact]
    public void Strong_AlwaysReturnsTheLatestWrite_EvenWhileTheReplicaLags()
    {
        var (store, _) = LaggingStore();

        Assert.Equal("v5", store.Read(CosmosConsistency.Strong));
        Assert.Equal(2, store.ReplicationLag);   // ...despite the replica being behind
    }

    [Fact]
    public void Eventual_ReturnsWhateverTheReplicaHas()
    {
        var (store, _) = LaggingStore();

        Assert.Equal("v3", store.Read(CosmosConsistency.Eventual));
    }

    [Fact]
    public void Session_LetsTheAuthorReadTheirOwnWrites_ButOthersSeeStaleData()
    {
        var (store, token) = LaggingStore();

        var author = store.Read(CosmosConsistency.Session, sessionToken: token);
        var otherUser = store.Read(CosmosConsistency.Session, sessionToken: 0);

        Assert.Equal("v5", author);      // read-your-own-writes
        Assert.Equal("v3", otherUser);   // someone else may still see the old value
    }

    [Fact]
    public void BoundedStaleness_NeverFallsFurtherBehindThanItsBound()
    {
        var (store, _) = LaggingStore();

        Assert.Equal("v4", store.Read(CosmosConsistency.BoundedStaleness, maxStaleness: 1));
        Assert.Equal("v3", store.Read(CosmosConsistency.BoundedStaleness, maxStaleness: 2));
    }

    [Fact]
    public void ConsistentPrefix_ReturnsAnUnbrokenPrefix_NeverSkippingAWrite()
    {
        var (store, _) = LaggingStore();

        // v3 means v1 and v2 are also visible — you never see v5 before v4.
        Assert.Equal("v3", store.Read(CosmosConsistency.ConsistentPrefix));
    }

    [Fact]
    public void StrongAndBoundedStaleness_CostDoubleOnReads()
    {
        Assert.Equal(2.0, ReplicatedStore.ReadCostMultiplier(CosmosConsistency.Strong));
        Assert.Equal(2.0, ReplicatedStore.ReadCostMultiplier(CosmosConsistency.BoundedStaleness));
        Assert.Equal(1.0, ReplicatedStore.ReadCostMultiplier(CosmosConsistency.Session));
        Assert.Equal(1.0, ReplicatedStore.ReadCostMultiplier(CosmosConsistency.Eventual));
    }

    [Fact]
    public void OnceReplicationCatchesUp_EveryLevelAgrees()
    {
        var (store, token) = LaggingStore();

        store.Replicate(2);   // fully caught up

        Assert.Equal(0, store.ReplicationLag);
        foreach (var level in Enum.GetValues<CosmosConsistency>())
            Assert.Equal("v5", store.Read(level, sessionToken: token));
    }
}
