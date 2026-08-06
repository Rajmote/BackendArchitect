using BackendArchitect.Practice.Exercise01;

namespace BackendArchitect.Practice.Tests.Exercise01;

// Exercise 01 — idempotency keys.
public class IdempotentPaymentHandlerTests
{
    private const decimal Amount = 25.00m;

    // --- requirements 1-3: the core idempotency behaviour ---

    [Fact]
    public void FirstCall_ChargesTheCustomer()
    {
        var handler = new IdempotentPaymentHandler();

        var result = handler.Charge("key-1", Amount);

        Assert.True(result.Succeeded);
        Assert.False(result.WasReplayed);
        Assert.Equal(Amount, result.Amount);
        Assert.NotNull(result.PaymentId);
        Assert.Equal(1, handler.ChargesExecuted);
    }

    [Fact]
    public void RetryWithTheSameKey_ReplaysTheOriginalResponse_WithoutChargingAgain()
    {
        var handler = new IdempotentPaymentHandler();

        var first = handler.Charge("key-1", Amount);
        var retry = handler.Charge("key-1", Amount);

        Assert.True(retry.Succeeded);
        Assert.True(retry.WasReplayed);
        Assert.Equal(first.PaymentId, retry.PaymentId);   // the SAME payment, not a new one
        Assert.Equal(1, handler.ChargesExecuted);         // money moved exactly once
    }

    [Fact]
    public void ADifferentKey_IsADifferentOperation_AndChargesAgain()
    {
        var handler = new IdempotentPaymentHandler();

        handler.Charge("key-1", Amount);
        var second = handler.Charge("key-2", Amount);

        Assert.True(second.Succeeded);
        Assert.False(second.WasReplayed);
        Assert.Equal(2, handler.ChargesExecuted);
    }

    [Fact]
    public void AReplay_IsASuccess_SoItCarriesNoError()
    {
        var handler = new IdempotentPaymentHandler();

        handler.Charge("key-1", Amount);
        var retry = handler.Charge("key-1", Amount);

        Assert.Null(retry.Error);   // WasReplayed already conveys it; Error would mislead the client
    }

    // --- requirement 4: invalid input is rejected without charging or storing ---

    [Theory]
    [InlineData(null, 25.00)]
    [InlineData("", 25.00)]
    [InlineData("   ", 25.00)]     // whitespace is not a usable key either
    [InlineData("key-1", 0)]
    [InlineData("key-1", -5.00)]
    public void InvalidInput_IsRejected_WithoutCharging(string? key, decimal amount)
    {
        var handler = new IdempotentPaymentHandler();

        var result = handler.Charge(key, amount);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
        Assert.Null(result.PaymentId);
        Assert.Equal(0, handler.ChargesExecuted);
    }

    [Fact]
    public void ARejectedRequest_DoesNotPoisonTheKey()
    {
        var handler = new IdempotentPaymentHandler();

        var rejected = handler.Charge("key-1", 0m);       // invalid: nothing may be stored
        var accepted = handler.Charge("key-1", Amount);   // the same key must still be usable

        Assert.False(rejected.Succeeded);
        Assert.True(accepted.Succeeded);
        Assert.False(accepted.WasReplayed);
        Assert.Equal(1, handler.ChargesExecuted);
    }

    [Fact]
    public void EachValidationFailure_ExplainsItself()
    {
        var handler = new IdempotentPaymentHandler();

        var missingKey = handler.Charge(null, Amount);
        var badAmount = handler.Charge("key-1", -1m);

        Assert.NotEqual(missingKey.Error, badAmount.Error);   // distinct, actionable messages
    }

    // --- requirement 5: the key identifies ONE operation ---

    [Fact]
    public void SameKey_WithADifferentAmount_IsRejected_RatherThanSilentlyReplayed()
    {
        var handler = new IdempotentPaymentHandler();

        handler.Charge("key-1", Amount);
        var mismatch = handler.Charge("key-1", 30.00m);

        Assert.False(mismatch.Succeeded);
        Assert.False(mismatch.WasReplayed);
        Assert.NotNull(mismatch.Error);
        Assert.Equal(1, handler.ChargesExecuted);   // and it certainly must not charge again
    }

    // --- requirement 6: concurrent retries ---

    [Fact]
    public void ConcurrentRetries_WithTheSameKey_ChargeExactlyOnce()
    {
        const int retries = 20;
        var handler = new IdempotentPaymentHandler();
        var results = new PaymentResult[retries];

        // Real threads plus a Barrier, so every retry is genuinely in flight at the same instant.
        // (Parallel.For can't be used with a Barrier here: the thread pool may not run all iterations
        // concurrently, and the barrier would deadlock waiting for participants that never start.)
        using var atTheGate = new Barrier(retries);
        var threads = Enumerable.Range(0, retries)
            .Select(i => new Thread(() =>
            {
                atTheGate.SignalAndWait();                          // line up...
                results[i] = handler.Charge("key-1", Amount);       // ...then all rush together
            }))
            .ToList();

        foreach (var thread in threads) thread.Start();
        foreach (var thread in threads) thread.Join();

        Assert.Equal(1, handler.ChargesExecuted);                       // charged exactly once
        Assert.All(results, r => Assert.True(r.Succeeded));             // every caller got an answer
        Assert.Single(results.Select(r => r.PaymentId).Distinct());     // all describing ONE payment
        Assert.Equal(retries - 1, results.Count(r => r.WasReplayed));   // exactly one was the original
    }

    [Fact]
    public void ConcurrentCallsWithDistinctKeys_EachChargeExactlyOnce()
    {
        const int keys = 20;
        var handler = new IdempotentPaymentHandler();

        // The lock must not LOSE updates either — 20 different keys means 20 distinct charges.
        Parallel.For(0, keys, i => handler.Charge($"key-{i}", Amount));

        Assert.Equal(keys, handler.ChargesExecuted);
    }
}
