namespace BackendArchitect.Databases.NoSql.Concepts;

public record OrderLine(int OrderId, int CustomerId, string Product, decimal Price);

// The SQL/normalized shape: the customer's name is stored ONCE; orders reference the customer by id.
// Showing "an order with the customer's name" therefore needs TWO lookups (the join), but renaming a
// customer needs only ONE write.
//
// Reads/Writes count how many stored items were touched — the same "how much work did it do?"
// instinct as logical reads in the SQL lessons.
public sealed class RelationalStore
{
    private readonly Dictionary<int, string> _customers = new();   // id -> name (the single copy)
    private readonly Dictionary<int, OrderLine> _orders = new();    // orderId -> order

    public int Reads { get; private set; }
    public int Writes { get; private set; }

    public void AddCustomer(int customerId, string name) => _customers[customerId] = name;
    public void AddOrder(OrderLine order) => _orders[order.OrderId] = order;

    // The JOIN: read the order, then read the customer it points at.
    public (string CustomerName, string Product) GetOrderView(int orderId)
    {
        Reads++;                                  // the order row
        var order = _orders[orderId];
        Reads++;                                  // the customer row it references
        var name = _customers[order.CustomerId];
        return (name, order.Product);
    }

    // Normalized: the name lives in exactly one place, so a rename is one write.
    public void RenameCustomer(int customerId, string newName)
    {
        Writes++;
        _customers[customerId] = newName;
    }
}
