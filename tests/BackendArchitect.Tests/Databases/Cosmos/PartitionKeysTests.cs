using BackendArchitect.Databases.Cosmos.PartitionKeys;

namespace BackendArchitect.Tests.Databases.Cosmos;

// Databases · Cosmos · Partition keys — spread and query cost, measured.
public class PartitionKeysTests
{
    private const int PhysicalPartitions = 4;

    private static List<OrderDoc> Orders(int count, int customers)
    {
        var docs = new List<OrderDoc>(count);
        for (var i = 1; i <= count; i++)
        {
            docs.Add(new OrderDoc(
                Id: $"order-{i:D4}",
                CustomerId: $"customer-{i % customers:D3}",
                Status: i % 10 < 7 ? "active" : "closed",
                Product: i % 3 == 0 ? "Latte" : "Muffin"));
        }

        return docs;
    }

    private static PartitionedContainer Fill(Func<OrderDoc, string> key, int count = 1000, int customers = 200)
    {
        var container = new PartitionedContainer(PhysicalPartitions, key);
        foreach (var doc in Orders(count, customers))
            container.Add(doc);
        return container;
    }

    [Fact]
    public void HighCardinalityKey_SpreadsDataEvenly()
    {
        var container = Fill(d => d.CustomerId);

        Assert.Equal(200, container.DistinctLogicalPartitions);
        Assert.All(container.ItemsPerPhysicalPartition, count => Assert.True(count > 0, "every machine should hold data"));
        Assert.True(container.LargestLogicalShare < 0.05,
            $"no single customer should dominate; was {container.LargestLogicalShare:P1}");
    }

    [Fact]
    public void LowCardinalityKey_CreatesAHotPartition()
    {
        var container = Fill(d => d.Status); // only "active" / "closed"

        Assert.Equal(2, container.DistinctLogicalPartitions);
        Assert.True(container.LargestLogicalShare > 0.5,
            $"'active' should swallow most of the data; was {container.LargestLogicalShare:P1}");
        // With two values hashed across four machines, at least two machines sit idle.
        Assert.Contains(0, container.ItemsPerPhysicalPartition);
    }

    [Fact]
    public void PointRead_TouchesOnePartition_AndReturnsExactlyOneItem()
    {
        var container = Fill(d => d.CustomerId);
        container.ResetCost();

        var found = container.PointRead("customer-001", "order-0001");

        Assert.NotNull(found);
        Assert.Equal("order-0001", found!.Id);
        Assert.Equal(1, container.PartitionsTouched);
    }

    [Fact]
    public void SinglePartitionQuery_TouchesOnePartition_AndReturnsAList()
    {
        var container = Fill(d => d.CustomerId);
        container.ResetCost();

        var orders = container.QueryWithinPartition("customer-001");

        Assert.Equal(5, orders.Count);                       // 1000 orders / 200 customers
        Assert.All(orders, o => Assert.Equal("customer-001", o.CustomerId));
        Assert.Equal(1, container.PartitionsTouched);        // cheap despite returning many items
    }

    [Fact]
    public void CrossPartitionQuery_TouchesEveryPartition()
    {
        var container = Fill(d => d.CustomerId);
        container.ResetCost();

        var lattes = container.CrossPartitionQuery(d => d.Product == "Latte");

        Assert.NotEmpty(lattes);
        Assert.Equal(PhysicalPartitions, container.PartitionsTouched); // the fan-out
    }

    [Fact]
    public void PointRead_ReturnsNull_WhenIdIsNotInThatPartition()
    {
        var container = Fill(d => d.CustomerId);

        // order-0001 belongs to customer-001, not customer-002 — the id alone is not enough.
        Assert.Null(container.PointRead("customer-002", "order-0001"));
    }
}
