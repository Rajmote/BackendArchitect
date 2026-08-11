using System.Net;
using BackendArchitect.Reliability.Resilience.Production;
using Microsoft.Extensions.DependencyInjection;

namespace BackendArchitect.Tests.Reliability;

// Reliability · Resilience · Production — testing the DI-wired HttpClient pipeline.
// This is also the pattern for testing resilience config for real: swap the primary handler for a fake
// server, and every strategy above it still runs.
public class ProductionResilienceTests
{
    private static PaymentApiClient BuildClient(HttpMessageHandler server)
    {
        var services = new ServiceCollection();
        services.AddPaymentApiClient(new PaymentApiOptions { RetryDelay = TimeSpan.FromMilliseconds(10) });
        services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => server));
        return services.BuildServiceProvider().GetRequiredService<PaymentApiClient>();
    }

    private static PaymentRequest ARequest() => new("order-1", 25.00m);

    [Fact]
    public async Task ATransientFailure_IsRetried_AndTheCallerNeverSeesIt()
    {
        var server = new FlakyServerHandler(failuresBeforeRecovery: 2);   // two 503s, then OK
        var client = BuildClient(server);

        var receipt = await client.ChargeAsync(ARequest());

        Assert.NotNull(receipt);
        Assert.Equal(3, server.RequestsReceived);   // 1 original + 2 retries
    }

    [Fact]
    public async Task AClientError_IsNotRetried_BecauseRepeatingItCannotHelp()
    {
        var server = new FlakyServerHandler(failuresBeforeRecovery: int.MaxValue, HttpStatusCode.BadRequest);
        var client = BuildClient(server);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.ChargeAsync(ARequest()));

        Assert.Equal(1, server.RequestsReceived);   // 400 = fix your request; retrying is pointless
    }

    [Fact]
    public async Task RetriesAreCapped_SoAFailingServerIsNotHammeredForever()
    {
        var server = new FlakyServerHandler(failuresBeforeRecovery: int.MaxValue);
        var client = BuildClient(server);

        await Assert.ThrowsAnyAsync<Exception>(() => client.ChargeAsync(ARequest()));

        Assert.InRange(server.RequestsReceived, 1, 4);   // 1 original + at most 3 retries
    }

    [Fact]
    public async Task TheClientReturnsTheServersPayload_WhenAllIsWell()
    {
        var server = new FlakyServerHandler(failuresBeforeRecovery: 0);
        var client = BuildClient(server);

        var receipt = await client.ChargeAsync(ARequest());

        Assert.Equal("pay-1", receipt!.PaymentId);
        Assert.Equal(1, server.RequestsReceived);   // no retries needed
    }

    [Fact]
    public void TheClientIsResolvableFromDi_WithResilienceAlreadyAttached()
    {
        var services = new ServiceCollection();
        services.AddPaymentApiClient(new PaymentApiOptions());

        var client = services.BuildServiceProvider().GetRequiredService<PaymentApiClient>();

        Assert.NotNull(client);   // registration is valid: option constraints all satisfied
    }
}
