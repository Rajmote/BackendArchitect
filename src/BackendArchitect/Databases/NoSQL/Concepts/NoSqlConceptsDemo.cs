namespace BackendArchitect.Databases.NoSql.Concepts;

// Example runner: the same two operations on a normalized (SQL-shaped) store and a document
// (NoSQL-shaped) store — showing the trade runs in BOTH directions.
//   * listing orders with the customer's name -> the document store wins (no join)
//   * renaming a customer                     -> the normalized store wins (the fact lives once)
public class NoSqlConceptsDemo
{
    private const int CustomerCount = 10;
    private const int OrdersEach = 10;

    public void Run()
    {
        var relational = new RelationalStore();
        var documents = new DocumentStore();

        var orderId = 0;
        for (var customerId = 1; customerId <= CustomerCount; customerId++)
        {
            relational.AddCustomer(customerId, $"Customer{customerId}");
            for (var i = 0; i < OrdersEach; i++)
            {
                orderId++;
                relational.AddOrder(new OrderLine(orderId, customerId, "Latte", 3.50m));
                documents.AddOrder(new OrderDocument
                {
                    OrderId = orderId,
                    CustomerId = customerId,
                    CustomerName = $"Customer{customerId}", // denormalized copy
                    Product = "Latte",
                    Price = 3.50m,
                });
            }
        }

        var totalOrders = CustomerCount * OrdersEach;

        // READ: list every order with the customer's name.
        for (var id = 1; id <= totalOrders; id++)
        {
            relational.GetOrderView(id);
            documents.GetOrderView(id);
        }

        Console.WriteLine($"List {totalOrders} orders with the customer's name:");
        Console.WriteLine($"  Normalized (join)   : {relational.Reads} reads");
        Console.WriteLine($"  Document (embedded) : {documents.Reads} reads   <- no join, half the work");

        // WRITE: one customer (who has 10 orders) changes their name.
        relational.RenameCustomer(1, "Renamed");
        documents.RenameCustomer(1, "Renamed");

        Console.WriteLine("Rename one customer who has 10 orders:");
        Console.WriteLine($"  Normalized          : {relational.Writes} write   <- the fact lives once");
        Console.WriteLine($"  Document            : {documents.Writes} writes  <- every copy must be updated");
        Console.WriteLine($"  (miss one copy and the store disagrees with itself: the update anomaly)");
    }
}
