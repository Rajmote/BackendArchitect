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

    private static string Simulate(bool serialized)
    {
        var booth = new TicketBooth(available: 1, serialized: serialized);
        using var release = new ManualResetEventSlim(false);

        var buyers = Enumerable.Range(0, 10).Select(_ => new Thread(() =>
        {
            release.Wait();      // line everyone up...
            booth.TryBook();     // ...then let them all rush the gate at once
        })).ToList();

        foreach (var t in buyers) t.Start();
        release.Set();
        foreach (var t in buyers) t.Join();

        var verdict = booth.Sold > 1 ? "OVERSOLD (bug)" : "correct";
        return $"sold {booth.Sold} of 1 -> {verdict}";
    }
}
