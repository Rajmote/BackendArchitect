using BackendArchitect.Apis.GraphQL.Basics;

namespace BackendArchitect.Tests.Apis;

// APIs · GraphQL · Basics — the two problems it solves and the two costs it introduces.
public class GraphQlBasicsTests
{
    [Fact]
    public void GraphQl_TransfersFarLessThanRest_WhenTheClientNeedsFewFields()
    {
        var rest = FetchComparison.RestBytes(products: 50);
        var graphql = FetchComparison.GraphQlBytes(products: 50, requestedFields: 2);

        Assert.True(graphql < rest / 10, $"2 of 25 fields should be ~92% smaller; {graphql} vs {rest}");
    }

    [Fact]
    public void RestRoundTrips_GrowWithTheNumberOfChildren_ButGraphQlStaysAtOne()
    {
        Assert.Equal(12, FetchComparison.RestRoundTrips(orders: 10));  // 1 user + 1 orders + 10 items
        Assert.Equal(1, FetchComparison.GraphQlRoundTrips());
    }

    [Fact]
    public void RoundTripsDominateLatencyOnMobile()
    {
        var rest = FetchComparison.LatencyMs(FetchComparison.RestRoundTrips(10));
        var graphql = FetchComparison.LatencyMs(FetchComparison.GraphQlRoundTrips());

        Assert.Equal(2400, rest);   // 12 x 200 ms — seconds of pure waiting
        Assert.Equal(200, graphql);
    }

    [Fact]
    public void NaiveResolvers_ProduceNPlusOneQueries()
    {
        var engine = new ResolverEngine();

        var queries = engine.ResolveOrdersWithCustomers_Naive(orderCount: 10);

        Assert.Equal(11, queries);   // 1 for the orders + 10 for the customers
    }

    [Fact]
    public void Batching_CollapsesNPlusOneIntoTwoQueries()
    {
        var engine = new ResolverEngine();

        var queries = engine.ResolveOrdersWithCustomers_Batched(orderCount: 10);

        Assert.Equal(2, queries);    // orders + WHERE Id IN (...)
    }

    [Fact]
    public void NestingMakesTheNPlusOneProblemMultiplicative()
    {
        var engine = new ResolverEngine();

        var queries = engine.ResolveNested_Naive(orders: 10, itemsPerOrder: 5);

        Assert.Equal(61, queries);   // 1 + 10 + 50
    }

    [Fact]
    public void Guard_RejectsDeeplyNestedQueries()
    {
        var guard = new QueryGuard(maxDepth: 10);

        var verdict = guard.Check(depth: 25, [10]);

        Assert.False(verdict.Allowed);
        Assert.Contains("depth", verdict.Reason);
    }

    [Fact]
    public void Guard_RejectsShallowButHugeQueries() // depth limiting alone is not enough
    {
        var guard = new QueryGuard(maxDepth: 10, maxPageSize: 100);

        var verdict = guard.Check(depth: 2, [1_000_000]);

        Assert.False(verdict.Allowed);
        Assert.Contains("page size", verdict.Reason);
    }

    [Fact]
    public void Guard_RejectsQueriesWhoseCostExplodesThroughNesting()
    {
        var guard = new QueryGuard(maxDepth: 10, maxComplexity: 1000, maxPageSize: 100);

        var verdict = guard.Check(depth: 4, [50, 50, 50]);   // 125,000

        Assert.False(verdict.Allowed);
        Assert.Contains("complexity", verdict.Reason);
    }

    [Fact]
    public void Guard_AllowsAReasonableQuery()
    {
        var guard = new QueryGuard();

        Assert.True(guard.Check(depth: 3, [10, 10]).Allowed);
    }

    [Fact]
    public void Complexity_MultipliesThroughEachLevelOfNesting()
    {
        Assert.Equal(100, QueryGuard.Complexity([10, 10]));
        Assert.Equal(1000, QueryGuard.Complexity([10, 10, 10]));
    }
}
