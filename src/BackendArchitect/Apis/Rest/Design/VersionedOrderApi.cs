namespace BackendArchitect.Apis.Rest.Design;

// One internal model, mapped to per-version representations at the edge — "version the contract, not
// your internal code". v2 renames `phone` to `phoneNumber`, which is BREAKING, so v1 keeps its shape.
public sealed record Customer(int Id, string Name, string Phone, int LoyaltyPoints);

public sealed class VersionedOrderApi
{
    private readonly Customer _customer = new(10, "Alice", "0612345678", LoyaltyPoints: 250);

    /// <summary>v1 representation. Frozen — existing clients depend on these exact field names.</summary>
    public Dictionary<string, object> GetCustomerV1(bool duringMigration = false)
    {
        var body = new Dictionary<string, object>
        {
            ["id"] = _customer.Id,
            ["name"] = _customer.Name,
            ["phone"] = _customer.Phone,
            // ADDITIVE change: new field appears in v1 too, and that is NOT breaking — tolerant
            // readers ignore what they don't know.
            ["loyaltyPoints"] = _customer.LoyaltyPoints,
        };

        // EXPAND phase of expand -> migrate -> contract: serve both names for a transition period so
        // clients can move at their own pace, then remove `phone` once usage reaches zero.
        if (duringMigration)
            body["phoneNumber"] = _customer.Phone;

        return body;
    }

    /// <summary>v2 representation: the renamed field only.</summary>
    public Dictionary<string, object> GetCustomerV2() => new()
    {
        ["id"] = _customer.Id,
        ["name"] = _customer.Name,
        ["phoneNumber"] = _customer.Phone,
        ["loyaltyPoints"] = _customer.LoyaltyPoints,
    };

    /// <summary>Deprecation signalling on v1 (RFC 8594) so a client's own logs can warn them.</summary>
    public static Dictionary<string, string> DeprecationHeadersForV1() => new()
    {
        ["Deprecation"] = "true",
        ["Sunset"] = "Sat, 31 Jan 2027 23:59:59 GMT",
        ["Link"] = "</v2/customers>; rel=\"successor-version\"",
    };
}

// A client that reads only the fields it knows and ignores the rest.
public static class TolerantReader
{
    /// <summary>Returns null when the expected field is absent — which is what a RENAME causes.</summary>
    public static string? ReadPhone(IDictionary<string, object> body, string expectedField) =>
        body.TryGetValue(expectedField, out var value) ? value.ToString() : null;
}
