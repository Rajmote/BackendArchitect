using BackendArchitect.Databases.Cosmos.Indexing;

namespace BackendArchitect.Tests.Databases.Cosmos;

// Databases · Cosmos · Indexing policy — both extremes lose; right-sizing wins.
public class IndexingPolicyTests
{
    private static readonly string[] AllPaths =
        ["/customerId", "/product", "/price", "/status", "/notes", "/address", "/metadata", "/history"];

    private const string HotFilter = "/product";
    private const int Writes = 100;
    private const int Queries = 100;
    private const int Matching = 10;

    private static WorkloadCost Cost() => new(documentKb: 1.0, documentsInPartition: 1000);

    [Fact]
    public void IndexingEverything_MakesWritesExpensive()
    {
        var cost = Cost();

        var all = cost.WriteCost(IndexPolicy.IndexEverything(AllPaths));
        var none = cost.WriteCost(IndexPolicy.IndexNothing());

        Assert.True(all > none, "more indexes must cost more per write");
        Assert.Equal(4.0, all - none, precision: 3); // 8 properties x 0.5 RU
    }

    [Fact]
    public void IndexingNothing_MakesQueriesFallBackToAFullScan()
    {
        var cost = Cost();

        var indexed = cost.QueryCost(IndexPolicy.RightSized(HotFilter), HotFilter, Matching);
        var unindexed = cost.QueryCost(IndexPolicy.IndexNothing(), HotFilter, Matching);

        Assert.True(unindexed > indexed * 5,
            $"an unindexed filter scans the partition; {unindexed} vs {indexed}");
    }

    [Fact]
    public void RightSizedPolicy_BeatsBothExtremes_OnTotalCost()
    {
        var cost = Cost();

        var everything = cost.TotalCost(IndexPolicy.IndexEverything(AllPaths), Writes, Queries, HotFilter, Matching);
        var nothing = cost.TotalCost(IndexPolicy.IndexNothing(), Writes, Queries, HotFilter, Matching);
        var rightSized = cost.TotalCost(IndexPolicy.RightSized("/customerId", HotFilter), Writes, Queries, HotFilter, Matching);

        Assert.True(rightSized < everything, $"right-sized {rightSized} should beat index-everything {everything}");
        Assert.True(rightSized < nothing, $"right-sized {rightSized} should beat index-nothing {nothing}");
    }

    [Fact]
    public void IsIndexed_ReflectsThePolicy()
    {
        var policy = IndexPolicy.RightSized("/customerId", "/product");

        Assert.True(policy.IsIndexed("/product"));
        Assert.False(policy.IsIndexed("/notes"));
        Assert.Equal(2, policy.IndexedPropertyCount);
    }

    [Fact]
    public void IndexEverything_IndexesEveryPath()
    {
        var policy = IndexPolicy.IndexEverything(AllPaths);

        Assert.All(AllPaths, path => Assert.True(policy.IsIndexed(path)));
    }
}
