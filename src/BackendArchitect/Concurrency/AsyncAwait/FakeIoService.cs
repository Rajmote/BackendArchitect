using System.Collections.Concurrent;

namespace BackendArchitect.Concurrency.AsyncAwait;

// Two ways to model the same 50ms "network call", so the thread cost of each can be measured:
//
//   BlockingCall - Thread.Sleep: the thread sits there doing nothing (a synchronous API, or the
//                  "async over sync" anti-pattern once you wrap it in Task.Run)
//   AsyncCall    - Task.Delay:   models real async I/O. No thread is waiting at all -
//                  "there is no thread"
//
// Every entry records the managed thread that ran it, so we can count how many the pool had to hand out.
public sealed class FakeIoService
{
    private readonly TimeSpan _latency;
    private readonly ConcurrentBag<int> _threadsUsed = [];

    public FakeIoService(TimeSpan latency) => _latency = latency;

    /// <summary>Distinct pool threads consumed — the cost signal for this topic.</summary>
    public int DistinctThreadsUsed => _threadsUsed.Distinct().Count();

    public int CallsMade => _threadsUsed.Count;

    /// <summary>A synchronous API: whoever calls it has their thread held for the duration.</summary>
    public string BlockingCall(string request)
    {
        _threadsUsed.Add(Environment.CurrentManagedThreadId);
        Thread.Sleep(_latency);           // the thread is stuck here, doing nothing
        return $"result for {request}";
    }

    /// <summary>Genuinely async I/O: the thread is released while the "network" works.</summary>
    public async Task<string> AsyncCall(string request, CancellationToken cancellationToken = default)
    {
        _threadsUsed.Add(Environment.CurrentManagedThreadId);
        await Task.Delay(_latency, cancellationToken);   // no thread is waiting here
        return $"result for {request}";
    }
}
