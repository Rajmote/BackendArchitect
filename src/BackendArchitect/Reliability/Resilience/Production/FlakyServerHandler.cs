using System.Net;
using System.Net.Http.Json;

namespace BackendArchitect.Reliability.Resilience.Production;

// A stand-in for the remote payment API, plugged in as the innermost HTTP message handler so the demo
// and tests run offline. This is also how you'd test resilience config for real: swap the primary
// handler for a fake that returns whatever failure sequence you want to prove.
public sealed class FlakyServerHandler : HttpMessageHandler
{
    private readonly int _failuresBeforeRecovery;
    private readonly HttpStatusCode _failureStatus;
    private int _requests;

    /// <param name="failureStatus">
    /// 503 is transient so the pipeline retries it; 400 is a client error and must NOT be retried.
    /// </param>
    public FlakyServerHandler(int failuresBeforeRecovery, HttpStatusCode failureStatus = HttpStatusCode.ServiceUnavailable)
    {
        _failuresBeforeRecovery = failuresBeforeRecovery;
        _failureStatus = failureStatus;
    }

    /// <summary>How many requests actually reached the "server" — i.e. what the breaker did NOT shield.</summary>
    public int RequestsReceived => Volatile.Read(ref _requests);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var attempt = Interlocked.Increment(ref _requests);

        if (attempt <= _failuresBeforeRecovery)
            return Task.FromResult(new HttpResponseMessage(_failureStatus));

        var receipt = new PaymentReceipt($"pay-{attempt}", 25.00m);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(receipt),
        });
    }
}
