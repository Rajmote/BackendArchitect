namespace BackendArchitect.Databases.Sql.Transactions;

// A small end-to-end tour of all four ACID properties on the Bank model — one method per letter.
public class AcidDemo
{
    private static Bank NewBank() =>
        new(new Dictionary<string, decimal> { ["Alice"] = 100m, ["Bob"] = 0m });

    public void Run()
    {
        Atomicity();
        Consistency();
        Isolation();
        Durability();
    }

    // A — all-or-nothing: a transfer that breaks the rule changes NOTHING.
    private static void Atomicity()
    {
        var bank = NewBank();
        var result = bank.Transfer("Alice", "Bob", 999m); // more than Alice has
        Console.WriteLine($"A Atomicity  : send 999 -> {(result.Success ? "COMMIT" : "ROLLBACK")}; " +
                          $"Alice={bank.BalanceOf("Alice"):0.00} (unchanged)");
    }

    // C — the invariant "money is conserved" holds across a committed transfer.
    private static void Consistency()
    {
        var bank = NewBank();
        var before = bank.TotalMoney();
        bank.Transfer("Alice", "Bob", 30m);
        Console.WriteLine($"C Consistency: total before={before:0.00}, after={bank.TotalMoney():0.00} (conserved)");
    }

    // I — 200 transfers running at once never corrupt the total (no lost updates).
    private static void Isolation()
    {
        var bank = NewBank();
        Parallel.For(0, 100, _ =>
        {
            bank.Transfer("Alice", "Bob", 1m);
            bank.Transfer("Bob", "Alice", 1m);
        });
        Console.WriteLine($"I Isolation  : after 200 concurrent transfers, total={bank.TotalMoney():0.00} (uncorrupted)");
    }

    // D — a committed transfer survives a simulated crash + restart.
    private static void Durability()
    {
        var bank = NewBank();
        bank.Transfer("Alice", "Bob", 25m);      // COMMIT
        var saved = bank.Snapshot();             // persisted to durable storage
        var afterRestart = new Bank(saved);      // simulate crash, then restart from storage
        Console.WriteLine($"D Durability : after restart, Bob={afterRestart.BalanceOf("Bob"):0.00} (survived)");
    }
}
