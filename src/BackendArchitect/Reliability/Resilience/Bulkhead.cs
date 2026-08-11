namespace BackendArchitect.Reliability.Resilience;

// Named after ship compartments: a hole in one must not sink the vessel.
//
// If every outbound call shares one thread pool, a single hanging dependency exhausts it and EVERY
// feature breaks - including ones that never touch that dependency. A bulkhead caps concurrency per
// dependency, so a hung recommendations service degrades recommendations, not checkout.
public sealed class Bulkhead
{
    private readonly int _maxConcurrency;
    private readonly Lock _gate = new();
    private int _inFlight;

    public Bulkhead(int maxConcurrency) => _maxConcurrency = maxConcurrency;

    public int Rejected { get; private set; }
    public int Executed { get; private set; }
    public int InFlight => Volatile.Read(ref _inFlight);

    /// <summary>
    /// Runs the call if there is capacity; otherwise rejects it immediately, leaving threads free for
    /// everything else. Rejection is the point: it is what stops one dependency draining the pool.
    /// </summary>
    public bool TryExecute(Action call)
    {
        lock (_gate)
        {
            if (_inFlight >= _maxConcurrency)
            {
                Rejected++;
                return false;
            }

            _inFlight++;
        }

        try
        {
            call();
            lock (_gate) Executed++;
            return true;
        }
        finally
        {
            lock (_gate) _inFlight--;
        }
    }
}
