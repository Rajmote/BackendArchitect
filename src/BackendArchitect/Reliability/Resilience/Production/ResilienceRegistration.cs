using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace BackendArchitect.Reliability.Resilience.Production;

/// <summary>Tunable resilience settings — bind these from appsettings.json per environment.</summary>
public sealed class PaymentApiOptions
{
    public Uri BaseAddress { get; init; } = new("https://payments.example.com");
    public int MaxRetryAttempts { get; init; } = 3;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(200);
    public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan TotalRequestTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public double CircuitBreakerFailureRatio { get; init; } = 0.5;
    public TimeSpan CircuitBreakerSamplingDuration { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan CircuitBreakerBreakDuration { get; init; } = TimeSpan.FromSeconds(15);
}

// How resilience is wired up in a real application: once, at startup, next to the HttpClient it
// protects — never scattered through the call sites.
public static class ResilienceRegistration
{
    /// <summary>
    /// The 90% answer: <c>AddStandardResilienceHandler()</c>. One line gives you Microsoft's
    /// recommended pipeline — rate limiter, total timeout, retry with exponential backoff + jitter,
    /// circuit breaker, and a per-attempt timeout — already correctly ordered.
    /// </summary>
    public static IServiceCollection AddPaymentApiClient(this IServiceCollection services, PaymentApiOptions options)
    {
        services
            .AddHttpClient<PaymentApiClient>(client => client.BaseAddress = options.BaseAddress)
            .AddStandardResilienceHandler(resilience =>
            {
                // Retry: capped, exponential, jittered.
                resilience.Retry.MaxRetryAttempts = options.MaxRetryAttempts;
                resilience.Retry.Delay = options.RetryDelay;
                resilience.Retry.BackoffType = DelayBackoffType.Exponential;
                resilience.Retry.UseJitter = true;

                // Circuit breaker: trip on a failure RATIO measured over a rolling window.
                resilience.CircuitBreaker.FailureRatio = options.CircuitBreakerFailureRatio;
                resilience.CircuitBreaker.SamplingDuration = options.CircuitBreakerSamplingDuration;
                resilience.CircuitBreaker.BreakDuration = options.CircuitBreakerBreakDuration;

                // Two timeouts, and the distinction matters:
                //   AttemptTimeout      - bounds ONE try
                //   TotalRequestTimeout - the budget for the whole operation INCLUDING retries
                resilience.AttemptTimeout.Timeout = options.AttemptTimeout;
                resilience.TotalRequestTimeout.Timeout = options.TotalRequestTimeout;
            });

        return services;
    }

    /// <summary>
    /// For work that isn't an HttpClient (a database call, a queue publish), register a NAMED pipeline
    /// and inject <see cref="ResiliencePipelineProvider{TKey}"/> to fetch it.
    /// </summary>
    public static IServiceCollection AddDatabaseResiliencePipeline(this IServiceCollection services) =>
        services.AddResiliencePipeline("database", builder => builder
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(100),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            })
            .AddTimeout(TimeSpan.FromSeconds(5)));
}
