using System.Globalization;

namespace BackendArchitect.Reliability.Resilience;

// Example runner: retry amplification, the thundering herd and what jitter does to it, a circuit
// breaker moving through its three states, and a bulkhead containing a hung dependency.
public class ResilienceDemo
{
    public void Run()
    {
        var ic = CultureInfo.InvariantCulture;

        // --- 1. retries amplify load ---
        Console.WriteLine("Retries are load amplification (service already struggling at 1000 req/s):");
        foreach (var attempts in new[] { 1, 3, 5 })
            Console.WriteLine($"  {attempts} attempt(s) per client -> {RetryPolicy.AmplifiedLoad(1000, attempts),5} req/s");

        // --- 2. the thundering herd, and jitter ---
        const int clients = 1000;
        var baseDelay = TimeSpan.FromSeconds(1);
        var random = new Random(Seed: 42);

        var withoutJitter = Enumerable.Range(0, clients)
            .Select(_ => RetryPolicy.ExponentialDelay(attempt: 1, baseDelay).TotalMilliseconds)
            .Distinct().Count();

        var withJitter = Enumerable.Range(0, clients)
            .Select(_ => Math.Round(RetryPolicy.JitteredDelay(1, baseDelay, random.NextDouble()).TotalMilliseconds))
            .Distinct().Count();

        Console.WriteLine();
        Console.WriteLine($"{clients} clients failed at the same instant; when do they retry?");
        Console.WriteLine($"  backoff only    : {withoutJitter,4} distinct moment(s)  <- all at once: thundering herd");
        Console.WriteLine($"  backoff + jitter: {withJitter,4} distinct moments    <- spread out, absorbed comfortably");

        // --- 3. circuit breaker ---
        Console.WriteLine();
        Console.WriteLine("Circuit breaker (threshold 3 failures, 30s break):");
        var clock = new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);
        var breaker = new CircuitBreaker(failureThreshold: 3, TimeSpan.FromSeconds(30), () => clock);
        var dependencyIsHealthy = false;

        for (var call = 1; call <= 5; call++)
        {
            var outcome = breaker.Execute(() => dependencyIsHealthy);
            Console.WriteLine($"  call {call}: {Describe(outcome),-32} state now: {breaker.State}");
        }

        clock = clock.AddSeconds(31);                       // cooldown elapses
        Console.WriteLine($"  ...30s later, state: {breaker.State} (one probe allowed)");

        dependencyIsHealthy = true;                          // the dependency recovers
        var probe = breaker.Execute(() => dependencyIsHealthy);
        Console.WriteLine($"  probe : {Describe(probe),-32} state now: {breaker.State}");
        Console.WriteLine($"  -> {breaker.CallsAttempted} calls reached the dependency, " +
                          $"{breaker.ShortCircuitedCalls} failed instantly without touching it");

        // --- 4. bulkhead ---
        Console.WriteLine();
        Console.WriteLine("Bulkhead (recommendations capped at 5 concurrent calls):");
        var bulkhead = new Bulkhead(maxConcurrency: 5);
        using var hang = new ManualResetEventSlim(false);

        var callers = Enumerable.Range(0, 20)
            .Select(_ => new Thread(() => bulkhead.TryExecute(() => hang.Wait(TimeSpan.FromMilliseconds(200)))))
            .ToList();
        foreach (var t in callers) t.Start();
        foreach (var t in callers) t.Join();

        Console.WriteLine($"  20 callers hit a hanging dependency -> {bulkhead.Executed} admitted, " +
                          $"{bulkhead.Rejected} rejected instantly");
        Console.WriteLine("  -> only 5 threads were ever stuck; checkout and everything else kept working");
    }

    private static string Describe(CallOutcome outcome) =>
        outcome.ShortCircuited ? "REJECTED instantly (no call made)"
        : outcome.Succeeded ? "succeeded"
        : "failed (called the dependency)";
}
