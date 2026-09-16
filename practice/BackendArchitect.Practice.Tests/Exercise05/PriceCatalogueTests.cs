using BackendArchitect.Practice.Exercise05;

namespace BackendArchitect.Practice.Tests.Exercise05;

// Exercise 05 — three starter tests showing the target shape.
//
// YOUR JOB: make these pass, then add tests for:
//   * concurrent publishes — 8 writer threads, Version ends at exactly 8 and nothing is lost
//   * atomic batches       — a reader never sees half a batch applied
//   * deep immutability    — a held snapshot is unchanged by later publishes
//
// 🌟 And VERIFY THE TESTS CAN FAIL: swap ImmutableDictionary for Dictionary and confirm the atomicity
//    test goes red.
public class PriceCatalogueTests
{
    [Fact]
    public void AnUnknownSkuHasNoPrice()
    {
        var catalogue = new PriceCatalogue();

        Assert.Null(catalogue.PriceOf("SKU-404"));
    }

    [Fact]
    public void PublishingMakesThePriceVisible()
    {
        var catalogue = new PriceCatalogue();

        catalogue.Publish([new PriceChange("SKU-1", 9.99m), new PriceChange("SKU-2", 4.50m)]);

        Assert.Equal(9.99m, catalogue.PriceOf("SKU-1"));
        Assert.Equal(4.50m, catalogue.PriceOf("SKU-2"));
    }

    [Fact]
    public void ASnapshotDoesNotChangeWhenNewPricesArePublished()
    {
        var catalogue = new PriceCatalogue();
        catalogue.Publish([new PriceChange("SKU-1", 9.99m)]);

        var held = catalogue.Current;
        catalogue.Publish([new PriceChange("SKU-1", 1.00m)]);

        Assert.Equal(9.99m, held.Prices["SKU-1"]);      // the snapshot is frozen
        Assert.Equal(1.00m, catalogue.PriceOf("SKU-1")); // the catalogue moved on
        Assert.True(catalogue.Version > held.Version);
    }

    // TODO (you): concurrent publishes — 8 Threads + a Barrier, each publishing once.
    //             Version must end at exactly 8 and every SKU must be present.
    //             Hint: Volatile.Write alone loses updates here — it is read-modify-write across two
    //                   fields, which is §4.3's check-then-act all over again.

    // TODO (you): atomic batches — a reader looping over Current while a writer publishes a 50-SKU
    //             batch must see either none of it or all of it, never a partial batch.

    // TODO (you): deep immutability — confirm a caller holding a PriceSnapshot cannot mutate it.
}
