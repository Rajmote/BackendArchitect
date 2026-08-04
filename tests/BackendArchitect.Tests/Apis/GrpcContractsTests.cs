using BackendArchitect.Apis.Grpc.Contracts;

namespace BackendArchitect.Tests.Apis;

// APIs · gRPC · Contracts — protobuf compatibility rules and deadline propagation.
public class GrpcContractsTests
{
    private static ProtoSchema V1() => new("Order",
    [
        new ProtoField(1, "id", ProtoType.Int32),
        new ProtoField(2, "customer", ProtoType.String),
        new ProtoField(3, "total", ProtoType.Double),
    ]);

    // field 2 renamed (same number), field 4 added
    private static ProtoSchema V2() => new("Order",
    [
        new ProtoField(1, "id", ProtoType.Int32),
        new ProtoField(2, "customerName", ProtoType.String),
        new ProtoField(3, "total", ProtoType.Double),
        new ProtoField(4, "currency", ProtoType.String),
    ]);

    private static WireMessage V2Payload() => ProtoCodec.Serialize(V2(), new Dictionary<string, object>
    {
        ["id"] = 5,
        ["customerName"] = "Alice",
        ["total"] = 9.75,
        ["currency"] = "EUR",
    });

    [Fact]
    public void RenamingAField_IsFree_BecauseNamesNeverTravel()
    {
        var decoded = ProtoCodec.Deserialize(V1(), V2Payload());

        // the v1 client still gets the value — under ITS name, from field number 2
        Assert.Equal("Alice", decoded["customer"]);
    }

    [Fact]
    public void AddingAField_IsIgnoredByOlderClients()
    {
        var decoded = ProtoCodec.Deserialize(V1(), V2Payload());

        Assert.False(decoded.ContainsKey("currency"));  // unknown number 4 -> skipped, not an error
        Assert.Equal(3, decoded.Count);
    }

    [Fact]
    public void OnlyFieldNumbersTravel_NotNames()
    {
        var wire = V2Payload();

        Assert.Equal(new[] { 1, 2, 3, 4 }, wire.Values.Keys.OrderBy(n => n));
        Assert.Equal("Alice", wire.Values[2]);
    }

    [Fact]
    public void ChangingAFieldNumber_BreaksOlderClients()
    {
        // v3 moves 'customer' from number 2 to number 5 — the value no longer lands where v1 looks.
        var v3 = new ProtoSchema("Order", [new ProtoField(5, "customer", ProtoType.String)]);
        var wire = ProtoCodec.Serialize(v3, new Dictionary<string, object> { ["customer"] = "Alice" });

        var decoded = ProtoCodec.Deserialize(V1(), wire);

        Assert.False(decoded.ContainsKey("customer"));  // silently missing
    }

    [Fact]
    public void ReservedNumbers_CannotBeReused()
    {
        var reuse = () => new ProtoSchema("Order",
            [new ProtoField(3, "discount", ProtoType.Int32)],
            reserved: [3]);

        var ex = Assert.Throws<InvalidOperationException>(reuse);
        Assert.Contains("reserved", ex.Message);
    }

    [Fact]
    public void ProtobufPayload_IsSmallerThanTheEquivalentJson()
    {
        var wire = V2Payload();

        var protoBytes = wire.ApproximateBytes();
        var jsonBytes = ProtoCodec.ToJson(V2(), wire).Length;

        Assert.True(protoBytes < jsonBytes, $"protobuf {protoBytes} should be smaller than JSON {jsonBytes}");
    }

    [Fact]
    public void Deadline_ShrinksAsItPropagatesDownTheChain()
    {
        var hops = CallChain.Propagate(2.0, ("A", 0.5), ("B", 0.5), ("C", 0.5));

        Assert.Equal(2.0, hops[0].BudgetOnArrival, precision: 3);
        Assert.Equal(1.5, hops[1].BudgetOnArrival, precision: 3);
        Assert.Equal(1.0, hops[2].BudgetOnArrival, precision: 3);
        Assert.All(hops, hop => Assert.True(hop.DidWork));
    }

    [Fact]
    public void WhenTheBudgetRunsOut_DownstreamServicesSkipTheWork() // no zombie work
    {
        var hops = CallChain.Propagate(2.0, ("A", 1.2), ("B", 1.2), ("C", 1.2));

        Assert.True(hops[0].DidWork);
        Assert.True(hops[1].DidWork);            // starts with 0.8s left, overruns
        Assert.False(hops[2].DidWork);           // budget gone -> never starts
        Assert.Contains("no zombie work", hops[2].Outcome);
    }

    [Fact]
    public void PropagatedDeadline_BoundsTotalLatency_UnlikeIndependentTimeouts()
    {
        var worstCase = CallChain.WorstCaseWithIndependentTimeouts(perServiceTimeout: 2.0, serviceCount: 3);

        Assert.Equal(6.0, worstCase, precision: 3);   // 2s + 2s + 2s
        // ...whereas one propagated 2s deadline bounds the whole chain at 2s.
    }

    [Fact]
    public void CanAfford_LetsAServiceFailFastInsteadOfStarting()
    {
        var budget = new DeadlineBudget(0.1);

        Assert.False(budget.CanAfford(0.5));   // don't start work we can't finish
        Assert.True(budget.CanAfford(0.05));
    }
}
