namespace BackendArchitect.Databases.NoSql.Concepts;

// A self-contained order document: the customer's name is DENORMALIZED onto every order, so the page
// that lists orders never needs a join. "Store together what you read together."
public sealed class OrderDocument
{
    public required int OrderId { get; init; }
    public required int CustomerId { get; init; }
    public required string CustomerName { get; set; } // the duplicated fact
    public required string Product { get; init; }
    public required decimal Price { get; init; }
}

// The document/NoSQL shape: reading an order view is ONE read (no join), but a rename must rewrite
// every document carrying a copy of that name — the denormalization trade, measured.
public sealed class DocumentStore
{
    private readonly Dictionary<int, OrderDocument> _docs = new();

    public int Reads { get; private set; }
    public int Writes { get; private set; }

    public void AddOrder(OrderDocument document) => _docs[document.OrderId] = document;

    // No join: everything the view needs is already inside the one document.
    public (string CustomerName, string Product) GetOrderView(int orderId)
    {
        Reads++;
        var doc = _docs[orderId];
        return (doc.CustomerName, doc.Product);
    }

    // Denormalized: every document holding a copy of this customer's name must be rewritten.
    public void RenameCustomer(int customerId, string newName)
    {
        foreach (var doc in _docs.Values.Where(d => d.CustomerId == customerId))
        {
            Writes++;
            doc.CustomerName = newName;
        }
    }

    // If you miss even one copy, the store now disagrees with itself (the update anomaly).
    public int DistinctNamesFor(int customerId) =>
        _docs.Values.Where(d => d.CustomerId == customerId)
                    .Select(d => d.CustomerName)
                    .Distinct()
                    .Count();
}
