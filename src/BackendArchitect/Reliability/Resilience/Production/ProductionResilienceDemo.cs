using Microsoft.Extensions.DependencyInjection;

namespace BackendArchitect.Reliability.Resilience.Production;

// Example runner: resilience configured once in DI, with application code that contains none of it.
public class ProductionResilienceDemo
{
    public void Run()
    {
        Console.WriteLine("PaymentApiClient.ChargeAsync() contains ZERO resilience code —");
        Console.WriteLine("retries, jitter, breaker and timeouts live in the DI registration.");
        Console.WriteLine();

        // A server that fails twice with 503 and then recovers.
        var server = new FlakyServerHandler(failuresBeforeRecovery: 2);
        var client = BuildClient(server);

        var receipt = client.ChargeAsync(new PaymentRequest("order-1", 25.00m)).GetAwaiter().GetResult();

        Console.WriteLine($"  caller sees      : success, payment {receipt!.PaymentId}");
        Console.WriteLine($"  server received  : {server.RequestsReceived} requests (1 original + 2 retries)");
        Console.WriteLine("  -> the caller never knew anything went wrong");

        Console.WriteLine();
        Console.WriteLine("AddStandardResilienceHandler() gives you, correctly ordered:");
        Console.WriteLine("    rate limiter -> total timeout -> retry -> circuit breaker -> attempt timeout");
        Console.WriteLine("  and it is tunable from appsettings.json per environment.");
    }

    /// <summary>Builds the real DI container, swapping only the innermost handler for the fake server.</summary>
    private static PaymentApiClient BuildClient(HttpMessageHandler server)
    {
        var services = new ServiceCollection();
        services.AddPaymentApiClient(new PaymentApiOptions
        {
            RetryDelay = TimeSpan.FromMilliseconds(20),   // keep the demo fast
        });

        // Replace the network with our fake server; every resilience strategy still runs above it.
        services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => server));

        return services.BuildServiceProvider().GetRequiredService<PaymentApiClient>();
    }
}
