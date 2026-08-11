using BackendArchitect.Practice.Exercise02;

namespace BackendArchitect.Practice.Tests.Exercise02;

// Exercise 02 — three starter tests showing the expected shape.
//
// YOUR JOB: make these pass, then add your own tests for:
//   2. trips on FAILURE RATIO over a rolling window (not consecutive failures)
//   4. half-open -> success closes and resets stats; failure re-opens for a FULL break duration
//   5. only ONE probe is admitted while half-open; concurrent arrivals are rejected
//   6. everything above holds under concurrent callers
public class PaymentCircuitBreakerTests
{
    /// <summary>A breaker plus a handle to move time forward, so nothing has to sleep.</summary>
    private static (PaymentCircuitBreaker Breaker, Action<int> AdvanceSeconds) NewBreaker(
        double failureRatio = 0.5, int sampleSize = 4, int breakSeconds = 30)
    {
        var clock = new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);
        var breaker = new PaymentCircuitBreaker(
            failureRatio, sampleSize, TimeSpan.FromSeconds(breakSeconds), () => clock);
        return (breaker, seconds => clock = clock.AddSeconds(seconds));
    }

    [Fact]
    public void ABreaker_StartsClosed_AndLetsCallsThrough()
    {
        var (breaker, _) = NewBreaker();

        var result = breaker.Execute(() => true);

        Assert.Equal(BreakerState.Closed, breaker.State);
        Assert.True(result.Succeeded);
        Assert.False(result.Rejected);
        Assert.Equal(1, breaker.CallsAttempted);
    }

    [Fact]
    public void EnoughFailures_TripTheBreakerOpen()
    {
        var (breaker, _) = NewBreaker(failureRatio: 0.5, sampleSize: 4);

        for (var i = 0; i < 4; i++)
            breaker.Execute(() => false);

        Assert.Equal(BreakerState.Open, breaker.State);
    }

    [Fact]
    public void AnOpenBreaker_RejectsInstantly_WithoutCallingTheDependency()
    {
        var (breaker, _) = NewBreaker(failureRatio: 0.5, sampleSize: 4);
        for (var i = 0; i < 4; i++) breaker.Execute(() => false);
        var attemptedBefore = breaker.CallsAttempted;

        var dependencyWasCalled = false;
        var result = breaker.Execute(() => { dependencyWasCalled = true; return true; });

        Assert.True(result.Rejected);
        Assert.False(dependencyWasCalled);                        // never invoked
        Assert.Equal(attemptedBefore, breaker.CallsAttempted);
        Assert.Equal(1, breaker.CallsRejected);
    }

    // TODO (you): requirement 2 — a 50%-failing service trips the breaker even though it never fails
    //             twice in a row. Hint: alternate true/false over the sample window.

    // TODO (you): requirement 4 — after the cooldown, a successful probe closes it AND resets the
    //             statistics (otherwise the old failures immediately re-trip it); a failed probe
    //             re-opens it for a FULL break duration.

    // TODO (you): requirement 5 — while half-open, exactly ONE call reaches the dependency and the
    //             rest are rejected. Hint: real Threads + a Barrier, as in Exercise 01.

    // TODO (you): requirement 6 — the counters stay correct under concurrent callers.
}
