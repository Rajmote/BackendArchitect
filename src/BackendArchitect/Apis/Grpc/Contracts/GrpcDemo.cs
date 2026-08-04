using System.Globalization;

namespace BackendArchitect.Apis.Grpc.Contracts;

// Example runner: why renaming a protobuf field is free, why reusing a number is not, the wire-size
// difference against JSON, and how a propagated deadline stops zombie work.
public class GrpcDemo
{
    public void Run()
    {
        var ic = CultureInfo.InvariantCulture;

        // v1 contract
        var v1 = new ProtoSchema("Order",
        [
            new ProtoField(1, "id", ProtoType.Int32),
            new ProtoField(2, "customer", ProtoType.String),
            new ProtoField(3, "total", ProtoType.Double),
        ]);

        // v2: field 2 RENAMED (same number), field 4 ADDED.
        var v2 = new ProtoSchema("Order",
        [
            new ProtoField(1, "id", ProtoType.Int32),
            new ProtoField(2, "customerName", ProtoType.String),   // renamed only
            new ProtoField(3, "total", ProtoType.Double),
            new ProtoField(4, "currency", ProtoType.String),       // added
        ]);

        var wire = ProtoCodec.Serialize(v2, new Dictionary<string, object>
        {
            ["id"] = 5,
            ["customerName"] = "Alice",
            ["total"] = 9.75,
            ["currency"] = "EUR",
        });

        Console.WriteLine("A v2 server responds; a v1 client decodes it:");
        var asV1 = ProtoCodec.Deserialize(v1, wire);
        foreach (var (name, value) in asV1.OrderBy(p => p.Key, StringComparer.Ordinal))
            Console.WriteLine($"    {name,-14}= {ProtoCodec.FormatValue(value)}");
        Console.WriteLine("  -> 'customer' still resolves (field 2), 'currency' (field 4) is ignored");
        Console.WriteLine("  -> renaming is FREE: names never travel, only numbers do");

        // Wire size vs JSON
        var json = ProtoCodec.ToJson(v2, wire);
        var protoBytes = wire.ApproximateBytes();
        Console.WriteLine();
        Console.WriteLine($"Payload size: protobuf ~{protoBytes} bytes vs JSON {json.Length} bytes " +
                          $"({100 - protoBytes * 100 / json.Length}% smaller)");
        Console.WriteLine($"  JSON: {json}");

        // Reusing a retired number
        Console.WriteLine();
        Console.WriteLine("Retiring field 3 and reserving its number:");
        try
        {
            _ = new ProtoSchema("Order",
                [new ProtoField(1, "id", ProtoType.Int32), new ProtoField(3, "discount", ProtoType.Int32)],
                reserved: [3]);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"  compiler refuses: {ex.Message}");
            Console.WriteLine("  -> without 'reserved', an old client would decode the new field with the OLD meaning");
        }

        // Deadline propagation
        Console.WriteLine();
        Console.WriteLine("A 2s deadline propagating through A -> B -> C (each takes 0.9s):");
        foreach (var hop in CallChain.Propagate(2.0, ("A", 0.9), ("B", 0.9), ("C", 0.9)))
            Console.WriteLine($"  {hop.Service}: arrived with {hop.BudgetOnArrival.ToString("0.0", ic)}s, " +
                              $"worked={hop.DidWork,-5} {hop.Outcome}");
        Console.WriteLine($"  independent 2s timeouts would allow up to " +
                          $"{CallChain.WorstCaseWithIndependentTimeouts(2.0, 3).ToString("0.0", ic)}s total");
    }
}
