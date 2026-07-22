using BackendArchitect.Databases.Sql.Transactions;

namespace BackendArchitect.Tests.Databases.Sql;

// Databases · SQL · Transactions — proving Atomicity and Consistency on the little Bank model.
public class TransactionsTests
{
    private static Bank NewBank() => new(new Dictionary<string, decimal>
    {
        ["Alice"] = 40m,
        ["Bob"] = 0m,
    });

    [Fact]
    public void Transfer_WithSufficientFunds_Commits_AndMovesMoney()
    {
        var bank = NewBank();

        var result = bank.Transfer("Alice", "Bob", 30m);

        Assert.True(result.Success);
        Assert.Equal(10m, bank.BalanceOf("Alice"));
        Assert.Equal(30m, bank.BalanceOf("Bob"));
    }

    [Fact]
    public void Transfer_WithInsufficientFunds_RollsBack_AndChangesNothing() // Atomicity + Consistency
    {
        var bank = NewBank();

        var result = bank.Transfer("Alice", "Bob", 100m);

        Assert.False(result.Success);
        Assert.Equal("Alice has insufficient funds", result.Reason);
        Assert.Equal(40m, bank.BalanceOf("Alice")); // untouched
        Assert.Equal(0m, bank.BalanceOf("Bob"));    // untouched
    }

    [Fact]
    public void Transfer_NeverCreatesOrDestroysMoney() // total is conserved either way
    {
        var bank = NewBank();
        var before = bank.TotalMoney();

        bank.Transfer("Alice", "Bob", 30m);   // commits
        bank.Transfer("Alice", "Bob", 999m);  // rolls back

        Assert.Equal(before, bank.TotalMoney());
    }

    [Fact]
    public void Transfer_WithNonPositiveAmount_RollsBack()
    {
        var bank = NewBank();

        Assert.False(bank.Transfer("Alice", "Bob", 0m).Success);
        Assert.False(bank.Transfer("Alice", "Bob", -5m).Success);
        Assert.Equal(40m, bank.BalanceOf("Alice"));
    }
}
