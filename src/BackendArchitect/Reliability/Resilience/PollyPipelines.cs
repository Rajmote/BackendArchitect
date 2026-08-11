using Polly;
using Polly.CircuitBreaker;
using Polly.Fallback;
using Polly.Retry;
using Polly.Timeout;

namespace BackendArchitect.Reliability.Resilience;

// The same patterns we hand-rolled, expressed with Polly v8 — the standard .NET resilience library.
//
// Polly v8 builds a RESILIENCE PIPELINE: strategies are added outside-in, so the order you add them
// is the order they wrap the call. Everything here is deliberately short-duration so the demo runs fast;
// production values would be seconds, not milliseconds.
public static class PollyPipelines
{
    /// <summary>Retry with exponential backoff AND jitter — the pairing that avoids a thundering herd.</summary>
    public static ResiliencePipeline Retry(Action<int, TimeSpan>? onRetry = null) =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<GatewayUnavailableException>(),
                MaxRetryAttempts = 3,                      // cap it: 3, not 10
                Delay = TimeSpan.FromMilliseconds(50),
                BackoffType = DelayBackoffType.Exponential, // 50ms, 100ms, 200ms
                UseJitter = true,                           // <- the one line most people forget
                OnRetry = args =>
                {
                    // AttemptNumber is 0-based in Polly v8 — the first retry reports 0.
                    onRetry?.Invoke(args.AttemptNumber + 1, args.RetryDelay);
                    return default;
                },
            })
            .Build();

    /// <summary>
    /// A circuit breaker. Note how closely Polly's options match what we built by hand:
    /// FailureRatio + MinimumThroughput over a SamplingDuration is our rolling window.
    /// </summary>
    public static ResiliencePipeline CircuitBreaker(Action<string>? onStateChange = null) =>
        new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<GatewayUnavailableException>(),
                FailureRatio = 0.5,                            // trip at 50% failures...
                MinimumThroughput = 4,                         // ...once we've seen at least 4 calls
                SamplingDuration = TimeSpan.FromSeconds(10),   // within this rolling window
                BreakDuration = TimeSpan.FromMilliseconds(500),// then allow a probe
                OnOpened = args => { onStateChange?.Invoke($"OPENED for {args.BreakDuration.TotalMilliseconds}ms"); return default; },
                OnClosed = _ => { onStateChange?.Invoke("CLOSED - dependency recovered"); return default; },
                OnHalfOpened = _ => { onStateChange?.Invoke("HALF-OPEN - letting one probe through"); return default; },
            })
            .Build();

    /// <summary>Timeout: turns a hanging call back into a fast failure.</summary>
    public static ResiliencePipeline Timeout(TimeSpan timeout) =>
        new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions { Timeout = timeout })
            .Build();

    /// <summary>Fallback: degrade gracefully instead of failing. Needs the GENERIC builder.</summary>
    public static ResiliencePipeline<string> Fallback(string fallbackValue) =>
        new ResiliencePipelineBuilder<string>()
            .AddFallback(new FallbackStrategyOptions<string>
            {
                ShouldHandle = new PredicateBuilder<string>().Handle<GatewayUnavailableException>(),
                FallbackAction = _ => Outcome.FromResultAsValueTask(fallbackValue),
            })
            .Build();

    /// <summary>
    /// The production shape: everything composed. ORDER MATTERS — strategies wrap outside-in, so read
    /// it as "an overall budget, inside which we retry, each attempt passing through the breaker, each
    /// individual try bounded by its own timeout".
    /// </summary>
    public static ResiliencePipeline<string> Full(string fallbackValue) =>
        new ResiliencePipelineBuilder<string>()
            .AddFallback(new FallbackStrategyOptions<string>        // 1. outermost: always return something
            {
                ShouldHandle = new PredicateBuilder<string>()
                    .Handle<GatewayUnavailableException>()
                    .Handle<BrokenCircuitException>()               // the breaker's rejection counts too
                    .Handle<TimeoutRejectedException>(),
                FallbackAction = _ => Outcome.FromResultAsValueTask(fallbackValue),
            })
            .AddTimeout(TimeSpan.FromSeconds(2))                    // 2. overall budget for ALL attempts
            .AddRetry(new RetryStrategyOptions<string>              // 3. retries
            {
                ShouldHandle = new PredicateBuilder<string>().Handle<GatewayUnavailableException>(),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(50),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<string>  // 4. breaker, per attempt
            {
                ShouldHandle = new PredicateBuilder<string>().Handle<GatewayUnavailableException>(),
                FailureRatio = 0.5,
                MinimumThroughput = 4,
                SamplingDuration = TimeSpan.FromSeconds(10),
                BreakDuration = TimeSpan.FromMilliseconds(500),
            })
            .AddTimeout(TimeSpan.FromMilliseconds(500))             // 5. innermost: per-ATTEMPT timeout
            .Build();
}
