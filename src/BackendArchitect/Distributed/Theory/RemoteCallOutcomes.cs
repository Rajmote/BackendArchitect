namespace BackendArchitect.Distributed.Theory;

public sealed record RetryOutcome(string Strategy, int Orders, int CallsMade, int ChargesApplied, int DoubleCharged);

// A local call has two outcomes: it returned, or it threw. A REMOTE call has three:
//
//   1. the request never arrived              -> not charged, safe to retry
//   2. it arrived, was processed, and the     -> CHARGED, and retrying charges again
//      response was lost on the way back
//   3. it arrived and is still running        -> about to charge, retrying races it
//
// A timeout cannot distinguish them, and waiting longer never will: "unknown" is permanent. Knowing the
// CAUSE (slow database, congested network) does not help either - any cause produces any of the three.
//
// You cannot eliminate the ambiguity. You can only stop caring about it, which is what an idempotency
// key does: the retry is the same operation, not a second one.
public sealed class PaymentGateway
{
    private readonly Dictionary<string, int> _chargesPerOrder = [];
    private readonly Dictionary<string, decimal> _receiptsByKey = [];

    public int TotalCharges => _chargesPerOrder.Values.Sum();

    public int OrdersChargedMoreThanOnce => _chargesPerOrder.Values.Count(charges => charges > 1);

    public decimal Charge(string orderId, decimal amount, string? idempotencyKey)
    {
        if (idempotencyKey is not null && _receiptsByKey.TryGetValue(idempotencyKey, out var existing))
            return existing;                       // a replay, not a second charge

        _chargesPerOrder[orderId] = _chargesPerOrder.GetValueOrDefault(orderId) + 1;

        if (idempotencyKey is not null)
            _receiptsByKey[idempotencyKey] = amount;

        return amount;
    }
}

public static class RemoteCallOutcomes
{
    /// <summary>
    /// Every order is charged once. For one order in <paramref name="everyNthLosesResponse"/> the
    /// gateway completes the charge and the RESPONSE is lost, so the client times out and retries -
    /// outcome 2, the dangerous one.
    /// </summary>
    public static RetryOutcome RunWithRetries(bool useIdempotencyKey, int orders, int everyNthLosesResponse)
    {
        var gateway = new PaymentGateway();
        var calls = 0;

        for (var order = 0; order < orders; order++)
        {
            var orderId = $"order-{order}";
            var key = useIdempotencyKey ? $"key-{order}" : null;
            var responseWillBeLost = order % everyNthLosesResponse == 0;

            calls++;
            gateway.Charge(orderId, 50m, key);     // the gateway always does the work

            if (!responseWillBeLost)
                continue;

            // The client saw a timeout. It has no way to know the charge already happened.
            calls++;
            gateway.Charge(orderId, 50m, key);
        }

        return new RetryOutcome(
            useIdempotencyKey ? "with idempotency key" : "plain retry",
            orders,
            calls,
            gateway.TotalCharges,
            gateway.OrdersChargedMoreThanOnce);
    }
}
