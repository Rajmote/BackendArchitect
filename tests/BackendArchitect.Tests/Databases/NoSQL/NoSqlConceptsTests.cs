using BackendArchitect.Databases.NoSql.Concepts;

namespace BackendArchitect.Tests.Databases.NoSql;

// Databases · NoSQL · Concepts — the trade runs both ways: documents win reads, normalized wins writes.
public class NoSqlConceptsTests
{
    // One customer with `orders` orders, in both shapes.
    private static (RelationalStore Relational, DocumentStore Documents) Seeded(int orders)
    {
        var relational = new RelationalStore();
        var documents = new DocumentStore();
        relational.AddCustomer(1, "Alice");

        for (var orderId = 1; orderId <= orders; orderId++)
        {
            relational.AddOrder(new OrderLine(orderId, 1, "Latte", 3.50m));
            documents.AddOrder(new OrderDocument
            {
                OrderId = orderId,
                CustomerId = 1,
                CustomerName = "Alice", // denormalized copy on every order
                Product = "Latte",
                Price = 3.50m,
            });
        }

        return (relational, documents);
    }

    [Fact]
    public void DocumentStore_ServesTheViewInOneRead_WhileNormalizedNeedsAJoin()
    {
        var (relational, documents) = Seeded(3);

        var fromRelational = relational.GetOrderView(1);
        var fromDocuments = documents.GetOrderView(1);

        Assert.Equal(fromRelational, fromDocuments); // same answer...
        Assert.Equal(2, relational.Reads);           // ...order + customer = the join
        Assert.Equal(1, documents.Reads);            // ...one self-contained document
    }

    [Fact]
    public void Rename_CostsOneWriteWhenNormalized_ButOnePerCopyWhenDenormalized()
    {
        var (relational, documents) = Seeded(10);

        relational.RenameCustomer(1, "Renamed");
        documents.RenameCustomer(1, "Renamed");

        Assert.Equal(1, relational.Writes);   // the fact is stored once
        Assert.Equal(10, documents.Writes);   // one write per duplicated copy
    }

    [Fact]
    public void AfterRename_EveryDocumentAgrees_SoTheViewIsConsistent()
    {
        var (_, documents) = Seeded(5);

        documents.RenameCustomer(1, "Renamed");

        Assert.Equal(1, documents.DistinctNamesFor(1)); // all copies updated -> no anomaly
        Assert.Equal("Renamed", documents.GetOrderView(3).CustomerName);
    }

    [Fact]
    public void PartialUpdate_LeavesTheDocumentStoreDisagreeingWithItself()
    {
        var (_, documents) = Seeded(3);

        // simulate updating only some copies (the classic denormalization bug)
        documents.AddOrder(new OrderDocument
        {
            OrderId = 1, CustomerId = 1, CustomerName = "Renamed", Product = "Latte", Price = 3.50m,
        });

        Assert.Equal(2, documents.DistinctNamesFor(1)); // two "truths" for one fact
    }
}
