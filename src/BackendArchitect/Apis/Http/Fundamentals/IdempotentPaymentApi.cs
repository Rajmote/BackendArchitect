namespace BackendArchitect.Apis.Http.Fundamentals;

public sealed record PaymentResult(string PaymentId, decimal Amount, bool WasReplayed);

// Makes a non-idempotent POST safe to retry, the way real payment APIs do it:
// the CLIENT generates a unique idempotency key per logical operation and resends it with every
// attempt; the server records "key -> result" and replays the stored response instead of charging
// again. One key = one charge, ever.
public sealed class IdempotentPaymentApi
{
    private readonly Dictionary<string, PaymentResult> _byIdempotencyKey = new(StringComparer.Ordinal);
    private int _nextId = 1;

    /// <summary>How many times money was actually taken (as opposed to a stored response replayed).</summary>
    public int ChargesExecuted { get; private set; }

    public PaymentResult Charge(string idempotencyKey, decimal amount)
    {
        // Seen this key before? Return the ORIGINAL response — do not charge again.
        if (_byIdempotencyKey.TryGetValue(idempotencyKey, out var existing))
            return existing with { WasReplayed = true };

        // New key: perform the charge and record the result under the key, together.
        ChargesExecuted++;
        var result = new PaymentResult($"pay-{_nextId++}", amount, WasReplayed: false);
        _byIdempotencyKey[idempotencyKey] = result;
        return result;
    }
}

// The naive version, for contrast: every call charges, so a retry after a timeout charges twice.
public sealed class NaivePaymentApi
{
    private int _nextId = 1;

    public int ChargesExecuted { get; private set; }

    public PaymentResult Charge(decimal amount)
    {
        ChargesExecuted++;
        return new PaymentResult($"pay-{_nextId++}", amount, WasReplayed: false);
    }
}
