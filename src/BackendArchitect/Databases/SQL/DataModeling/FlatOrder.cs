namespace BackendArchitect.Databases.Sql.DataModeling;

// UNNORMALIZED: the customer's email is copied onto every order row. Because the same fact lives in
// many places, a partial update leaves the data inconsistent — the classic "update anomaly".
public sealed class FlatOrder
{
    public required int OrderId { get; init; }
    public required string CustomerName { get; init; }
    public required string CustomerEmail { get; set; } // the duplicated fact
    public required string Product { get; init; }
}
