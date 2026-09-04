namespace BackendArchitect.Concurrency.Locks;

public sealed class Account
{
    public Account(int id, string name, decimal balance)
    {
        Id = id;
        Name = name;
        Balance = balance;
    }

    public int Id { get; }
    public string Name { get; }
    public decimal Balance { get; set; }

    internal Lock Gate { get; } = new();
}

public sealed record TransferReport(bool OrderedLocks, int Completed, int Deadlocked, decimal TotalMoney);

// Two locks taken in OPPOSITE orders is a deadlock.
//
//   Thread A: Transfer(alice, bob)      Thread B: Transfer(bob, alice)
//   lock (alice) - acquired             lock (bob)   - acquired
//   lock (bob)   - waiting for B        lock (alice) - waiting for A     -> both wait forever
//
// "from first, then to" LOOKS like a consistent order. It is consistent in terms of the PARAMETERS and
// inconsistent in terms of the OBJECTS, which is the only thing the locks know about.
//
// Worse than a race: no exception, no crash. The threads simply stop. The pool drains, the service
// stops answering, and only a restart clears it.
//
// The fix is a total order every caller agrees on - here, ascending account id.
public static class AccountTransfer
{
    // A real deadlock never times out. TryEnter is only here so the demo can DETECT one and report it
    // instead of hanging the test run.
    private static readonly TimeSpan FirstLockPatience = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SecondLockPatience = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Runs two opposing transfers at the same instant. With <paramref name="orderLocks"/> off they
    /// deadlock; with it on both complete.
    /// </summary>
    public static TransferReport RunOpposingTransfers(bool orderLocks)
    {
        var alice = new Account(1, "Alice", 1_000m);
        var bob = new Account(2, "Bob", 1_000m);

        var bothThreadsRunning = new Barrier(2);
        var bothHoldTheirFirstLock = new CountdownEvent(2);
        var completed = 0;
        var deadlocked = 0;

        // With ordered locks the second thread is blocked on the FIRST lock, so it can never reach the
        // rendezvous - a short wait there is pure dead time. Unordered, both threads hold a different
        // lock and will always arrive, so a generous budget costs nothing and removes the flake.
        var rendezvous = orderLocks ? TimeSpan.FromMilliseconds(200) : TimeSpan.FromSeconds(5);

        void Transfer(Account from, Account to, decimal amount)
        {
            // The whole fix is this one line: order by the OBJECT, not by the parameter name.
            var (first, second) = orderLocks && to.Id < from.Id ? (to, from) : (from, to);

            bothThreadsRunning.SignalAndWait();

            if (!first.Gate.TryEnter(FirstLockPatience))
            {
                Interlocked.Increment(ref deadlocked);
                return;
            }

            try
            {
                // Force the worst-case interleaving: neither thread reaches for its second lock until
                // both are holding their first.
                bothHoldTheirFirstLock.Signal();
                bothHoldTheirFirstLock.Wait(rendezvous);

                if (!second.Gate.TryEnter(SecondLockPatience))
                {
                    Interlocked.Increment(ref deadlocked);
                    return;
                }

                try
                {
                    from.Balance -= amount;
                    to.Balance += amount;
                    Interlocked.Increment(ref completed);
                }
                finally
                {
                    second.Gate.Exit();
                }
            }
            finally
            {
                first.Gate.Exit();
            }
        }

        var threadA = new Thread(() => Transfer(alice, bob, 100m));
        var threadB = new Thread(() => Transfer(bob, alice, 50m));

        threadA.Start();
        threadB.Start();
        threadA.Join();
        threadB.Join();

        // Money is conserved either way - a deadlock loses availability, not correctness.
        return new TransferReport(orderLocks, completed, deadlocked, alice.Balance + bob.Balance);
    }
}
