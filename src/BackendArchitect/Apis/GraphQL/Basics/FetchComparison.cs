namespace BackendArchitect.Apis.GraphQL.Basics;

/// <summary>A product as the server stores it: 25 fields, of which a list screen needs two.</summary>
public sealed record Product(int Id, string Name, string Thumbnail, string Description);

// Compares what REST and GraphQL actually transfer, and how many round trips each needs — the two
// problems GraphQL exists to solve:
//
//   OVER-fetching  — each response carries MORE than the client needs   -> wasted bytes
//   UNDER-fetching — each response carries LESS than the client needs   -> wasted ROUND TRIPS
//
// Round trips matter far more than bytes: each one pays full network latency (100-300 ms on mobile),
// and a waterfall is sequential, so the delays add up.
public static class FetchComparison
{
    public const int FieldsStoredPerProduct = 25;
    public const int BytesPerField = 20;
    public const int MobileLatencyMs = 200;

    /// <summary>REST returns the whole representation — the server decides the shape.</summary>
    public static int RestBytes(int products) => products * FieldsStoredPerProduct * BytesPerField;

    /// <summary>GraphQL transfers only the fields the client asked for.</summary>
    public static int GraphQlBytes(int products, int requestedFields) => products * requestedFields * BytesPerField;

    /// <summary>
    /// The waterfall: 1 request for the user, 1 for their orders, then 1 per order for its items.
    /// Each step depends on the previous one, so they cannot be parallelised.
    /// </summary>
    public static int RestRoundTrips(int orders) => 2 + orders;

    /// <summary>GraphQL resolves the whole tree server-side: one request.</summary>
    public static int GraphQlRoundTrips() => 1;

    public static int LatencyMs(int roundTrips) => roundTrips * MobileLatencyMs;
}
