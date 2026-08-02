namespace BackendArchitect.Apis.Http.Fundamentals;

// Example runner: the safe/idempotent table, what a retry policy decides, and the double-charge bug
// with and without an idempotency key.
public class HttpFundamentalsDemo
{
    public void Run()
    {
        Console.WriteLine("Method semantics:");
        Console.WriteLine($"  {"method",-9}{"safe",7}{"idempotent",13}   retry a 503?");
        foreach (var method in Enum.GetValues<HttpMethodKind>())
        {
            var safe = HttpSemantics.IsSafe(method) ? "yes" : "no";
            var idem = HttpSemantics.IsIdempotent(method) ? "yes" : "no";
            var retry = RetryPolicy.ShouldRetry(503, method) ? "yes" : "no  <- needs an idempotency key";
            Console.WriteLine($"  {method,-9}{safe,7}{idem,13}   {retry}");
        }

        Console.WriteLine();
        Console.WriteLine("Retry decisions by error status code:");
        foreach (var status in new[] { 400, 404, 408, 429, 500, 501, 503 })
            Console.WriteLine($"  {status} -> {(RetryPolicy.IsTransient(status) ? "transient, retry" : "permanent, do not retry")}");

        Console.WriteLine();
        Console.WriteLine("POST /payments times out; the client retries 3 times:");

        var naive = new NaivePaymentApi();
        for (var attempt = 0; attempt < 3; attempt++)
            naive.Charge(100m);
        Console.WriteLine($"  without idempotency key : customer charged {naive.ChargesExecuted} times  <- BUG");

        var safeApi = new IdempotentPaymentApi();
        var key = "7f3c9a1e-4b2d-4c8a-9f11-2e6d5b8c1a04";  // generated ONCE per logical operation
        PaymentResult? last = null;
        for (var attempt = 0; attempt < 3; attempt++)
            last = safeApi.Charge(key, 100m);
        Console.WriteLine($"  with idempotency key    : customer charged {safeApi.ChargesExecuted} time; " +
                          $"last response replayed={last!.WasReplayed}, id={last.PaymentId}");
    }
}
