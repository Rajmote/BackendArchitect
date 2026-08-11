namespace BackendArchitect.Practice.Exercise02;

public enum BreakerState
{
    /// <summary>Working normally: calls pass through and outcomes are recorded.</summary>
    Closed,

    /// <summary>Tripped: every call is rejected instantly without touching the dependency.</summary>
    Open,

    /// <summary>Cooldown elapsed: exactly ONE probe call may go through to test recovery.</summary>
    HalfOpen,
}

/// <summary>The result of putting a call through the breaker.</summary>
public sealed record BreakerResult(bool Succeeded, bool Rejected)
{
    public static BreakerResult Ok() => new(Succeeded: true, Rejected: false);
    public static BreakerResult Failed() => new(Succeeded: false, Rejected: false);

    /// <summary>Refused by the breaker — the dependency was never called.</summary>
    public static BreakerResult RejectedByBreaker() => new(Succeeded: false, Rejected: true);
}

/// <summary>
/// Wraps calls to a flaky dependency and stops calling it once it is clearly broken — protecting the
/// caller's threads AND giving the dependency room to recover.
/// See practice/Exercise02-CircuitBreaker.md.
/// </summary>
public sealed class PaymentCircuitBreaker
{
    private readonly double _failureRatio;
    private readonly int _sampleSize;
    private readonly TimeSpan _breakDuration;
    private readonly Func<DateTimeOffset> _now;

    private readonly Lock _gate = new();

    // The last _sampleSize outcomes: true = success. A rolling window, so a service failing 60% of the
    // time trips the breaker even though it never fails twice in a row.
    private readonly Queue<bool> _window = new();

    private BreakerState _state = BreakerState.Closed;
    private DateTimeOffset _openedAt;

    // Requirement 5: while half-open the state alone is not enough — ten threads can arrive at once and
    // all see "HalfOpen". This flag records that one of them has already been sent as the probe.
    private bool _probeInFlight;

    private int _callsAttempted;
    private int _callsRejected;

    public PaymentCircuitBreaker(double failureRatio, int sampleSize, TimeSpan breakDuration, Func<DateTimeOffset> now)
    {
        _failureRatio = failureRatio;
        _sampleSize = sampleSize;
        _breakDuration = breakDuration;
        _now = now;
    }

    public BreakerState State
    {
        get
        {
            lock (_gate)
                return CurrentState();
        }
    }

    /// <summary>Calls that actually reached the dependency.</summary>
    public int CallsAttempted
    {
        get { lock (_gate) return _callsAttempted; }
    }

    /// <summary>Calls the breaker refused without touching the dependency.</summary>
    public int CallsRejected
    {
        get { lock (_gate) return _callsRejected; }
    }

    /// <summary>
    /// Put a call through the breaker. The dependency is invoked only when the breaker permits it, and
    /// never by more than one thread while half-open.
    /// </summary>
    public BreakerResult Execute(Func<bool> call)
    {
        // PHASE 1 - DECIDE (locked). Short, and does no I/O.
        lock (_gate)
        {
            var state = CurrentState();

            if (state == BreakerState.Open || (state == BreakerState.HalfOpen && _probeInFlight))
            {
                _callsRejected++;
                return BreakerResult.RejectedByBreaker();   // fail fast: no network call, no thread held
            }

            if (state == BreakerState.HalfOpen)
            {
                // The cooldown has elapsed and this thread is the single probe.
                _state = BreakerState.HalfOpen;
                _probeInFlight = true;
            }

            _callsAttempted++;
        }

        // PHASE 2 - CALL (unlocked). Never hold a lock across I/O: it would serialise every caller
        // behind one slow network call and defeat the whole point of the breaker.
        bool succeeded;
        try
        {
            succeeded = call();
        }
        catch
        {
            Record(success: false);   // an exception is a failure too
            throw;
        }

        // PHASE 3 - RECORD (locked).
        Record(succeeded);
        return succeeded ? BreakerResult.Ok() : BreakerResult.Failed();
    }

    private void Record(bool success)
    {
        lock (_gate)
        {
            if (_state == BreakerState.HalfOpen)
            {
                // The probe decides the whole state, rather than merely adding to the statistics.
                if (success)
                    Close();      // recovered
                else
                    Trip();       // still broken: wait a FULL break duration again

                return;
            }

            // Evaluate the ratio after EVERY call, not only after failures: with a rolling window the
            // threshold can be crossed on a call that itself succeeded (fail, ok, fail, ok = 50%).
            Observe(success);
            if (ShouldTrip())
                Trip();
        }
    }

    /// <summary>An open breaker becomes half-open once the break duration has elapsed.</summary>
    private BreakerState CurrentState() =>
        _state == BreakerState.Open && _now() - _openedAt >= _breakDuration
            ? BreakerState.HalfOpen
            : _state;

    private void Observe(bool success)
    {
        _window.Enqueue(success);
        if (_window.Count > _sampleSize)
            _window.Dequeue();      // forget the oldest — only the last _sampleSize calls count
    }

    private bool ShouldTrip()
    {
        if (_window.Count < _sampleSize)
            return false;           // not enough evidence yet to judge

        var failures = _window.Count(succeeded => !succeeded);
        return (double)failures / _window.Count >= _failureRatio;
    }

    private void Trip()
    {
        _state = BreakerState.Open;
        _openedAt = _now();
        _probeInFlight = false;
        _window.Clear();
    }

    private void Close()
    {
        _state = BreakerState.Closed;
        _probeInFlight = false;
        _window.Clear();            // reset the statistics, or the old failures re-trip it instantly
    }
}
