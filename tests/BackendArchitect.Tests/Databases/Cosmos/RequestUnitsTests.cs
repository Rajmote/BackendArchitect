using BackendArchitect.Databases.Cosmos.RequestUnits;

namespace BackendArchitect.Tests.Databases.Cosmos;

// Databases · Cosmos · Request Units — the cost ratios, and what happens at the budget limit.
public class RequestUnitsTests
{
    [Fact]
    public void PointRead_OfOneKilobyte_IsTheOneRuAnchor()
    {
        Assert.Equal(1.0, RuCost.PointRead(1.0), precision: 3);
    }

    [Fact]
    public void Write_CostsSeveralTimesAPointRead()
    {
        var read = RuCost.PointRead(1.0);
        var write = RuCost.Write(1.0, indexedProperties: 0);

        Assert.True(write >= read * 5, $"a write should cost ~5x a read; was {write} vs {read}");
    }

    [Fact]
    public void Write_GetsCheaper_WhenFewerPropertiesAreIndexed()
    {
        var fullyIndexed = RuCost.Write(1.0, indexedProperties: 8);
        var trimmed = RuCost.Write(1.0, indexedProperties: 2);

        Assert.True(trimmed < fullyIndexed);
        Assert.Equal(3.0, fullyIndexed - trimmed, precision: 3); // 6 properties x 0.5 RU
    }

    [Fact]
    public void Query_IsPricedByWorkDone_NotByResultsReturned()
    {
        // Both return one document, but one had to examine 1000 across 4 partitions.
        var cheap = RuCost.Query(documentsExamined: 1, partitionsTouched: 1);
        var expensive = RuCost.Query(documentsExamined: 1000, partitionsTouched: 4);

        Assert.True(expensive > cheap * 10,
            $"scanning a lot to find a little must cost far more; {expensive} vs {cheap}");
    }

    [Fact]
    public void Budget_Throttles_WhenTheWorkloadExceedsProvisionedThroughput()
    {
        var budget = new ThroughputBudget(ruPerSecond: 400);
        var charge = RuCost.Write(1.0, indexedProperties: 8); // 9 RU each

        for (var i = 0; i < 60; i++)
            budget.TryConsume(charge);

        Assert.Equal(16, budget.Throttled);            // only 44 of 60 fit in 400 RU
        Assert.Equal(396.0, budget.Consumed, precision: 3);
    }

    [Fact]
    public void Budget_StopsThrottling_WhenEachOperationCostsLess()
    {
        var budget = new ThroughputBudget(ruPerSecond: 400);
        var charge = RuCost.Write(1.0, indexedProperties: 2); // 6 RU each

        for (var i = 0; i < 60; i++)
            budget.TryConsume(charge);

        Assert.Equal(0, budget.Throttled);             // 60 x 6 = 360 RU, fits
        Assert.Equal(360.0, budget.Consumed, precision: 3);
    }

    [Fact]
    public void Budget_RefillsEachSecond()
    {
        var budget = new ThroughputBudget(ruPerSecond: 10);

        Assert.True(budget.TryConsume(10));
        Assert.False(budget.TryConsume(1));   // exhausted this second
        budget.NextSecond();
        Assert.True(budget.TryConsume(10));   // fresh budget

        Assert.Equal(1, budget.Throttled);
    }
}
