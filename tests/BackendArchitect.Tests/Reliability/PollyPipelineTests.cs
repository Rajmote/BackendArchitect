using BackendArchitect.Reliability.Resilience;
using Polly.CircuitBreaker;

namespace BackendArchitect.Tests.Reliability;

// Reliability · Resilience with Polly — testing OUR configuration, not Polly itself.
public class PollyPipelineTests
{
    [Fact]
    public void Retry_KeepsTryingUntilTheGatewayRecovers()
    {
        var gateway = new FlakyPaymentGateway(failuresBeforeRecovery: 2);
        var pipeline = PollyPipelines.Retry();

        var result = pipeline.Execute(() => gateway.Charge());

        Assert.Equal("charged on call 3", result);
        Assert.Equal(3, gateway.CallsReceived);   // 1 original + 2 retries
    }

    [Fact]
    public void Retry_GivesUpAfterTheConfiguredNumberOfAttempts()
    {
        var gateway = new FlakyPaymentGateway(failuresBeforeRecovery: int.MaxValue);
        var pipeline = PollyPipelines.Retry();

        Assert.Throws<GatewayUnavailableException>(() => pipeline.Execute(() => gateway.AlwaysFail()));
        Assert.Equal(4, gateway.CallsReceived);   // capped: 1 original + 3 retries, not forever
    }

    [Fact]
    public void Retry_UsesJitter_SoDelaysDiffer()
    {
        var delays = new List<TimeSpan>();
        var pipeline = PollyPipelines.Retry((_, delay) => delays.Add(delay));
        var gateway = new FlakyPaymentGateway(failuresBeforeRecovery: int.MaxValue);

        try { pipeline.Execute(() => gateway.AlwaysFail()); }
        catch (GatewayUnavailableException) { /* expected */ }

        Assert.Equal(3, delays.Count);
        Assert.True(delays[1] > delays[0], "exponential: each wait should grow");
        // With jitter the values are not the exact 50/100/200 powers of two.
        Assert.DoesNotContain(delays, d => d == TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public void CircuitBreaker_OpensAndThenStopsCallingTheGateway()
    {
        var gateway = new FlakyPaymentGateway(failuresBeforeRecovery: int.MaxValue);
        var pipeline = PollyPipelines.CircuitBreaker();
        var rejected = 0;

        for (var i = 0; i < 8; i++)
        {
            try { pipeline.Execute(() => gateway.AlwaysFail()); }
            catch (BrokenCircuitException) { rejected++; }        // refused without calling
            catch (GatewayUnavailableException) { /* reached the gateway and failed */ }
        }

        Assert.True(rejected > 0, "the breaker should start rejecting once it trips");
        Assert.True(gateway.CallsReceived < 8, $"the gateway was shielded; it saw {gateway.CallsReceived} of 8");
    }

    [Fact]
    public void CircuitBreaker_ReportsItsStateTransitions()
    {
        var transitions = new List<string>();
        var gateway = new FlakyPaymentGateway(failuresBeforeRecovery: int.MaxValue);
        var pipeline = PollyPipelines.CircuitBreaker(transitions.Add);

        for (var i = 0; i < 6; i++)
        {
            try { pipeline.Execute(() => gateway.AlwaysFail()); }
            catch (Exception) { /* both failure kinds are expected here */ }
        }

        Assert.Contains(transitions, t => t.StartsWith("OPENED", StringComparison.Ordinal));
    }

    [Fact]
    public void Fallback_ReturnsAUsableValue_InsteadOfThrowing()
    {
        var gateway = new FlakyPaymentGateway(failuresBeforeRecovery: int.MaxValue);
        var pipeline = PollyPipelines.Fallback("payment queued for later");

        var result = pipeline.Execute(() => gateway.AlwaysFail());

        Assert.Equal("payment queued for later", result);
    }

    [Fact]
    public void TheFullPipeline_RecoversFromATransientFailure()
    {
        var gateway = new FlakyPaymentGateway(failuresBeforeRecovery: 2);
        var pipeline = PollyPipelines.Full("payment queued for later");

        var result = pipeline.Execute(() => gateway.Charge());

        Assert.Equal("charged on call 3", result);   // the retry rode out the blip
    }

    [Fact]
    public void TheFullPipeline_FallsBack_WhenTheGatewayIsPermanentlyDown()
    {
        var gateway = new FlakyPaymentGateway(failuresBeforeRecovery: int.MaxValue);
        var pipeline = PollyPipelines.Full("payment queued for later");

        var result = pipeline.Execute(() => gateway.AlwaysFail());

        Assert.Equal("payment queued for later", result);   // the caller never sees an exception
    }

    [Fact]
    public void Timeout_TurnsAHangingCallIntoAFastFailure()
    {
        var pipeline = PollyPipelines.Timeout(TimeSpan.FromMilliseconds(100));

        Assert.ThrowsAny<Exception>(() =>
            pipeline.Execute(token => Task.Delay(TimeSpan.FromSeconds(5), token).GetAwaiter().GetResult()));
    }
}
