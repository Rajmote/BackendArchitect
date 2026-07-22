namespace BackendArchitect.Databases.Sql.Transactions;

// The result of attempting a transfer: did the transaction COMMIT, or ROLL BACK (and why)?
public readonly record struct TransferResult(bool Success, string? Reason)
{
    public static TransferResult Ok() => new(true, null);
    public static TransferResult RolledBack(string reason) => new(false, reason);
}

// A tiny in-memory model of a transactional money transfer, to make ACID's A and C concrete
// (an analogy, not a real database engine).
//
//   * Atomicity  — we do the work on a COPY of the balances. If anything is wrong we throw the copy
//                  away, so NOTHING changed. If all is well we apply both changes together.
//   * Consistency — a rule ("no balance may go negative") is checked before commit; a transfer that
//                  would break it is rolled back, so the invariant always holds.
public sealed class Bank
{
    private readonly Dictionary<string, decimal> _balances;

    public Bank(IDictionary<string, decimal> initialBalances)
        => _balances = new Dictionary<string, decimal>(initialBalances);

    public decimal BalanceOf(string account) => _balances[account];

    public decimal TotalMoney() => _balances.Values.Sum();

    // One transaction: debit `from`, credit `to`, all-or-nothing.
    public TransferResult Transfer(string from, string to, decimal amount)
    {
        if (amount <= 0)
            return TransferResult.RolledBack("amount must be positive");

        // BEGIN: work on a copy so a failure leaves the real balances untouched (Atomicity).
        var working = new Dictionary<string, decimal>(_balances);
        working[from] -= amount;
        working[to] += amount;

        // CONSISTENCY rule: no balance may go negative.
        if (working[from] < 0)
            return TransferResult.RolledBack($"{from} has insufficient funds"); // ROLLBACK: discard copy

        // COMMIT: apply both changes together.
        _balances[from] = working[from];
        _balances[to] = working[to];
        return TransferResult.Ok();
    }
}
