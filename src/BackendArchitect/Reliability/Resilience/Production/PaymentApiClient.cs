using System.Net.Http.Json;

namespace BackendArchitect.Reliability.Resilience.Production;

public sealed record PaymentRequest(string OrderId, decimal Amount);
public sealed record PaymentReceipt(string PaymentId, decimal Amount);

// THIS is the production shape: a typed client with ZERO resilience code in it.
//
// No try/catch loops, no Polly pipeline, no retry counters, no circuit-breaker state. The retries,
// backoff, jitter, circuit breaker and timeouts are attached to this client's HttpClient in DI
// (see ResilienceRegistration) and run inside the HTTP message-handler chain.
//
// That separation is the point: resilience is INFRASTRUCTURE CONFIGURATION, not application logic.
// Business code stays readable, and the policy can be changed (or tuned per environment from config)
// without touching a single line in here.
public sealed class PaymentApiClient
{
    private readonly HttpClient _http;

    public PaymentApiClient(HttpClient http) => _http = http;

    public async Task<PaymentReceipt?> ChargeAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        // One plain call. Everything resilient happens beneath it.
        var response = await _http.PostAsJsonAsync("/payments", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PaymentReceipt>(cancellationToken);
    }
}
