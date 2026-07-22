namespace BackendArchitect.Databases.Sql.Transactions;

// Example runner: shows a transfer that ROLLS BACK (rule broken) and one that COMMITS,
// and that a rolled-back transfer changes nothing (Atomicity) and money is never created/destroyed.
public class TransactionsDemo
{
    public void Run()
    {
        var bank = new Bank(new Dictionary<string, decimal>
        {
            ["Alice"] = 40m,
            ["Bob"] = 0m,
        });

        Console.WriteLine($"Start   : Alice={bank.BalanceOf("Alice"):0.00}, Bob={bank.BalanceOf("Bob"):0.00}, total={bank.TotalMoney():0.00}");

        // 1) Too much — breaks the no-negative rule -> ROLLBACK, nothing changes.
        var r1 = bank.Transfer("Alice", "Bob", 100m);
        Console.WriteLine($"Send 100: {(r1.Success ? "COMMIT" : "ROLLBACK — " + r1.Reason)}");
        Console.WriteLine($"        : Alice={bank.BalanceOf("Alice"):0.00}, Bob={bank.BalanceOf("Bob"):0.00} (unchanged)");

        // 2) Affordable -> COMMIT, both balances move together.
        var r2 = bank.Transfer("Alice", "Bob", 30m);
        Console.WriteLine($"Send 30 : {(r2.Success ? "COMMIT" : "ROLLBACK — " + r2.Reason)}");
        Console.WriteLine($"        : Alice={bank.BalanceOf("Alice"):0.00}, Bob={bank.BalanceOf("Bob"):0.00}, total={bank.TotalMoney():0.00}");
    }
}
