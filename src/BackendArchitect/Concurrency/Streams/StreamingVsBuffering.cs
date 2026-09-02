using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace BackendArchitect.Concurrency.Streams;

public sealed record OrderRow(int Id, string Product);

// Task<List<T>> vs IAsyncEnumerable<T>.
//
// BOTH are async — neither blocks a thread. The axis is BATCH vs STREAM:
//   Task<List<T>>        materialises every row before the caller sees anything
//   IAsyncEnumerable<T>  yields each row as it arrives -> flat memory, instant first item
public sealed class OrderReader
{
    private readonly int _rowCount;
    private readonly TimeSpan _perRowLatency;

    public OrderReader(int rowCount, TimeSpan perRowLatency)
    {
        _rowCount = rowCount;
        _perRowLatency = perRowLatency;
    }

    /// <summary>Highest number of rows held in memory at once — the cost signal here.</summary>
    public int PeakRowsInMemory { get; private set; }

    /// <summary>❌ Buffers everything: peak memory = the whole result set.</summary>
    public async Task<List<OrderRow>> BufferAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = new List<OrderRow>();
        for (var i = 1; i <= _rowCount; i++)
        {
            await Task.Delay(_perRowLatency, cancellationToken);
            rows.Add(new OrderRow(i, "Latte"));
            PeakRowsInMemory = Math.Max(PeakRowsInMemory, rows.Count);
        }

        return rows;   // the caller has waited for ALL rows before getting anything
    }

    /// <summary>✅ Streams: one row is alive at a time, and the first arrives immediately.</summary>
    public async IAsyncEnumerable<OrderRow> StreamAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var i = 1; i <= _rowCount; i++)
        {
            await Task.Delay(_perRowLatency, cancellationToken);
            PeakRowsInMemory = Math.Max(PeakRowsInMemory, 1);
            yield return new OrderRow(i, "Latte");   // handed to the caller now, not at the end
        }
    }

    /// <summary>How long until the consumer can act on the FIRST row.</summary>
    public static async Task<long> TimeToFirstRowWhenBufferedAsync(OrderReader reader)
    {
        var stopwatch = Stopwatch.StartNew();
        var all = await reader.BufferAllAsync();
        stopwatch.Stop();
        _ = all[0];
        return stopwatch.ElapsedMilliseconds;
    }

    public static async Task<long> TimeToFirstRowWhenStreamedAsync(OrderReader reader)
    {
        var stopwatch = Stopwatch.StartNew();
        await foreach (var _ in reader.StreamAsync())
            break;                          // stop at the first row
        stopwatch.Stop();
        return stopwatch.ElapsedMilliseconds;
    }
}
