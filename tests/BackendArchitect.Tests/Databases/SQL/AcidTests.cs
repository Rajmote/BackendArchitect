using BackendArchitect.Databases.Sql.Transactions;

namespace BackendArchitect.Tests.Databases.Sql;

// Databases · SQL · Transactions — the Isolation and Durability halves of ACID.
// (Atomicity and Consistency are covered in TransactionsTests.)
public class AcidTests
{
    private static Bank NewBank() => new(new Dictionary<string, decimal>
    {
        ["Alice"] = 100m,
        ["Bob"] = 0m,
    });

    [Fact]
    public void Isolation_ConcurrentTransfers_NeverCorruptTheTotal()
    {
        var bank = NewBank();

        Parallel.For(0, 1000, _ =>
        {
            bank.Transfer("Alice", "Bob", 1m);
            bank.Transfer("Bob", "Alice", 1m);
        });

        Assert.Equal(100m, bank.TotalMoney()); // no lost updates under concurrency
    }

    [Fact]
    public void Durability_CommittedState_SurvivesARestart()
    {
        var bank = NewBank();
        bank.Transfer("Alice", "Bob", 25m); // committed

        var snapshot = bank.Snapshot();     // persisted
        var afterRestart = new Bank(snapshot); // crash + restart

        Assert.Equal(75m, afterRestart.BalanceOf("Alice"));
        Assert.Equal(25m, afterRestart.BalanceOf("Bob"));
    }
}
