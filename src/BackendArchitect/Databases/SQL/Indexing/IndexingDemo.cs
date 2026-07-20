using System.Globalization;

namespace BackendArchitect.Databases.Sql.Indexing;

// Example runner: shows the scan-vs-seek cost gap on a synthetic 100k-row "table".
public class IndexingDemo
{
    public void Run()
    {
        var ic = CultureInfo.InvariantCulture;
        const int rows = 100_000;
        var table = Enumerable.Range(1, rows)
                              .Select(i => new Customer(i, $"user{i:D6}@example.com"))
                              .ToList();

        var target = $"user{rows:D6}@example.com";   // worst case for a scan: the last row

        var scan = IndexIntuition.Scan(table, target);

        var index = new EmailIndex(table);            // build the index once...
        var seek = IndexIntuition.Seek(index, target); // ...then seek is cheap

        Console.WriteLine($"Table size            : {rows.ToString("N0", ic)} rows");
        Console.WriteLine($"Full SCAN comparisons : {scan.Comparisons.ToString("N0", ic)}  (O(n))");
        Console.WriteLine($"Index SEEK comparisons: {seek.Comparisons.ToString("N0", ic)}       (O(log n))");
        Console.WriteLine($"Speed-up              : ~{(scan.Comparisons / (double)seek.Comparisons).ToString("N0", ic)}x fewer reads");
    }
}
