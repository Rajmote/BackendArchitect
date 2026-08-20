namespace BackendArchitect.Practice.Exercise03;

public sealed record Customer(int Id, string Name);
public sealed record Order(int OrderId, string Product, int Quantity);
public sealed record OrderSummary(string CustomerName, int OrderCount, decimal TotalValue);

/// <summary>The three downstream services. Each call takes ~50ms and honours cancellation.</summary>
public sealed class SummaryDataSource
{
    private readonly TimeSpan _latency;

    public SummaryDataSource(TimeSpan latency) => _latency = latency;

    public int CallsMade { get; private set; }

    public async Task<Customer> GetCustomerAsync(int customerId, CancellationToken cancellationToken = default)
    {
        CallsMade++;
        await Task.Delay(_latency, cancellationToken);
        return new Customer(customerId, $"Customer{customerId}");
    }

    public async Task<IReadOnlyList<Order>> GetOrdersAsync(int customerId, CancellationToken cancellationToken = default)
    {
        CallsMade++;
        await Task.Delay(_latency, cancellationToken);
        return [new Order(1, "Latte", 2), new Order(2, "Muffin", 3)];
    }

    public async Task<IReadOnlyDictionary<string, decimal>> GetPricesAsync(CancellationToken cancellationToken = default)
    {
        CallsMade++;
        await Task.Delay(_latency, cancellationToken);
        return new Dictionary<string, decimal> { ["Latte"] = 3.50m, ["Muffin"] = 2.75m };
    }

    public async Task WriteAuditAsync(string message, CancellationToken cancellationToken = default)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken);
        LastAudit = message;
    }

    public string? LastAudit { get; private set; }
}

// ⚠️ THIS CODE IS DELIBERATELY WRONG — it contains all four classic async mistakes.
// Rewrite it per practice/Exercise03-AsyncPatterns.md. The tests describe the target shape.
public sealed class OrderSummaryService
{
    private readonly SummaryDataSource _data;

    public OrderSummaryService(SummaryDataSource data) => _data = data;

    /// <summary>
    /// THIS is the method the tests call — implement it, then delete the broken <see cref="GetSummary"/>
    /// below once you no longer need it for reference.
    /// </summary>
    public Task<OrderSummary> GetSummaryAsync(int customerId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Exercise 03 — implement GetSummaryAsync");

    // The inherited version, kept so you can see exactly what needs fixing.
    public async Task<OrderSummary> GetSummary(int customerId)
    {
        // ❌ mistake 1: .Result blocks a thread (and can deadlock in some contexts)
        // ❌ mistake 2: the three independent calls run one after another
        // ❌ mistake 3: Task.Run around already-async work wastes a pool thread
        // ❌ mistake 4: no CancellationToken anywhere
        var customer = await _data.GetCustomerAsync(customerId);
        var orders = await _data.GetOrdersAsync(customerId);
        var prices = await _data.GetPricesAsync();

        var total = orders.Sum(order => order.Quantity * prices[order.Product]);

        await LogAudit($"summary built for {customer.Name}");

        return new OrderSummary(customer.Name, orders.Count, total);
    }

    // ❌ mistake 5: async void — cannot be awaited, and an exception here would crash the process
    private async Task LogAudit(string message)
    {
        await _data.WriteAuditAsync(message);
    }
}
