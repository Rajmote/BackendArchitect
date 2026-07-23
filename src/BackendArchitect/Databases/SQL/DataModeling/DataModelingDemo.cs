namespace BackendArchitect.Databases.Sql.DataModeling;

// Example runner: the same "change Alice's email" operation on a flat (unnormalized) model vs a
// normalized one — showing how duplication lets the flat model go inconsistent.
public class DataModelingDemo
{
    public void Run()
    {
        // FLAT: Alice's email is duplicated across her two order rows.
        var flat = new List<FlatOrder>
        {
            new() { OrderId = 1, CustomerName = "Alice", CustomerEmail = "alice@x.com", Product = "Latte" },
            new() { OrderId = 2, CustomerName = "Alice", CustomerEmail = "alice@x.com", Product = "Muffin" },
        };
        flat[0].CustomerEmail = "alice@new.com"; // buggy PARTIAL update: only one row changed

        var flatEmails = flat.Where(o => o.CustomerName == "Alice")
                             .Select(o => o.CustomerEmail).Distinct().Count();
        Console.WriteLine($"Flat model : after partial update, Alice has {flatEmails} emails -> " +
                          $"{(flatEmails > 1 ? "INCONSISTENT (update anomaly)" : "ok")}");

        // NORMALIZED: email lives once, on the Customer.
        var customers = new Dictionary<int, Customer>
        {
            [10] = new() { Id = 10, Name = "Alice", Email = "alice@x.com" },
        };
        var orders = new List<Order>
        {
            new() { OrderId = 1, CustomerId = 10, Product = "Latte" },
            new() { OrderId = 2, CustomerId = 10, Product = "Muffin" },
        };
        customers[10].Email = "alice@new.com"; // ONE update

        var normalizedEmails = orders.Select(o => customers[o.CustomerId].Email).Distinct().Count();
        Console.WriteLine($"Normalized : after one update, every order resolves to {normalizedEmails} email -> consistent");
    }
}
