using System.Globalization;
using Polly.CircuitBreaker;

namespace BackendArchitect.Reliability.Resilience;

// Example runner: the same patterns we hand-rolled, now with Polly v8 — the standard .NET library.
public class PollyDemo
{
    public void Run()
    {
        var ic = CultureInfo.InvariantCulture;

        // --- retry with backoff + jitter ---
        Console.WriteLine("Retry (3 attempts, exponential + jitter) against a gateway that fails twice:");
        var flaky = new FlakyPaymentGateway(failuresBeforeRecovery: 2);
        var retry = PollyPipelines.Retry((attempt, delay) =>
            Console.WriteLine($"    call failed -> retry {attempt} in {delay.TotalMilliseconds.ToString("0", ic)}ms (jittered)"));

        var result = retry.Execute(() => flaky.Charge());
        Console.WriteLine($"  result: {result}  (gateway hit {flaky.CallsReceived} times)");

        // --- circuit breaker ---
        Console.WriteLine();
        Console.WriteLine("Circuit breaker (50% failures over >=4 calls, 500ms break) against a dead gateway:");
        var dead = new FlakyPaymentGateway(failuresBeforeRecovery: int.MaxValue);
        var breaker = PollyPipelines.CircuitBreaker(state => Console.WriteLine($"    breaker {state}"));

        for (var i = 1; i <= 6; i++)
        {
            try
            {
                breaker.Execute(() => dead.AlwaysFail());
            }
            catch (BrokenCircuitException)
            {
                Console.WriteLine($"  call {i}: REJECTED by the breaker (gateway not called)");
                continue;
            }
            catch (GatewayUnavailableException)
            {
                Console.WriteLine($"  call {i}: failed at the gateway");
            }
        }

        Console.WriteLine($"  -> 6 calls made, gateway was only hit {dead.CallsReceived} times");

        // --- fallback ---
        Console.WriteLine();
        Console.WriteLine("Fallback — degrade gracefully instead of throwing:");
        var down = new FlakyPaymentGateway(failuresBeforeRecovery: int.MaxValue);
        var fallback = PollyPipelines.Fallback("payment queued for later");
        Console.WriteLine($"  result: {fallback.Execute(() => down.AlwaysFail())}");

        // --- the full pipeline ---
        Console.WriteLine();
        Console.WriteLine("The full pipeline (fallback > timeout > retry > breaker > per-try timeout):");
        var recovering = new FlakyPaymentGateway(failuresBeforeRecovery: 2);
        var pipeline = PollyPipelines.Full("payment queued for later");
        Console.WriteLine($"  flaky gateway  : {pipeline.Execute(() => recovering.Charge())}");

        var permanentlyDown = new FlakyPaymentGateway(failuresBeforeRecovery: int.MaxValue);
        Console.WriteLine($"  dead gateway   : {pipeline.Execute(() => permanentlyDown.AlwaysFail())}");
        Console.WriteLine("  -> the caller always gets an answer; it never sees an exception");
    }
}
