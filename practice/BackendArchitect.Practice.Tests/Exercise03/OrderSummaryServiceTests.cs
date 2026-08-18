using System.Diagnostics;
using BackendArchitect.Practice.Exercise03;

namespace BackendArchitect.Practice.Tests.Exercise03;

// Exercise 03 — three starter tests showing the target shape.
//
// They call `GetSummaryAsync(customerId, cancellationToken)`, which DOESN'T EXIST YET — that's the
// point: the tests describe the API you're being asked to write, so they won't compile until you
// rename and reshape the method.
//
// YOUR JOB: make these pass, then add tests for:
//   * concurrency  — the three fetches overlap, so it's much faster than 3 x latency
//   * cancellation — a cancelled token stops the work
public class OrderSummaryServiceTests
{
    private static readonly TimeSpan Latency = TimeSpan.FromMilliseconds(50);

    private static (OrderSummaryService Service, SummaryDataSource Data) NewService()
    {
        var data = new SummaryDataSource(Latency);
        return (new OrderSummaryService(data), data);
    }

    [Fact]
    public async Task ItReturnsTheCustomerNameAndOrderCount()
    {
        var (service, _) = NewService();

        var summary = await service.GetSummaryAsync(customerId: 10);

        Assert.Equal("Customer10", summary.CustomerName);
        Assert.Equal(2, summary.OrderCount);
    }

    [Fact]
    public async Task ItTotalsQuantityTimesPrice()
    {
        var (service, _) = NewService();

        var summary = await service.GetSummaryAsync(customerId: 10);

        // 2 lattes @ 3.50 + 3 muffins @ 2.75 = 7.00 + 8.25
        Assert.Equal(15.25m, summary.TotalValue);
    }

    [Fact]
    public async Task ItWritesAnAuditEntry_AndWaitsForIt()
    {
        var (service, data) = NewService();

        await service.GetSummaryAsync(customerId: 10);

        // With async void this would usually still be null when we get here.
        Assert.NotNull(data.LastAudit);
        Assert.Contains("Customer10", data.LastAudit);
    }

    // TODO (you): concurrency — three 50ms calls overlapped should finish well under 150ms.
    //             Hint: Stopwatch, and assert something like elapsed < 120ms.

    // TODO (you): cancellation — pass an already-cancelled token and assert it throws.
    //             Hint: await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ...)
}
