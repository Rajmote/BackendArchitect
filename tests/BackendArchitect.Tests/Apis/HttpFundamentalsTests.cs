using BackendArchitect.Apis.Http.Fundamentals;

namespace BackendArchitect.Tests.Apis;

// APIs · HTTP · Fundamentals — safe/idempotent semantics, retry decisions, idempotency keys.
public class HttpFundamentalsTests
{
    [Theory]
    [InlineData(HttpMethodKind.Get, true)]
    [InlineData(HttpMethodKind.Head, true)]
    [InlineData(HttpMethodKind.Post, false)]
    [InlineData(HttpMethodKind.Put, false)]
    [InlineData(HttpMethodKind.Delete, false)]
    public void IsSafe_IsTrueOnlyForReadOnlyMethods(HttpMethodKind method, bool expected)
    {
        Assert.Equal(expected, HttpSemantics.IsSafe(method));
    }

    [Theory]
    [InlineData(HttpMethodKind.Get, true)]
    [InlineData(HttpMethodKind.Put, true)]      // replace: same result however many times
    [InlineData(HttpMethodKind.Delete, true)]   // deleting twice leaves it just as deleted
    [InlineData(HttpMethodKind.Post, false)]    // creates a NEW resource every time
    [InlineData(HttpMethodKind.Patch, false)]
    public void IsIdempotent_MatchesTheHttpSpec(HttpMethodKind method, bool expected)
    {
        Assert.Equal(expected, HttpSemantics.IsIdempotent(method));
    }

    [Fact]
    public void EverySafeMethod_IsAlsoIdempotent()
    {
        foreach (var method in Enum.GetValues<HttpMethodKind>())
            if (HttpSemantics.IsSafe(method))
                Assert.True(HttpSemantics.IsIdempotent(method), $"{method} is safe so must be idempotent");
    }

    [Theory]
    [InlineData(400, false)]  // your request is malformed — retrying changes nothing
    [InlineData(404, false)]
    [InlineData(408, true)]   // timeout
    [InlineData(429, true)]   // the 4xx that IS retryable
    [InlineData(500, true)]
    [InlineData(501, false)]  // the 5xx that is NOT
    [InlineData(503, true)]
    public void IsTransient_SeparatesRetryableStatusesFromPermanentOnes(int status, bool expected)
    {
        Assert.Equal(expected, RetryPolicy.IsTransient(status));
    }

    [Fact]
    public void ShouldRetry_RequiresBothATransientStatusAndARepeatableOperation()
    {
        Assert.True(RetryPolicy.ShouldRetry(503, HttpMethodKind.Put));    // transient + idempotent
        Assert.False(RetryPolicy.ShouldRetry(400, HttpMethodKind.Put));   // permanent
        Assert.False(RetryPolicy.ShouldRetry(503, HttpMethodKind.Post));  // transient but NOT idempotent
    }

    [Fact]
    public void ShouldRetry_AllowsPost_WhenAnIdempotencyKeyMakesTheHandlerIdempotent()
    {
        Assert.True(RetryPolicy.ShouldRetry(503, HttpMethodKind.Post, hasIdempotencyKey: true));
    }

    [Fact]
    public void WithoutAnIdempotencyKey_RetriesChargeTheCustomerEveryTime()
    {
        var api = new NaivePaymentApi();

        for (var attempt = 0; attempt < 3; attempt++)
            api.Charge(100m);

        Assert.Equal(3, api.ChargesExecuted); // the double(-triple)-charge bug
    }

    [Fact]
    public void WithAnIdempotencyKey_RetriesChargeOnce_AndReplayTheOriginalResponse()
    {
        var api = new IdempotentPaymentApi();
        const string key = "key-1";

        var first = api.Charge(key, 100m);
        var second = api.Charge(key, 100m);
        var third = api.Charge(key, 100m);

        Assert.Equal(1, api.ChargesExecuted);
        Assert.False(first.WasReplayed);
        Assert.True(second.WasReplayed);
        Assert.Equal(first.PaymentId, third.PaymentId); // same original response returned
    }

    [Fact]
    public void ADifferentKey_IsADifferentOperation_AndChargesAgain()
    {
        var api = new IdempotentPaymentApi();

        api.Charge("key-1", 100m);
        api.Charge("key-2", 100m);   // e.g. a NEW key generated per attempt — the classic mistake

        Assert.Equal(2, api.ChargesExecuted);
    }
}
