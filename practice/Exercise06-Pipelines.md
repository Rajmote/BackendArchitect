# Exercise 06 — a receipt pipeline that survives bad data

**Topic:** [§4.5 Producer/consumer pipelines](../src/BackendArchitect/Concurrency/Pipelines/Pipelines.md)
**Difficulty:** ⭐⭐
**Status:** 🔴 in progress — stubs and starter tests are in place.

---

## Scenario

Orders arrive as ids. Each one is rendered into a receipt (CPU work, sometimes throws on malformed
data) and then written out (I/O, one writer). Two stages, a bounded queue between them:

```
order ids ──► render (N workers) ──[bounded queue]──► write (1 worker) ──► receipts
```

The render function is injected, so your tests control exactly when it fails.

---

## What you implement

```csharp
public sealed class ReceiptPipeline
{
    public ReceiptPipeline(int renderWorkers, int capacity, Func<int, Receipt> render);

    public Task<PipelineOutcome> RunAsync(IReadOnlyList<int> orderIds, CancellationToken ct = default);
    public IReadOnlyList<int> DeadLetters { get; }
    public IReadOnlyList<Receipt> Written { get; }
}

public sealed record Receipt(int OrderId, decimal Total);
public sealed record PipelineOutcome(int Processed, int DeadLettered, int PeakQueueDepth);
```

## Requirements

| # | Requirement |
|---|---|
| 1 | A render that throws costs **one order** — the worker keeps going |
| 2 | Nothing is lost: `Processed + DeadLettered == orderIds.Count`, always |
| 3 | Failed order ids land in `DeadLetters` |
| 4 | `PeakQueueDepth` never exceeds the `capacity` you were given |
| 5 | Completion is correct — no `ChannelClosedException`, and every rendered receipt reaches `Written` |
| 6 | A cancelled token stops the run promptly and throws `OperationCanceledException` |

## Acceptance criteria

- ✅ Requirements 1–6 hold
- ✅ 🌟 **Verify the tests can fail** — move your `try/catch` *outside* the `await foreach` and confirm
  requirement 2 goes red. That placement is the whole lesson
- ✅ Say who calls `Complete()` on the queue between the stages, and why it cannot be a worker
- ✅ No `async void`, and every worker task is awaited — a faulted task nobody awaits is a silent failure

## Hints (read only when stuck)

<details>
<summary>Requirement 1 — where does the try go?</summary>

Inside the `await foreach`, around the single item. Outside the loop it still catches the exception and
the worker still dies, because that loop is the worker's entire life. The demo measures the difference:
37/200 processed with 0 workers alive, versus 181/200 with all 4 alive.
</details>

<details>
<summary>Requirement 5 — who completes the channel?</summary>

Not a worker. `await Task.WhenAll(renderWorkers)` first, and only then
`queue.Writer.Complete()`. One worker finishing early and completing the channel gives its siblings
`ChannelClosedException` and silently loses whatever they were holding — the demo loses 3 of 40 that way.

`Task.WhenAll` does a second job here: it is where a faulted worker's exception finally surfaces.
</details>

<details>
<summary>Requirement 4 — measuring peak depth</summary>

Count writes and reads with `Interlocked`, take the difference after each write, and keep the maximum
with a compare-and-swap loop. `OrderPipeline.InterlockedMax` in the reference code shows the shape —
`Interlocked.CompareExchange` in a `while`, because "read the max, then maybe write it" is itself a
check-then-act.
</details>
