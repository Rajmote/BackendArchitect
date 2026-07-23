namespace BackendArchitect.Databases.Sql.DataModeling;

// NORMALIZED (3NF): the email is stored ONCE, on the Customer. Orders point to the customer by a
// foreign key, so there is exactly one place to change the email — no update anomaly is possible.
public sealed class Customer
{
    public required int Id { get; init; }          // primary key
    public required string Name { get; init; }
    public required string Email { get; set; }      // the fact, stored once
}

public sealed class Order
{
    public required int OrderId { get; init; }      // primary key
    public required int CustomerId { get; init; }   // foreign key -> Customer.Id
    public required string Product { get; init; }
}
