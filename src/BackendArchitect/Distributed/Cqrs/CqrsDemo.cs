namespace BackendArchitect.Distributed.Cqrs;

// Example runner: what each level costs, the read-your-writes bill, a stalled projector, and the
// per-use-case decision.
public class CqrsDemo
{
    public void Run()
    {
        // --- 1. what each level costs a list page ---
        const int queries = 100;
        var levels = new[]
        {
            ReadModelLevels.LoadTheDomainGraph(queries),
            ReadModelLevels.ProjectToAFlatDto(queries),
            ReadModelLevels.ReadFromAProjection(queries)
        };

        Console.WriteLine($"{queries} order-list page loads:");
        foreach (var level in levels)
            Console.WriteLine($"  {level.Level,-28}: {level.TablesTouched} table(s), {level.RowsScanned,7:N0} rows, " +
                              $"eventually consistent = {level.EventuallyConsistent}");
        Console.WriteLine("  -> level 2 removes 99% of the work with NO staleness; level 3 buys little more and costs a projector");

        // --- 2. the read-your-writes bill ---
        const int writes = 50;
        const int delay = 3;
        var strategies = Enum.GetValues<ReadAfterWriteStrategy>()
            .Select(strategy => OrderProjectionSystem.PlaceThenView(strategy, writes, delay))
            .ToArray();

        Console.WriteLine();
        Console.WriteLine($"Place an order, then immediately show it ({writes} times, projection {delay} behind):");
        foreach (var run in strategies)
            Console.WriteLine($"  {run.Strategy,-26}: {run.MissingImmediatelyAfter,2}/{run.Writes} orders missing, " +
                              $"{run.ProjectionWaits,2} projection wait(s)");
        Console.WriteLine("  -> the write side already HAS the order; querying the read store for it is the mistake");

        // --- 3. a stalled projector is invisible ---
        const int events = 100;
        var healthy = OrderProjectionSystem.ProjectorStalls(stopProjector: false, events);
        var stalled = OrderProjectionSystem.ProjectorStalls(stopProjector: true, events);

        Console.WriteLine();
        Console.WriteLine($"{events} orders written, and the read model's freshness:");
        foreach (var run in new[] { healthy, stalled })
            Console.WriteLine($"  {run.Scenario,-36}: projected {run.EventsProjected,3}/{run.EventsPublished}, " +
                              $"lag {run.Lag,2}, read store healthy = {run.ReadStoreHealthy}");
        Console.WriteLine("  -> every health check stays green: the read store is up, fast, and answering with stale data");

        // --- 4. the level is a per-use-case decision ---
        Console.WriteLine();
        Console.WriteLine("One system, five use cases, five answers:");
        foreach (var verdict in LevelDecision.DecideAll())
            Console.WriteLine($"  {verdict.UseCase,-20} -> level {verdict.Level}  ({verdict.Because})");
        Console.WriteLine("  -> 'we're a CQRS shop' is the same mistake as 'we're an AP shop'");
    }
}
