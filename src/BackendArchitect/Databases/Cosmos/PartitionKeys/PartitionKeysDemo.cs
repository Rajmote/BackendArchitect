using System.Globalization;

namespace BackendArchitect.Databases.Cosmos.PartitionKeys;

// Example runner: the SAME 1,000 documents stored under a good partition key vs a bad one, then the
// three ways of reading them — showing spread and query cost side by side.
public class PartitionKeysDemo
{
    private const int Orders = 1000;
    private const int Customers = 200;
    private const int PhysicalPartitions = 4;

    public void Run()
    {
        var docs = BuildOrders();

        // GOOD: high-cardinality key that queries actually filter by.
        var byCustomer = new PartitionedContainer(PhysicalPartitions, d => d.CustomerId);
        // BAD: only two possible values, so everything piles into two logical partitions.
        var byStatus = new PartitionedContainer(PhysicalPartitions, d => d.Status);

        foreach (var doc in docs)
        {
            byCustomer.Add(doc);
            byStatus.Add(doc);
        }

        Console.WriteLine($"{Orders} orders, {Customers} customers, {PhysicalPartitions} physical partitions");
        Report("/customerId (high cardinality)", byCustomer);
        Report("/status      (two values)     ", byStatus);

        // Cost of the three access patterns, on the well-partitioned container.
        var sample = docs[0];

        byCustomer.ResetCost();
        byCustomer.PointRead(sample.CustomerId, sample.Id);
        var pointCost = byCustomer.PartitionsTouched;

        byCustomer.ResetCost();
        var mine = byCustomer.QueryWithinPartition(sample.CustomerId);
        var singleCost = byCustomer.PartitionsTouched;

        byCustomer.ResetCost();
        var lattes = byCustomer.CrossPartitionQuery(d => d.Product == "Latte");
        var crossCost = byCustomer.PartitionsTouched;

        Console.WriteLine("Access patterns (partitions touched):");
        Console.WriteLine($"  point read      (pk + id) : {pointCost} -> 1 item");
        Console.WriteLine($"  single-partition (pk)     : {singleCost} -> {mine.Count} items");
        Console.WriteLine($"  cross-partition  (no pk)  : {crossCost} -> {lattes.Count} items  <- fans out");
    }

    private static void Report(string label, PartitionedContainer container)
    {
        var ic = CultureInfo.InvariantCulture;
        var spread = string.Join(", ", container.ItemsPerPhysicalPartition);
        var share = container.LargestLogicalShare.ToString("P0", ic);
        var verdict = container.LargestLogicalShare > 0.25 ? "HOT PARTITION (bad)" : "balanced (good)";

        Console.WriteLine($"  partition key {label}");
        Console.WriteLine($"    items per physical partition : {spread}");
        Console.WriteLine($"    distinct logical partitions  : {container.DistinctLogicalPartitions}");
        Console.WriteLine($"    biggest logical partition    : {container.LargestLogicalPartition} items ({share}) -> {verdict}");
    }

    private static List<OrderDoc> BuildOrders()
    {
        var docs = new List<OrderDoc>(Orders);
        for (var i = 1; i <= Orders; i++)
        {
            var customerId = $"customer-{i % Customers:D3}";
            var status = i % 10 < 7 ? "active" : "closed";       // 70/30 split
            var product = i % 3 == 0 ? "Latte" : "Muffin";
            docs.Add(new OrderDoc($"order-{i:D4}", customerId, status, product));
        }

        return docs;
    }
}
