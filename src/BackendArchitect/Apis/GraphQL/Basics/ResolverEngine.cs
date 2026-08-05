namespace BackendArchitect.Apis.GraphQL.Basics;

// The N+1 problem and its fix.
//
// Each GraphQL resolver runs independently and cannot see its siblings, so a field like `customer` on
// a list of orders fires once PER ORDER: 1 query for the orders + N for the customers = N+1.
//
// The fix is BATCHING (DataLoader — built into Hot Chocolate in .NET): collect the ids requested within
// one tick and issue a single `WHERE Id IN (...)`, turning N+1 into 2.
public sealed class ResolverEngine
{
    /// <summary>Database queries issued — the cost signal, like logical reads or RU.</summary>
    public int DatabaseQueries { get; private set; }

    public void Reset() => DatabaseQueries = 0;

    /// <summary>Naive resolvers: one query for the parents, then one per child. </summary>
    public int ResolveOrdersWithCustomers_Naive(int orderCount)
    {
        DatabaseQueries++;                       // 1: fetch the orders
        for (var i = 0; i < orderCount; i++)
            DatabaseQueries++;                   // N: one customer lookup per order

        return DatabaseQueries;
    }

    /// <summary>Batched resolvers: one query for the parents, one for ALL the children together.</summary>
    public int ResolveOrdersWithCustomers_Batched(int orderCount)
    {
        DatabaseQueries++;                       // 1: fetch the orders
        if (orderCount > 0)
            DatabaseQueries++;                   // 1: SELECT ... WHERE Id IN (...)

        return DatabaseQueries;
    }

    /// <summary>Nesting makes it multiplicative: orders -> items -> product.</summary>
    public int ResolveNested_Naive(int orders, int itemsPerOrder)
    {
        DatabaseQueries++;                                 // orders
        DatabaseQueries += orders;                         // items per order
        DatabaseQueries += orders * itemsPerOrder;         // product per item
        return DatabaseQueries;
    }
}
