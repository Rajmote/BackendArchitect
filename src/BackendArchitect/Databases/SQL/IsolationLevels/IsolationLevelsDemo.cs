namespace BackendArchitect.Databases.Sql.IsolationLevels;

// Example runner: 1 ticket, 10 buyers at the same instant — with the isolation dial OFF vs ON.
public class IsolationLevelsDemo
{
    public void Run()
    {
        Console.WriteLine("Scenario: 1 ticket left, 10 buyers click 'book' at the same instant.");
        Console.WriteLine($"  Weak isolation (no lock)  -> {Simulate(serialized: false)}");
        Console.WriteLine($"  Serializable (locked)     -> {Simulate(serialized: true)}");
    }

    private const int Buyers = 10;

    private static string Simulate(bool serialized)
    {
        // Without isolation, force the worst case a loose level permits: every buyer reads availability
        // BEFORE any of them writes. The barrier holds all buyers in the read->write gap until all have
        // passed the "is a ticket available?" check. With a lock (serialized), the lock is held across
        // that gap, so the interleaving can't happen — hence no barrier is needed there.
        using var gate = serialized ? null : new Barrier(Buyers);
        var booth = new TicketBooth(available: 1, serialized: serialized, gap: () => gate?.SignalAndWait());

        var buyers = Enumerable.Range(0, Buyers)
            .Select(_ => new Thread(() => booth.TryBook()))
            .ToList();

        foreach (var t in buyers) t.Start();
        foreach (var t in buyers) t.Join();

        var verdict = booth.Sold > 1 ? "OVERSOLD (bug)" : "correct";
        return $"sold {booth.Sold} of 1 -> {verdict}";
    }
}
