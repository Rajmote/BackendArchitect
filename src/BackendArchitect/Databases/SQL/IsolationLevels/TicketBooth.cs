namespace BackendArchitect.Databases.Sql.IsolationLevels;

// Models the concert-ticket oversell to make isolation concrete (an analogy, not a real DB).
//
// Booking is a classic "check-then-act": read availability, THEN decrement. There's a gap between
// the read and the write. Whether two concurrent buyers can both slip through that gap depends on
// isolation:
//   * serialized = false -> no protection: concurrent buyers race and can OVERSELL (the phantom /
//                           lost-update bug you get at a too-loose isolation level).
//   * serialized = true  -> a lock makes each booking atomic (like SERIALIZABLE): never oversells.
public sealed class TicketBooth
{
    private readonly bool _serialized;
    private readonly object _gate = new();
    private int _available;

    public TicketBooth(int available, bool serialized)
    {
        _available = available;
        _serialized = serialized;
    }

    public int Available => _available;
    public int Sold { get; private set; }

    public bool TryBook()
    {
        if (_serialized)
        {
            lock (_gate)
                return BookCore();
        }

        return BookCore(); // no isolation: the read->write gap below is a race window
    }

    private bool BookCore()
    {
        if (_available <= 0)
            return false;

        // Widen the read->write gap so concurrent callers reliably interleave (models the race).
        Thread.Sleep(5);

        _available--;
        Sold++;
        return true;
    }
}
