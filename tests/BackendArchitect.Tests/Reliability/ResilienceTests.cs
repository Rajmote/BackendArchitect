using BackendArchitect.Reliability.Resilience;

namespace BackendArchitect.Tests.Reliability;

// Reliability · Resilience — retry amplification, jitter, the circuit-breaker state machine, bulkheads.
public class ResilienceTests
{
    // --- retries ---

    [Fact]
    public void Retries_MultiplyTheLoadOnAStrugglingService()
    {
        Assert.Equal(1000, RetryPolicy.AmplifiedLoad(1000, maxAttempts: 1));
        Assert.Equal(4000, RetryPolicy.AmplifiedLoad(1000, maxAttempts: 4));
    }

    [Fact]
    public void ExponentialBackoff_DoublesEachAttempt()
    {
        var baseDelay = TimeSpan.FromSeconds(1);

        Assert.Equal(TimeSpan.FromSeconds(1), RetryPolicy.ExponentialDelay(0, baseDelay));
        Assert.Equal(TimeSpan.FromSeconds(2), RetryPolicy.ExponentialDelay(1, baseDelay));
        Assert.Equal(TimeSpan.FromSeconds(4), RetryPolicy.ExponentialDelay(2, baseDelay));
    }

    [Fact]
    public void WithoutJitter_EveryClientRetriesAtExactlyTheSameMoment() // the thundering herd
    {
        var baseDelay = TimeSpan.FromSeconds(1);

        var delays = Enumerable.Range(0, 100)
            .Select(_ => RetryPolicy.ExponentialDelay(1, baseDelay))
            .Distinct()
            .ToList();

        Assert.Single(delays);   // 100 clients, one moment -> they arrive as a wall
    }

    [Fact]
    public void Jitter_SpreadsRetriesAcrossClients()
    {
        var baseDelay = TimeSpan.FromSeconds(1);
        var random = new Random(Seed: 42);

        var delays = Enumerable.Range(0, 100)
            .Select(_ => RetryPolicy.JitteredDelay(1, baseDelay, random.NextDouble()))
            .Distinct()
            .ToList();

        Assert.True(delays.Count > 90, $"expected a wide spread, got {delays.Count} distinct delays");
    }

    [Fact]
    public void JitteredDelay_StaysWithinHalfToFullOfTheBackoff()
    {
        var baseDelay = TimeSpan.FromSeconds(1);   // attempt 1 -> 2s backoff

        var earliest = RetryPolicy.JitteredDelay(1, baseDelay, random: 0.0);
        var latest = RetryPolicy.JitteredDelay(1, baseDelay, random: 1.0);

        Assert.Equal(TimeSpan.FromSeconds(1), earliest);   // never less than half
        Assert.Equal(TimeSpan.FromSeconds(2), latest);     // never more than the full backoff
    }

    [Theory]
    [InlineData(true, true, false, true)]    // transient + idempotent -> retry
    [InlineData(false, true, false, false)]  // permanent -> never
    [InlineData(true, false, false, false)]  // transient but not idempotent -> unsafe
    [InlineData(true, false, true, true)]    // ...unless an idempotency key makes it safe
    public void ShouldRetry_NeedsBothATransientFailureAndARepeatableOperation(
        bool transient, bool idempotent, bool key, bool expected)
    {
        Assert.Equal(expected, RetryPolicy.ShouldRetry(transient, idempotent, key));
    }

    // --- circuit breaker ---

    private static (CircuitBreaker Breaker, Func<DateTimeOffset> Now, Action<int> Advance) NewBreaker(
        int threshold = 3, int breakSeconds = 30)
    {
        var clock = new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);
        var breaker = new CircuitBreaker(threshold, TimeSpan.FromSeconds(breakSeconds), () => clock);
        return (breaker, () => clock, seconds => clock = clock.AddSeconds(seconds));
    }

    [Fact]
    public void ABreaker_StartsClosed_AndLetsCallsThrough()
    {
        var (breaker, _, _) = NewBreaker();

        var outcome = breaker.Execute(() => true);

        Assert.Equal(CircuitState.Closed, breaker.State);
        Assert.True(outcome.Succeeded);
        Assert.Equal(1, breaker.CallsAttempted);
    }

    [Fact]
    public void ReachingTheFailureThreshold_TripsTheBreakerOpen()
    {
        var (breaker, _, _) = NewBreaker(threshold: 3);

        for (var i = 0; i < 3; i++)
            breaker.Execute(() => false);

        Assert.Equal(CircuitState.Open, breaker.State);
    }

    [Fact]
    public void AnOpenBreaker_FailsInstantlyWithoutCallingTheDependency()
    {
        var (breaker, _, _) = NewBreaker(threshold: 3);
        for (var i = 0; i < 3; i++) breaker.Execute(() => false);
        var attemptedBefore = breaker.CallsAttempted;

        var outcome = breaker.Execute(() => true);   // would have succeeded — but is never called

        Assert.True(outcome.ShortCircuited);
        Assert.False(outcome.Succeeded);
        Assert.Equal(attemptedBefore, breaker.CallsAttempted);   // the dependency was NOT touched
        Assert.Equal(1, breaker.ShortCircuitedCalls);
    }

    [Fact]
    public void ASuccessBeforeTheThreshold_ClearsTheFailureStreak()
    {
        var (breaker, _, _) = NewBreaker(threshold: 3);

        breaker.Execute(() => false);
        breaker.Execute(() => false);
        breaker.Execute(() => true);    // recovery resets the count
        breaker.Execute(() => false);
        breaker.Execute(() => false);

        Assert.Equal(CircuitState.Closed, breaker.State);   // never reached 3 consecutive
    }

    [Fact]
    public void AfterTheBreakDuration_TheBreakerBecomesHalfOpen()
    {
        var (breaker, _, advance) = NewBreaker(threshold: 3, breakSeconds: 30);
        for (var i = 0; i < 3; i++) breaker.Execute(() => false);

        advance(31);

        Assert.Equal(CircuitState.HalfOpen, breaker.State);
    }

    [Fact]
    public void ASuccessfulProbe_ClosesTheBreaker()
    {
        var (breaker, _, advance) = NewBreaker(threshold: 3, breakSeconds: 30);
        for (var i = 0; i < 3; i++) breaker.Execute(() => false);
        advance(31);

        var probe = breaker.Execute(() => true);   // the dependency has recovered

        Assert.True(probe.Succeeded);
        Assert.Equal(CircuitState.Closed, breaker.State);
    }

    [Fact]
    public void AFailedProbe_ReopensTheBreakerForAnotherFullPeriod()
    {
        var (breaker, _, advance) = NewBreaker(threshold: 3, breakSeconds: 30);
        for (var i = 0; i < 3; i++) breaker.Execute(() => false);
        advance(31);

        breaker.Execute(() => false);              // still broken

        Assert.Equal(CircuitState.Open, breaker.State);
        advance(10);
        Assert.Equal(CircuitState.Open, breaker.State);   // the cooldown restarted
    }

    // --- bulkhead ---

    [Fact]
    public void ABulkhead_AdmitsUpToItsLimit_AndRejectsTheRest()
    {
        var bulkhead = new Bulkhead(maxConcurrency: 5);
        using var hold = new ManualResetEventSlim(false);

        var threads = Enumerable.Range(0, 20)
            .Select(_ => new Thread(() => bulkhead.TryExecute(() => hold.Wait(TimeSpan.FromMilliseconds(300)))))
            .ToList();
        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        Assert.True(bulkhead.Rejected > 0, "a hung dependency must not consume every thread");
        Assert.Equal(20, bulkhead.Executed + bulkhead.Rejected);
    }

    [Fact]
    public void ABulkhead_ReleasesCapacityWhenACallCompletes()
    {
        var bulkhead = new Bulkhead(maxConcurrency: 1);

        Assert.True(bulkhead.TryExecute(() => { }));
        Assert.True(bulkhead.TryExecute(() => { }));   // the first one finished, so there is room

        Assert.Equal(2, bulkhead.Executed);
        Assert.Equal(0, bulkhead.Rejected);
        Assert.Equal(0, bulkhead.InFlight);
    }
}
