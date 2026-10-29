using BackendArchitect.Distributed.Cqrs;

namespace BackendArchitect.Tests.Distributed;

public class CqrsTests
{
    [Fact]
    public void Level_two_removes_most_of_the_work_without_adding_staleness()
    {
        var graph = ReadModelLevels.LoadTheDomainGraph(queries: 100);
        var dto = ReadModelLevels.ProjectToAFlatDto(queries: 100);

        Assert.True(dto.RowsScanned < graph.RowsScanned / 10);
        Assert.False(dto.EventuallyConsistent);
    }

    [Fact]
    public void Level_three_adds_eventual_consistency_for_little_extra_speed()
    {
        var dto = ReadModelLevels.ProjectToAFlatDto(queries: 100);
        var projection = ReadModelLevels.ReadFromAProjection(queries: 100);

        Assert.Equal(dto.RowsScanned, projection.RowsScanned);   // the same rows, in a different store
        Assert.True(projection.EventuallyConsistent);
    }

    [Fact]
    public void Querying_the_read_store_loses_the_order_the_user_just_placed()
    {
        var run = OrderProjectionSystem.PlaceThenView(ReadAfterWriteStrategy.QueryTheReadStore, writes: 50, projectionDelay: 3);

        Assert.True(run.MissingImmediatelyAfter > 0, "the projection has not caught up yet");
    }

    [Fact]
    public void Returning_the_result_from_the_command_costs_nothing_and_fixes_it()
    {
        var run = OrderProjectionSystem.PlaceThenView(ReadAfterWriteStrategy.ReturnFromTheCommand, writes: 50, projectionDelay: 3);

        Assert.Equal(0, run.MissingImmediatelyAfter);
        Assert.Equal(0, run.ProjectionWaits);          // no waiting, no second query
    }

    [Fact]
    public void Waiting_for_the_projection_is_correct_and_costs_latency()
    {
        var run = OrderProjectionSystem.PlaceThenView(ReadAfterWriteStrategy.WaitForTheProjection, writes: 50, projectionDelay: 3);

        Assert.Equal(0, run.MissingImmediatelyAfter);
        Assert.True(run.ProjectionWaits > 0, "it had to block until the read model caught up");
    }

    [Fact]
    public void A_synchronous_projection_is_consistent_and_gives_back_the_independence()
    {
        var run = OrderProjectionSystem.PlaceThenView(ReadAfterWriteStrategy.SynchronousProjection, writes: 50, projectionDelay: 3);

        Assert.Equal(0, run.MissingImmediatelyAfter);
        Assert.Equal(0, run.ProjectionWaits);
    }

    [Fact]
    public void A_stalled_projector_leaves_the_read_store_healthy_and_wrong()
    {
        var run = OrderProjectionSystem.ProjectorStalls(stopProjector: true, events: 100);

        Assert.True(run.Lag > 0);
        Assert.True(run.EventsProjected < run.EventsPublished);
        Assert.True(run.ReadStoreHealthy, "every health check stays green - that is the problem");
    }

    [Fact]
    public void A_healthy_projector_keeps_the_read_model_current()
    {
        var run = OrderProjectionSystem.ProjectorStalls(stopProjector: false, events: 100);

        Assert.Equal(0, run.Lag);
        Assert.Equal(100, run.EventsProjected);
    }

    [Fact]
    public void A_merely_slow_query_does_not_justify_a_second_datastore()
    {
        var verdict = LevelDecision.Decide(new UseCase("order list", NeedsDifferentTechnology: false,
            StalenessAcceptable: false, ReadToWriteRatio: 40, QueryIsMerelySlow: true));

        Assert.Equal(2, verdict.Level);
        Assert.Contains("index", verdict.Because);
    }

    [Fact]
    public void Full_text_search_justifies_level_three()
    {
        var verdict = LevelDecision.Decide(new UseCase("full-text search", NeedsDifferentTechnology: true,
            StalenessAcceptable: true, ReadToWriteRatio: 500, QueryIsMerelySlow: false));

        Assert.Equal(3, verdict.Level);
    }

    [Fact]
    public void A_different_store_without_tolerable_staleness_stays_at_level_two()
    {
        // An account balance cannot be stale, so the read side has to stay with the write store
        // however much a different technology would suit it.
        var verdict = LevelDecision.Decide(new UseCase("account balance", NeedsDifferentTechnology: true,
            StalenessAcceptable: false, ReadToWriteRatio: 200, QueryIsMerelySlow: false));

        Assert.Equal(2, verdict.Level);
    }

    [Fact]
    public void One_system_gets_different_levels_for_different_use_cases()
    {
        var verdicts = LevelDecision.DecideAll();

        // CQRS is applied per use case, not per system.
        Assert.Contains(verdicts, v => v.Level == 2);
        Assert.Contains(verdicts, v => v.Level == 3);
        Assert.Equal(3, verdicts.Single(v => v.UseCase == "full-text search").Level);
        Assert.Equal(2, verdicts.Single(v => v.UseCase == "order list page").Level);
    }
}
