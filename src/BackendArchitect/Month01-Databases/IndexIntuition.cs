namespace BackendArchitect.Databases;

// A record in our tiny "table".
public record Customer(int Id, string Email);

// A deliberately simplified model of the ONE idea behind database indexing:
//   * A table is an UNORDERED pile of rows -> finding a value means a full SCAN (O(n)).
//   * An index is a SORTED structure over one column -> finding a value is a SEEK (O(log n)).
//
// We count "comparisons" as a stand-in for the database's "logical reads" cost signal:
// the honest measure of how much work a lookup did. This is an analogy, not a B-tree —
// but the scaling behaviour (n vs log2 n) is exactly what a real index buys you.
public static class IndexIntuition
{
    // FULL TABLE SCAN: walk every row until we find the match (or run out).
    // Returns the found row (or null) and how many rows we had to touch.
    public static (Customer? Row, int Comparisons) Scan(IReadOnlyList<Customer> table, string email)
    {
        var comparisons = 0;
        foreach (var row in table)
        {
            comparisons++;
            if (row.Email == email)
                return (row, comparisons);
        }

        return (null, comparisons);
    }

    // INDEX SEEK: binary-search a column sorted by Email. O(log n) comparisons.
    // The index stores (key, row) pairs kept in sorted order by key.
    public static (Customer? Row, int Comparisons) Seek(EmailIndex index, string email)
        => index.Find(email);
}

// A stand-in for a non-clustered index on Customer.Email: the emails kept SORTED,
// each pointing back to its full row. Built once, then reused for fast lookups.
public sealed class EmailIndex
{
    private readonly (string Email, Customer Row)[] _entries;

    public EmailIndex(IEnumerable<Customer> rows)
        => _entries = rows.Select(r => (r.Email, r))
                          .OrderBy(e => e.Email, StringComparer.Ordinal)
                          .ToArray();

    public (Customer? Row, int Comparisons) Find(string email)
    {
        int lo = 0, hi = _entries.Length - 1, comparisons = 0;
        while (lo <= hi)
        {
            comparisons++;
            int mid = lo + (hi - lo) / 2;
            int cmp = string.CompareOrdinal(_entries[mid].Email, email);
            if (cmp == 0)
                return (_entries[mid].Row, comparisons);
            if (cmp < 0)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        return (null, comparisons);
    }
}
