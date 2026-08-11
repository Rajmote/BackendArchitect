namespace BackendArchitect.Reliability.Resilience;

/// <summary>Raised by the fake gateway to represent a transient failure worth retrying.</summary>
public sealed class GatewayUnavailableException(string message) : Exception(message);

// A stand-in dependency for the Polly examples: fails the first N calls, then recovers.
// It counts how many times it was actually invoked, so we can see what the pipeline shielded it from.
public sealed class FlakyPaymentGateway
{
    private readonly int _failuresBeforeRecovery;
    private int _calls;

    public FlakyPaymentGateway(int failuresBeforeRecovery) =>
        _failuresBeforeRecovery = failuresBeforeRecovery;

    /// <summary>How many times the dependency was actually hit.</summary>
    public int CallsReceived => Volatile.Read(ref _calls);

    public string Charge()
    {
        var attempt = Interlocked.Increment(ref _calls);
        if (attempt <= _failuresBeforeRecovery)
            throw new GatewayUnavailableException($"gateway unavailable (call {attempt})");

        return $"charged on call {attempt}";
    }

    /// <summary>Always fails — used to show a breaker opening.</summary>
    public string AlwaysFail()
    {
        Interlocked.Increment(ref _calls);
        throw new GatewayUnavailableException("gateway is down");
    }
}
