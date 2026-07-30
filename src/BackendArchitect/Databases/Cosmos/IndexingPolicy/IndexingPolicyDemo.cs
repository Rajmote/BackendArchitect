using System.Globalization;

namespace BackendArchitect.Databases.Cosmos.Indexing;

// Example runner: the same workload priced under three indexing policies, showing that BOTH extremes
// lose — over-indexing punishes writes, under-indexing punishes reads, and right-sizing wins.
public class IndexingPolicyDemo
{
    private static readonly string[] AllPaths =
        ["/customerId", "/product", "/price", "/status", "/notes", "/address", "/metadata", "/history"];

    private const string HotFilter = "/product";   // the one path our queries actually filter on
    private const double DocumentKb = 1.0;
    private const int DocumentsInPartition = 1000;
    private const int MatchingDocuments = 10;
    private const int Writes = 100;
    private const int Queries = 100;

    public void Run()
    {
        var cost = new WorkloadCost(DocumentKb, DocumentsInPartition);

        var policies = new[]
        {
            IndexPolicy.IndexEverything(AllPaths),
            IndexPolicy.IndexNothing(),
            IndexPolicy.RightSized("/customerId", HotFilter),
        };

        Console.WriteLine($"Workload: {Writes} writes + {Queries} queries filtering on {HotFilter}");
        Console.WriteLine($"          ({DocumentsInPartition} docs in the partition, {MatchingDocuments} match)");
        Console.WriteLine();
        Console.WriteLine($"  {"policy",-26}{"indexed",8}{"write",9}{"query",9}{"TOTAL RU",11}");

        var ic = CultureInfo.InvariantCulture;
        var best = policies.MinBy(p => cost.TotalCost(p, Writes, Queries, HotFilter, MatchingDocuments))!;

        foreach (var policy in policies)
        {
            var write = cost.WriteCost(policy);
            var query = cost.QueryCost(policy, HotFilter, MatchingDocuments);
            var total = cost.TotalCost(policy, Writes, Queries, HotFilter, MatchingDocuments);
            var marker = policy.Name == best.Name ? "  <- cheapest" : "";

            Console.WriteLine($"  {policy.Name,-26}{policy.IndexedPropertyCount,8}" +
                              $"{write.ToString("0.0", ic),9}{query.ToString("0.0", ic),9}" +
                              $"{total.ToString("0", ic),11}{marker}");
        }

        Console.WriteLine();
        Console.WriteLine("  index everything -> writes pay for 8 indexes, 6 of which nobody queries");
        Console.WriteLine("  index nothing    -> every query falls back to scanning the whole partition");
        Console.WriteLine("  right-sized      -> index exactly what you filter on: cheap writes AND cheap reads");
    }
}
