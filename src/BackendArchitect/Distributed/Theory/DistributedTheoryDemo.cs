namespace BackendArchitect.Distributed.Theory;

// Example runner: the third outcome of a remote call, CAP during a 3/2 split, and what "eventually
// consistent" actually promises.
public class DistributedTheoryDemo
{
    public void Run()
    {
        // --- 1. the outcome you cannot observe ---
        const int orders = 100;
        const int everyNth = 10;

        var plain = RemoteCallOutcomes.RunWithRetries(useIdempotencyKey: false, orders, everyNth);
        var keyed = RemoteCallOutcomes.RunWithRetries(useIdempotencyKey: true, orders, everyNth);

        Console.WriteLine($"{orders} payments, 1 in {everyNth} loses its RESPONSE after the charge succeeded:");
        foreach (var run in new[] { plain, keyed })
            Console.WriteLine($"  {run.Strategy,-22}: {run.CallsMade,3} calls -> {run.ChargesApplied,3} charges, " +
                              $"{run.DoubleCharged,2} customer(s) charged twice");
        Console.WriteLine("  -> the client cannot tell 'never arrived' from 'done, reply lost'; the key makes it stop mattering");

        // --- 2. CAP during a partition ---
        const int keys = 10;
        var cp = QuorumCluster.RunSplitBrain(PartitionBehaviour.ConsistentAndPartitionTolerant, keys);
        var ap = QuorumCluster.RunSplitBrain(PartitionBehaviour.AvailableAndPartitionTolerant, keys);

        Console.WriteLine();
        Console.WriteLine($"5 nodes split 3|2, both sides alive, {keys} keys written on each side:");
        foreach (var run in new[] { cp, ap })
            Console.WriteLine($"  {run.Behaviour,-14}: {run.WritesAccepted,2} accepted, {run.WritesRefused,2} refused, " +
                              $"{run.DivergentKeysAfterHeal,2} key(s) disagree once the split heals");
        Console.WriteLine("  -> P is not a choice; the only choice is C or A, during the partition, per operation");

        // --- 3. read-your-writes ---
        const int writes = 50;
        const int delay = 3;

        var fromReplica = ReplicatedStore.MeasureReadAfterWrite(ReadStrategy.FromReplica, writes, delay);
        var yourWrites = ReplicatedStore.MeasureReadAfterWrite(ReadStrategy.ReadYourWrites, writes, delay);

        Console.WriteLine();
        Console.WriteLine($"Write an order, then immediately read it back ({writes} times, replica {delay} behind):");
        foreach (var run in new[] { fromReplica, yourWrites })
            Console.WriteLine($"  {run.Strategy,-18}: {run.StaleReads,2} of {run.Reads} reads returned null for an order that EXISTS");
        Console.WriteLine("  -> 'create then read' is the commonest eventual-consistency bug in production");

        // --- 4. what "eventually" actually promises ---
        var stopped = ReplicatedStore.MeasureConvergence(writesEverStop: true, writes, delay);
        var ongoing = ReplicatedStore.MeasureConvergence(writesEverStop: false, writes, delay);

        Console.WriteLine();
        Console.WriteLine("The guarantee is: IF WRITES STOP, the replicas converge.");
        foreach (var run in new[] { stopped, ongoing })
            Console.WriteLine($"  {run.Scenario,-18}: lag {run.LagAtEnd}, converged = {run.Converged}");
        Console.WriteLine("  -> there is no time bound in the definition, and live systems never stop writing");
    }
}
