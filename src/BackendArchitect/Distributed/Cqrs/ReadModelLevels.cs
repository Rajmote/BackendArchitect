namespace BackendArchitect.Distributed.Cqrs;

public sealed record QueryCostReport(string Level, int Queries, int TablesTouched, int RowsScanned, bool EventuallyConsistent);

// CQRS says one thing: use a different MODEL to write data than you use to read it.
//
// It says nothing about two databases, message buses or event stores - those are implementations
// people chose. It is a SPECTRUM, and most teams should stop at level 2:
//
//   level 0  one model, one database                      plain CRUD, correct for most apps
//   level 1  separate command and query METHODS           free, just discipline
//   level 2  separate read MODELS, same database   <-- the sweet spot
//   level 3  separate read STORE, synchronised            two datastores and eventual consistency
//
// The write side enforces invariants and stays normalized; the read side has no rules to enforce and
// wants the answer pre-shaped. One model asked to do both ends up bad at each.
public static class ReadModelLevels
{
    private const int OrderTables = 6;          // Orders, Customers, Lines, Products, Addresses, Status
    private const int OrdersPerQuery = 50;
    private const int RowsPerJoinedTable = 400;

    /// <summary>Level 0/1: the list page loads the full domain graph to show four columns.</summary>
    public static QueryCostReport LoadTheDomainGraph(int queries) =>
        new("level 0/1 — load aggregates", queries, OrderTables,
            queries * OrderTables * RowsPerJoinedTable, EventuallyConsistent: false);

    /// <summary>
    /// Level 2: a flat DTO queried directly. Same database, same transaction, no projector - and the
    /// join collapses because nothing loads the write model at all.
    /// </summary>
    public static QueryCostReport ProjectToAFlatDto(int queries) =>
        new("level 2 — flat read model", queries, 2,
            queries * OrdersPerQuery, EventuallyConsistent: false);

    /// <summary>
    /// Level 3: a maintained read store. One lookup, and the bill is eventual consistency plus a
    /// projector to operate.
    /// </summary>
    public static QueryCostReport ReadFromAProjection(int queries) =>
        new("level 3 — separate store", queries, 1,
            queries * OrdersPerQuery, EventuallyConsistent: true);
}
