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
/// Wraps calls to a flaky dependency. See practice/Exercise02-CircuitBreaker.md for the requirements.
/// </summary>
public sealed class PaymentCircuitBreaker
{
    private readonly double _failureRatio;
    private readonly int _sampleSize;
    private readonly TimeSpan _breakDuration;
    private readonly Func<DateTimeOffset> _now;

    /// <param name="failureRatio">Trip when this proportion of the sample has failed, e.g. 0.5 for 50%.</param>
    /// <param name="sampleSize">How many recent calls to judge on.</param>
    /// <param name="breakDuration">How long to stay open before allowing a probe.</param>
    /// <param name="now">Injected clock, so tests never sleep.</param>
    public PaymentCircuitBreaker(double failureRatio, int sampleSize, TimeSpan breakDuration, Func<DateTimeOffset> now)
    {
        _failureRatio = failureRatio;
        _sampleSize = sampleSize;
        _breakDuration = breakDuration;
        _now = now;
    }

    /// <summary>The breaker's current state.</summary>
    public BreakerState State =>
        throw new NotImplementedException("Exercise 02 — implement State");

    /// <summary>Calls that actually reached the dependency.</summary>
    public int CallsAttempted =>
        throw new NotImplementedException("Exercise 02 — implement CallsAttempted");

    /// <summary>Calls the breaker refused without touching the dependency.</summary>
    public int CallsRejected =>
        throw new NotImplementedException("Exercise 02 — implement CallsRejected");

    /// <summary>
    /// Put a call through the breaker. <paramref name="call"/> returns true for success.
    /// It must NOT be invoked when the breaker is open, nor by more than one thread while half-open.
    /// </summary>
    public BreakerResult Execute(Func<bool> call)
    {
        // TODO: implement me.
        throw new NotImplementedException("Exercise 02 — implement Execute()");
    }
}
