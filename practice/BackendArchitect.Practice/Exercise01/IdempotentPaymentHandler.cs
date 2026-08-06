namespace BackendArchitect.Practice.Exercise01;

/// <summary>The outcome of a charge attempt.</summary>
/// <remarks>
/// Failures are RETURNED, not thrown: a client sending a bad amount is an everyday outcome, not an
/// exceptional one, and it maps naturally onto an HTTP 400. Exceptions are reserved for things the
/// caller cannot anticipate.
/// </remarks>
public sealed record PaymentResult(
    bool Succeeded,
    string? PaymentId,
    decimal Amount,
    bool WasReplayed,
    string? Error)
{
    /// <summary>Money moved: a brand-new payment.</summary>
    public static PaymentResult Charged(string paymentId, decimal amount) =>
        new(Succeeded: true, paymentId, amount, WasReplayed: false, Error: null);

    /// <summary>The stored response for a key we've already seen. Still a SUCCESS — so no error text.</summary>
    public static PaymentResult Replayed(PaymentResult original) =>
        original with { WasReplayed = true };

    /// <summary>The request was refused; nothing was charged and nothing was stored.</summary>
    public static PaymentResult Rejected(decimal amount, string error) =>
        new(Succeeded: false, PaymentId: null, amount, WasReplayed: false, error);
}

/// <summary>
/// Makes POST /payments safe to retry using a client-supplied idempotency key: one key = one charge,
/// however many times the request arrives. See practice/Exercise01-IdempotencyKeys.md.
/// </summary>
public sealed class IdempotentPaymentHandler
{
    // Guards every piece of mutable state below. A plain Dictionary is not thread-safe for concurrent
    // writes, and — more importantly — "look up the key, then charge" is a CHECK-THEN-ACT sequence:
    // without a lock two simultaneous retries both find nothing and both charge the customer.
    private readonly Lock _gate = new();

    private readonly Dictionary<string, PaymentResult> _completedPayments = new(StringComparer.Ordinal);
    private int _nextPaymentId = 1;
    private int _chargesExecuted;

    /// <summary>How many times money actually moved. Replays and rejections do not count.</summary>
    public int ChargesExecuted => Volatile.Read(ref _chargesExecuted);

    /// <summary>
    /// Charge <paramref name="amount"/> exactly once per <paramref name="idempotencyKey"/>.
    /// Repeat calls with the same key return the stored original response instead of charging again.
    /// </summary>
    public PaymentResult Charge(string? idempotencyKey, decimal amount)
    {
        // Validate before taking the lock — there is no reason to serialise threads to reject bad input.
        // Each failure gets its own message: "invalid request" tells a caller nothing at 2am.
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return PaymentResult.Rejected(amount, "An idempotency key is required.");

        if (amount <= 0)
            return PaymentResult.Rejected(amount, $"The amount must be greater than zero, but was {amount}.");

        // The lookup and the charge must be ONE atomic step.
        lock (_gate)
        {
            if (_completedPayments.TryGetValue(idempotencyKey, out var original))
            {
                // A key identifies ONE operation. A different amount means the client reused the key by
                // mistake — silently returning the original would hide a real bug and charge the wrong
                // amount in the caller's mind. Stripe rejects this too.
                return original.Amount == amount
                    ? PaymentResult.Replayed(original)
                    : PaymentResult.Rejected(amount,
                        $"Idempotency key '{idempotencyKey}' was already used for an amount of {original.Amount}.");
            }

            // Record the result under the key in the same critical section that performs the charge,
            // so no concurrent caller can observe a charge that has not yet been stored.
            var payment = PaymentResult.Charged(NextPaymentId(), amount);
            _completedPayments[idempotencyKey] = payment;
            _chargesExecuted++;
            return payment;
        }
    }

    /// <summary>Generates the next payment id. Only safe to call while holding <see cref="_gate"/>.</summary>
    private string NextPaymentId() => $"pay-{_nextPaymentId++}";
}
