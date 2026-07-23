using BackendArchitect.Databases.Sql.DataModeling;

namespace BackendArchitect.Tests.Databases.Sql;

// Databases · SQL · Data modeling — the update anomaly (flat) vs a single source of truth (normalized).
public class DataModelingTests
{
    [Fact]
    public void FlatModel_PartialUpdate_CanBecomeInconsistent()
    {
        var flat = new List<FlatOrder>
        {
            new() { OrderId = 1, CustomerName = "Alice", CustomerEmail = "alice@x.com", Product = "Latte" },
            new() { OrderId = 2, CustomerName = "Alice", CustomerEmail = "alice@x.com", Product = "Muffin" },
        };

        flat[0].CustomerEmail = "alice@new.com"; // only one of the two rows

        var distinctEmails = flat.Where(o => o.CustomerName == "Alice")
                                 .Select(o => o.CustomerEmail).Distinct().Count();
        Assert.Equal(2, distinctEmails); // two "truths" for one fact — the update anomaly
    }

    [Fact]
    public void NormalizedModel_SingleUpdate_KeepsEveryOrderConsistent()
    {
        var customers = new Dictionary<int, Customer>
        {
            [10] = new() { Id = 10, Name = "Alice", Email = "alice@x.com" },
        };
        var orders = new List<Order>
        {
            new() { OrderId = 1, CustomerId = 10, Product = "Latte" },
            new() { OrderId = 2, CustomerId = 10, Product = "Muffin" },
        };

        customers[10].Email = "alice@new.com"; // one place

        var distinctEmails = orders.Select(o => customers[o.CustomerId].Email).Distinct().ToList();
        Assert.Single(distinctEmails);
        Assert.Equal("alice@new.com", distinctEmails[0]);
    }
}
