namespace BackendArchitect.Databases.Sql.Transactions;

// The result of attempting a transfer: did the transaction COMMIT, or ROLL BACK (and why)?
public readonly record struct TransferResult(bool Success, string? Reason)
{
    public static TransferResult Ok() => new(true, null);
    public static TransferResult RolledBack(string reason) => new(false, reason);
}

// A tiny in-memory model of transactional money transfers, to make all four ACID properties concrete
// (an analogy, not a real database engine):
//
//   * Atomicity  — we do the work on a COPY of the balances; if anything is wrong we throw the copy
//                  away (nothing changed), otherwise we apply both changes together.
//   * Consistency — a rule ("no balance may go negative") is checked before commit, so the invariant
//                  always holds and money is never created or destroyed.
//   * Isolation  — a lock serializes transfers so two running at once can't corrupt each other
//                  (no lost updates).
//   * Durability — Snapshot() captures committed state; rebuilding a Bank from it models data
//                  surviving a crash/restart.
public sealed class Bank
{
    private readonly Dictionary<string, decimal> _balances;
    private readonly object _gate = new(); // ISOLATION: one transfer touches the balances at a time

    public Bank(IReadOnlyDictionary<string, decimal> initialBalances)
        => _balances = new Dictionary<string, decimal>(initialBalances);

    public decimal BalanceOf(string account)
    {
        lock (_gate)
            return _balances[account];
    }

    public decimal TotalMoney()
    {
        lock (_gate)
            return _balances.Values.Sum();
    }

    // DURABILITY analogy: a committed snapshot you can reload after a "restart".
    public IReadOnlyDictionary<string, decimal> Snapshot()
    {
        lock (_gate)
            return new Dictionary<string, decimal>(_balances);
    }

    // One transaction: debit `from`, credit `to`, all-or-nothing.
    public TransferResult Transfer(string from, string to, decimal amount)
    {
        if (amount <= 0)
            return TransferResult.RolledBack("amount must be positive");

        lock (_gate) // ISOLATION: serialize concurrent transfers so they can't corrupt each other
        {
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
}
