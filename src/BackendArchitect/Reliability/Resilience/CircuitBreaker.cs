namespace BackendArchitect.Reliability.Resilience;

public enum CircuitState
{
    /// <summary>Traffic flows; failures are counted. (Confusingly, "closed" = working — as in a closed electrical circuit.)</summary>
    Closed,

    /// <summary>Tripped: calls fail INSTANTLY without touching the dependency.</summary>
    Open,

    /// <summary>Cooldown elapsed: exactly one probe call is allowed through to test recovery.</summary>
    HalfOpen,
}

public sealed record CallOutcome(bool Succeeded, bool ShortCircuited, string? Error)
{
    public static CallOutcome Ok() => new(true, false, null);
    public static CallOutcome Failed(string error) => new(false, false, error);

    /// <summary>Rejected by the breaker without calling the dependency at all.</summary>
    public static CallOutcome Rejected() => new(false, true, "circuit is open");
}

// Like the breaker in a fuse box: when a dependency keeps failing, cut the connection instead of
// pushing more current into a fault.
//
// It fails FAST on purpose, which helps two parties:
//   * the caller   - fails in microseconds instead of holding a thread for the timeout duration
//   * the callee   - stops being hammered, so it finally gets the quiet it needs to recover
//
// Time is injected so the state transitions can be tested without sleeping.
public sealed class CircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _breakDuration;
    private readonly Func<DateTimeOffset> _now;

    private CircuitState _state = CircuitState.Closed;
    private int _consecutiveFailures;
    private DateTimeOffset _openedAt;

    public CircuitBreaker(int failureThreshold, TimeSpan breakDuration, Func<DateTimeOffset> now)
    {
        _failureThreshold = failureThreshold;
        _breakDuration = breakDuration;
        _now = now;
    }

    /// <summary>Calls that never reached the dependency because the breaker was open.</summary>
    public int ShortCircuitedCalls { get; private set; }

    /// <summary>Calls that actually reached the dependency.</summary>
    public int CallsAttempted { get; private set; }

    public CircuitState State
    {
        get
        {
            // An open breaker becomes half-open once the cooldown has elapsed.
            if (_state == CircuitState.Open && _now() - _openedAt >= _breakDuration)
                return CircuitState.HalfOpen;

            return _state;
        }
    }

    /// <summary>Run <paramref name="call"/> through the breaker. Returns true from the call to signal success.</summary>
    public CallOutcome Execute(Func<bool> call)
    {
        var state = State;

        if (state == CircuitState.Open)
        {
            ShortCircuitedCalls++;
            return CallOutcome.Rejected();     // fail fast: no network call, no thread held
        }

        // Closed or HalfOpen: the call is allowed through. In HalfOpen this is the single probe.
        _state = state;
        CallsAttempted++;

        var succeeded = call();
        if (succeeded)
        {
            OnSuccess();
            return CallOutcome.Ok();
        }

        OnFailure();
        return CallOutcome.Failed("dependency call failed");
    }

    private void OnSuccess()
    {
        // A successful probe closes the breaker; a success while closed clears the failure streak.
        _state = CircuitState.Closed;
        _consecutiveFailures = 0;
    }

    private void OnFailure()
    {
        if (_state == CircuitState.HalfOpen)
        {
            Trip();                            // the probe failed: still broken, wait another period
            return;
        }

        _consecutiveFailures++;
        if (_consecutiveFailures >= _failureThreshold)
            Trip();
    }

    private void Trip()
    {
        _state = CircuitState.Open;
        _openedAt = _now();
    }
}
