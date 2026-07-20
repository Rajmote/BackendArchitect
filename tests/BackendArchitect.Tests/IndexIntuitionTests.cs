using BackendArchitect.Databases;

namespace BackendArchitect.Tests;

// Month 1 — proving the scan vs seek scaling story.
public class IndexIntuitionTests
{
    private static List<Customer> Table(int rows) =>
        Enumerable.Range(1, rows)
                  .Select(i => new Customer(i, $"user{i:D6}@example.com"))
                  .ToList();

    [Fact]
    public void Scan_FindsRow_ButTouchesEveryRowUpToIt()
    {
        var table = Table(1000);
        var last = "user001000@example.com";

        var (row, comparisons) = IndexIntuition.Scan(table, last);

        Assert.NotNull(row);
        Assert.Equal(1000, row!.Id);
        Assert.Equal(1000, comparisons); // worst case: full scan touches all rows
    }

    [Fact]
    public void Seek_FindsSameRow_WithLogarithmicComparisons()
    {
        var table = Table(1000);
        var index = new EmailIndex(table);

        var (row, comparisons) = IndexIntuition.Seek(index, "user001000@example.com");

        Assert.NotNull(row);
        Assert.Equal(1000, row!.Id);
        Assert.True(comparisons <= 11, $"log2(1000) ~= 10; got {comparisons}");
    }

    [Fact]
    public void Seek_IsDramaticallyCheaperThanScan_AtScale()
    {
        var table = Table(100_000);
        var index = new EmailIndex(table);
        var target = "user100000@example.com";

        var scan = IndexIntuition.Scan(table, target);
        var seek = IndexIntuition.Seek(index, target);

        Assert.Equal(100_000, scan.Comparisons);
        Assert.True(seek.Comparisons < 20);            // ~17 for 100k
        Assert.True(scan.Comparisons > seek.Comparisons * 1000);
    }

    [Fact]
    public void Seek_ReturnsNull_WhenValueMissing()
    {
        var index = new EmailIndex(Table(100));

        var (row, _) = IndexIntuition.Seek(index, "nobody@example.com");

        Assert.Null(row);
    }
}
