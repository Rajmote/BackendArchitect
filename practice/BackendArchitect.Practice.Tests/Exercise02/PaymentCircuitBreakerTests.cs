using BackendArchitect.Practice.Exercise02;

namespace BackendArchitect.Practice.Tests.Exercise02;

// Exercise 02 — circuit breaker.
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

    // --- requirement 1: closed by default ---

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

    // --- requirement 2: trips on the failure RATIO over a rolling window ---

    [Fact]
    public void EnoughFailures_TripTheBreakerOpen()
    {
        var (breaker, _) = NewBreaker(failureRatio: 0.5, sampleSize: 4);

        for (var i = 0; i < 4; i++)
            breaker.Execute(() => false);

        Assert.Equal(BreakerState.Open, breaker.State);
    }

    [Fact]
    public void AServiceFailingHalfTheTime_Trips_EvenWithoutTwoFailuresInARow()
    {
        var (breaker, _) = NewBreaker(failureRatio: 0.5, sampleSize: 4);

        breaker.Execute(() => false);   // fail
        breaker.Execute(() => true);    // ok    <- never two failures in a row...
        breaker.Execute(() => false);   // fail
        breaker.Execute(() => true);    // ok

        Assert.Equal(BreakerState.Open, breaker.State);   // ...but 50% failed: broken
    }

    [Fact]
    public void ASingleFailure_DoesNotTrip_BeforeThereIsEnoughEvidence()
    {
        var (breaker, _) = NewBreaker(failureRatio: 0.5, sampleSize: 4);

        breaker.Execute(() => false);

        Assert.Equal(BreakerState.Closed, breaker.State);   // 1 of 4 is not a verdict
    }

    [Fact]
    public void AMostlyHealthyService_StaysClosed()
    {
        var (breaker, _) = NewBreaker(failureRatio: 0.5, sampleSize: 4);

        breaker.Execute(() => false);
        for (var i = 0; i < 3; i++) breaker.Execute(() => true);

        Assert.Equal(BreakerState.Closed, breaker.State);   // 25% failure — acceptable
    }

    // --- requirement 3: open rejects instantly ---

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

    // --- requirement 4: half-open transitions ---

    [Fact]
    public void AfterTheBreakDuration_TheBreakerBecomesHalfOpen()
    {
        var (breaker, advance) = NewBreaker(breakSeconds: 30);
        for (var i = 0; i < 4; i++) breaker.Execute(() => false);

        advance(31);

        Assert.Equal(BreakerState.HalfOpen, breaker.State);
    }

    [Fact]
    public void ASuccessfulProbe_ClosesTheBreaker_AndResetsTheStatistics()
    {
        var (breaker, advance) = NewBreaker(failureRatio: 0.5, sampleSize: 4, breakSeconds: 30);
        for (var i = 0; i < 4; i++) breaker.Execute(() => false);
        advance(31);

        breaker.Execute(() => true);                       // the probe succeeds
        Assert.Equal(BreakerState.Closed, breaker.State);

        // If the old failures had been kept, this single failure would re-trip it immediately.
        breaker.Execute(() => false);
        Assert.Equal(BreakerState.Closed, breaker.State);
    }

    [Fact]
    public void AFailedProbe_ReopensTheBreakerForAFullBreakDuration()
    {
        var (breaker, advance) = NewBreaker(breakSeconds: 30);
        for (var i = 0; i < 4; i++) breaker.Execute(() => false);
        advance(31);

        breaker.Execute(() => false);                      // the probe fails

        Assert.Equal(BreakerState.Open, breaker.State);
        advance(20);
        Assert.Equal(BreakerState.Open, breaker.State);    // the cooldown restarted, not resumed
        advance(11);
        Assert.Equal(BreakerState.HalfOpen, breaker.State);
    }

    // --- requirement 5: exactly one probe ---

    [Fact]
    public void WhileHalfOpen_ExactlyOneCallReachesTheDependency()
    {
        const int callers = 10;
        var (breaker, advance) = NewBreaker(breakSeconds: 30);
        for (var i = 0; i < 4; i++) breaker.Execute(() => false);
        advance(31);                                        // now half-open

        var dependencyCalls = 0;
        var results = new BreakerResult[callers];

        // The probe must still be IN FLIGHT while the others arrive — otherwise it completes, closes
        // the breaker, and the rest legitimately pass through a closed breaker (which is correct
        // behaviour, just not what this test is about). So the probe blocks until the others are done.
        using var atTheGate = new Barrier(callers);
        using var othersFinished = new CountdownEvent(callers - 1);

        var threads = Enumerable.Range(0, callers)
            .Select(i => new Thread(() =>
            {
                atTheGate.SignalAndWait();
                results[i] = breaker.Execute(() =>
                {
                    Interlocked.Increment(ref dependencyCalls);
                    othersFinished.Wait(TimeSpan.FromSeconds(5));   // hold the probe open
                    return true;
                });

                if (!results[i].Rejected) return;                  // the probe itself doesn't signal
                othersFinished.Signal();
            }))
            .ToList();

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        Assert.Equal(1, dependencyCalls);                          // only the probe got through
        Assert.Equal(callers - 1, results.Count(r => r.Rejected)); // the other nine were refused
    }

    // --- requirement 6: thread safety ---

    [Fact]
    public void CountersStayConsistent_UnderConcurrentCallers()
    {
        const int callers = 50;
        var (breaker, _) = NewBreaker(failureRatio: 0.5, sampleSize: 4);

        Parallel.For(0, callers, _ => breaker.Execute(() => true));

        Assert.Equal(callers, breaker.CallsAttempted + breaker.CallsRejected);
        Assert.Equal(BreakerState.Closed, breaker.State);   // all succeeded, so it never trips
    }

    [Fact]
    public void ABreakerUnderConcurrentFailures_TripsExactlyOnce_AndThenRejects()
    {
        var (breaker, _) = NewBreaker(failureRatio: 0.5, sampleSize: 4);

        Parallel.For(0, 100, _ => breaker.Execute(() => false));

        Assert.Equal(BreakerState.Open, breaker.State);
        Assert.True(breaker.CallsRejected > 0, "once open, later calls must be rejected");
        Assert.Equal(100, breaker.CallsAttempted + breaker.CallsRejected);
    }
}
