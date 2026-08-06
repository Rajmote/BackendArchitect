using BackendArchitect.Practice.Exercise01;

namespace BackendArchitect.Practice.Tests.Exercise01;

// Exercise 01 — three starter tests showing the expected shape.
//
// YOUR JOB: make these pass, then add your own tests for requirements 4, 5 and 6:
//   4. invalid input (null/empty key, amount <= 0) is rejected without charging or storing
//   5. same key with a DIFFERENT amount is rejected, not silently replayed
//   6. two simultaneous retries on different threads still charge exactly once
public class IdempotentPaymentHandlerTests
{
    [Fact]
    public void FirstCall_ChargesTheCustomer()
    {
        var handler = new IdempotentPaymentHandler();

        var result = handler.Charge("key-1", 25.00m);

        Assert.True(result.Succeeded);
        Assert.False(result.WasReplayed);
        Assert.Equal(25.00m, result.Amount);
        Assert.NotNull(result.PaymentId);
        Assert.Equal(1, handler.ChargesExecuted);
    }

    [Fact]
    public void RetryWithTheSameKey_ReplaysTheOriginalResponse_WithoutChargingAgain()
    {
        var handler = new IdempotentPaymentHandler();

        var first = handler.Charge("key-1", 25.00m);
        var retry = handler.Charge("key-1", 25.00m);

        Assert.True(retry.Succeeded);
        Assert.True(retry.WasReplayed);
        Assert.Equal(first.PaymentId, retry.PaymentId);   // the SAME payment, not a new one
        Assert.Equal(1, handler.ChargesExecuted);         // money moved exactly once
    }

    [Fact]
    public void ADifferentKey_IsADifferentOperation_AndChargesAgain()
    {
        var handler = new IdempotentPaymentHandler();

        handler.Charge("key-1", 25.00m);
        var second = handler.Charge("key-2", 25.00m);

        Assert.True(second.Succeeded);
        Assert.False(second.WasReplayed);
        Assert.Equal(2, handler.ChargesExecuted);
    }

    // TODO (you): requirement 4 — invalid input rejected, nothing charged, nothing stored.
    //             Hint: after a rejected call, the SAME key should still work for a valid request.

    // TODO (you): requirement 5 — same key, different amount -> rejected.

    // TODO (you): requirement 6 — concurrent retries charge exactly once.
    //             Hint: Parallel.For, or several Threads released together.
}
