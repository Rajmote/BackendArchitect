namespace BackendArchitect.Apis.GraphQL.Basics;

public sealed record GuardVerdict(bool Allowed, string Reason);

// REST endpoints have naturally bounded cost — the SERVER decides what a request does. GraphQL hands
// that control to the CLIENT, so a production server must hand-build the limits REST gave for free.
//
// Depth alone is not enough: a SHALLOW query asking for a million rows is just as damaging, which is
// why complexity scoring multiplies field cost by requested list size.
public sealed class QueryGuard
{
    private readonly int _maxDepth;
    private readonly int _maxComplexity;
    private readonly int _maxPageSize;

    public QueryGuard(int maxDepth = 10, int maxComplexity = 1000, int maxPageSize = 100)
    {
        _maxDepth = maxDepth;
        _maxComplexity = maxComplexity;
        _maxPageSize = maxPageSize;
    }

    /// <summary>Cost grows multiplicatively with nesting: each level multiplies by its page size.</summary>
    public static int Complexity(IReadOnlyList<int> pageSizePerLevel) =>
        pageSizePerLevel.Aggregate(1, (total, size) => total * Math.Max(1, size));

    public GuardVerdict Check(int depth, IReadOnlyList<int> pageSizePerLevel)
    {
        if (depth > _maxDepth)
            return new GuardVerdict(false, $"depth {depth} exceeds the limit of {_maxDepth}");

        var oversizedPage = pageSizePerLevel.FirstOrDefault(size => size > _maxPageSize);
        if (oversizedPage > 0)
            return new GuardVerdict(false, $"page size {oversizedPage} exceeds the limit of {_maxPageSize}");

        var complexity = Complexity(pageSizePerLevel);
        if (complexity > _maxComplexity)
            return new GuardVerdict(false, $"complexity {complexity} exceeds the budget of {_maxComplexity}");

        return new GuardVerdict(true, $"depth {depth}, complexity {complexity} - within budget");
    }
}
