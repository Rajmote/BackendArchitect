using System.Globalization;

namespace BackendArchitect.Databases.Cosmos.RequestUnits;

// Example runner: what operations cost, and what happens when a workload exceeds the provisioned
// budget — plus the fix (trim the indexing policy so writes get cheaper).
public class RequestUnitsDemo
{
    private const double DocumentKb = 1.0;
    private const int FullyIndexed = 8;   // Cosmos indexes every property by default
    private const int TrimmedIndex = 2;   // only what we actually filter on
    private const double ProvisionedRuPerSecond = 400;
    private const int WritesInOneSecond = 60;

    public void Run()
    {
        var ic = CultureInfo.InvariantCulture;

        Console.WriteLine("What operations cost (RU):");
        Cost("point read, 1 KB", RuCost.PointRead(DocumentKb), "<- the anchor");
        Cost($"write, 1 KB, {FullyIndexed} indexed properties", RuCost.Write(DocumentKb, FullyIndexed), "<- ~9x a read");
        Cost($"write, 1 KB, {TrimmedIndex} indexed properties", RuCost.Write(DocumentKb, TrimmedIndex), "<- trimmed policy");
        Cost("query, 1 partition, 5 docs examined", RuCost.Query(5, 1), "");
        Cost("query, 4 partitions, 1000 docs examined", RuCost.Query(1000, 4), "<- fan-out + scanning");

        void Cost(string label, double ru, string note) =>
            Console.WriteLine($"  {label,-40}: {ru.ToString("0.0", ic),6}   {note}");

        Console.WriteLine();
        Console.WriteLine($"Provisioned {ProvisionedRuPerSecond} RU/s; workload = {WritesInOneSecond} writes in one second:");
        Report("full indexing", RunWrites(FullyIndexed));
        Report("trimmed index", RunWrites(TrimmedIndex));
        Console.WriteLine("  -> same workload; the only change was how much each write costs.");
    }

    private static ThroughputBudget RunWrites(int indexedProperties)
    {
        var budget = new ThroughputBudget(ProvisionedRuPerSecond);
        var charge = RuCost.Write(DocumentKb, indexedProperties);
        for (var i = 0; i < WritesInOneSecond; i++)
            budget.TryConsume(charge);   // false = 429, which the SDK would then retry
        return budget;
    }

    private static void Report(string label, ThroughputBudget budget)
    {
        var ic = CultureInfo.InvariantCulture;
        var verdict = budget.Throttled > 0 ? $"{budget.Throttled} requests got 429 (retried -> latency)" : "no throttling";
        Console.WriteLine($"  {label}: consumed {budget.Consumed.ToString("0.0", ic),5} RU -> {verdict}");
    }
}
