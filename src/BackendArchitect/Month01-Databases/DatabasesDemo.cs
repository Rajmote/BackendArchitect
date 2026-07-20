namespace BackendArchitect.Databases;

// Month 1 runner: shows the scan-vs-seek cost gap on a synthetic 100k-row "table".
public class DatabasesDemo
{
    public void Run()
    {
        const int rows = 100_000;
        var table = Enumerable.Range(1, rows)
                              .Select(i => new Customer(i, $"user{i:D6}@example.com"))
                              .ToList();

        var target = $"user{rows:D6}@example.com";   // worst case for a scan: the last row

        var scan = IndexIntuition.Scan(table, target);

        var index = new EmailIndex(table);            // build the index once...
        var seek = IndexIntuition.Seek(index, target); // ...then seek is cheap

        Console.WriteLine($"Table size          : {rows:N0} rows");
        Console.WriteLine($"Full SCAN comparisons: {scan.Comparisons:N0}  (O(n))");
        Console.WriteLine($"Index SEEK comparisons: {seek.Comparisons:N0}       (O(log n))");
        Console.WriteLine($"Speed-up            : ~{scan.Comparisons / (double)seek.Comparisons:N0}x fewer reads");
    }
}
