namespace BackendArchitect.Apis.GraphQL.Basics;

// Example runner: the two problems GraphQL solves (over- and under-fetching), and the two costs it
// introduces (the N+1 problem and unbounded query cost).
public class GraphQlDemo
{
    public void Run()
    {
        const int products = 50;
        const int requestedFields = 2;      // the list screen needs name + thumbnail

        Console.WriteLine("OVER-fetching — a list screen needing 2 of 25 fields:");
        var rest = FetchComparison.RestBytes(products);
        var graph = FetchComparison.GraphQlBytes(products, requestedFields);
        Console.WriteLine($"  REST    (server decides the shape): {rest,6} bytes");
        Console.WriteLine($"  GraphQL (client decides the shape): {graph,6} bytes  -> {100 - graph * 100 / rest}% less");

        Console.WriteLine();
        Console.WriteLine("UNDER-fetching — user + 10 orders + the items of each order:");
        var restTrips = FetchComparison.RestRoundTrips(10);
        var graphTrips = FetchComparison.GraphQlRoundTrips();
        Console.WriteLine($"  REST    : {restTrips,2} sequential round trips -> ~{FetchComparison.LatencyMs(restTrips)} ms on mobile");
        Console.WriteLine($"  GraphQL : {graphTrips,2} round trip             -> ~{FetchComparison.LatencyMs(graphTrips)} ms");
        Console.WriteLine("  -> round trips cost far more than bytes: each pays full network latency");

        Console.WriteLine();
        Console.WriteLine("The cost GraphQL introduces — N+1 on the server (10 orders, each with a customer):");
        var engine = new ResolverEngine();
        Console.WriteLine($"  naive resolvers  : {engine.ResolveOrdersWithCustomers_Naive(10)} queries  <- 1 + N");
        engine.Reset();
        Console.WriteLine($"  with DataLoader  : {engine.ResolveOrdersWithCustomers_Batched(10)} queries  <- batched into WHERE Id IN (...)");
        engine.Reset();
        Console.WriteLine($"  nested (10 orders x 5 items): {engine.ResolveNested_Naive(10, 5)} queries unbatched");

        Console.WriteLine();
        Console.WriteLine("The other cost — the client controls query cost, so the server must set limits:");
        var guard = new QueryGuard(maxDepth: 10, maxComplexity: 1000, maxPageSize: 100);
        Report(guard, "normal query", depth: 3, [10, 10]);
        Report(guard, "deeply nested", depth: 25, [10, 10, 10]);
        Report(guard, "shallow but huge", depth: 2, [1_000_000]);
        Report(guard, "cost explosion", depth: 4, [50, 50, 50]);
    }

    private static void Report(QueryGuard guard, string label, int depth, int[] pageSizes)
    {
        var verdict = guard.Check(depth, pageSizes);
        Console.WriteLine($"  {(verdict.Allowed ? "ALLOW " : "REJECT")} {label,-18} {verdict.Reason}");
    }
}
