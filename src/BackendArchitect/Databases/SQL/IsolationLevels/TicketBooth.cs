namespace BackendArchitect.Databases.Sql.IsolationLevels;

// Models the concert-ticket oversell to make isolation concrete (an analogy, not a real DB).
//
// Booking is a classic "check-then-act": read availability, THEN decrement. There's a gap between
// the read and the write. Whether two concurrent buyers can both slip through that gap depends on
// isolation:
//   * serialized = false -> no protection: concurrent buyers race and can OVERSELL (the lost-update
//                           bug you get at a too-loose isolation level).
//   * serialized = true  -> a lock makes each booking atomic (like SERIALIZABLE): never oversells.
//
// Counters use Interlocked so the *counts* are accurate even under the race — the bug we're showing
// is the check-then-act gap, not a corrupted counter. `gap` is an optional hook run inside that gap;
// the demo injects a barrier there to force the worst-case interleaving deterministically; tests
// leave it null.
public sealed class TicketBooth
{
    private readonly bool _serialized;
    private readonly object _lock = new();
    private readonly Action? _gap;
    private int _available;
    private int _sold;

    public TicketBooth(int available, bool serialized, Action? gap = null)
    {
        _available = available;
        _serialized = serialized;
        _gap = gap;
    }

    public int Available => Volatile.Read(ref _available);
    public int Sold => Volatile.Read(ref _sold);

    public bool TryBook()
    {
        if (_serialized)
        {
            lock (_lock)
                return BookCore();
        }

        return BookCore(); // no isolation: the read->write gap below is a race window
    }

    private bool BookCore()
    {
        if (Volatile.Read(ref _available) <= 0)
            return false;

        _gap?.Invoke(); // the gap between reading availability and writing it back

        Interlocked.Decrement(ref _available);
        Interlocked.Increment(ref _sold);
        return true;
    }
}
