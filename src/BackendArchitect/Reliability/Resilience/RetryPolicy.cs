namespace BackendArchitect.Reliability.Resilience;

// Retries help with a momentary blip and actively HARM an overloaded service, because they are load
// amplification. Two mistakes cause most retry-driven outages:
//
//   1. no cap          -> 1000 req/s becomes 4000 req/s exactly when the service can least cope
//   2. no jitter       -> every client waits the SAME delay and they all return in lockstep
//                         (the "thundering herd"), knocking over a service that was recovering
public static class RetryPolicy
{
    /// <summary>Total requests a struggling service receives once every client retries.</summary>
    public static int AmplifiedLoad(int requestsPerSecond, int maxAttempts) =>
        requestsPerSecond * maxAttempts;

    /// <summary>Exponential backoff: 1s, 2s, 4s, 8s... giving the dependency room to recover.</summary>
    public static TimeSpan ExponentialDelay(int attempt, TimeSpan baseDelay) =>
        baseDelay * Math.Pow(2, attempt);

    /// <summary>
    /// Backoff spreads retries out in TIME; jitter spreads them out across CLIENTS. Without jitter a
    /// thousand clients that failed together also retry together, forever.
    /// </summary>
    /// <param name="random">A value in [0,1). Injected so tests are deterministic.</param>
    public static TimeSpan JitteredDelay(int attempt, TimeSpan baseDelay, double random) =>
        ExponentialDelay(attempt, baseDelay) * (0.5 + random * 0.5);

    /// <summary>
    /// A retry is only safe when the failure is transient AND repeating the operation is harmless
    /// (see Apis/Http/Fundamentals: retry safety = transient status AND idempotent operation).
    /// </summary>
    public static bool ShouldRetry(bool isTransient, bool isIdempotent, bool hasIdempotencyKey = false) =>
        isTransient && (isIdempotent || hasIdempotencyKey);
}
