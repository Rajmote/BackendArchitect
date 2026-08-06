namespace BackendArchitect.Practice.Exercise01;

/// <summary>The outcome of a charge attempt. Feel free to reshape this if you prefer a different design.</summary>
public sealed record PaymentResult(
    bool Succeeded,
    string? PaymentId,
    decimal Amount,
    bool WasReplayed,
    string? Error);

/// <summary>
/// Makes POST /payments safe to retry using a client-supplied idempotency key.
/// See practice/Exercise01-IdempotencyKeys.md for the requirements.
/// </summary>
public sealed class IdempotentPaymentHandler
{
    private int _nextPaymentId = 1;

    /// <summary>How many times money actually moved (replays must NOT increment this).</summary>
    public int ChargesExecuted { get; private set; }

    /// <summary>
    /// Charge <paramref name="amount"/> once per <paramref name="idempotencyKey"/>.
    /// Repeat calls with the same key return the stored original response.
    /// </summary>
    public PaymentResult Charge(string? idempotencyKey, decimal amount)
    {
        // TODO: implement me.
        // Remember: a rejected request must not store anything, and two threads may arrive at once.
        throw new NotImplementedException("Exercise 01 — implement Charge()");
    }

    /// <summary>Generates the id for a newly created payment. Use this when you actually charge.</summary>
    private string NextPaymentId() => $"pay-{_nextPaymentId++}";
}
