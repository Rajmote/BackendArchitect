# Exercise 03 — Fixing async code

**Topic:** §4.1 async/await · **Difficulty:** ⭐⭐ · **Estimated:** 40–60 min

> Deliberately back to ⭐⭐. No state machine, no locking — this is about **spotting and fixing the four
> classic async mistakes**, which is exactly what you'll do in real code reviews.

---

## Scenario

You've inherited `OrderSummaryService`. It builds a dashboard summary by fetching a customer, their
orders, and current prices from three different services.

It works — and it's slow, wastes threads, and has a bug that can take the process down.

## Your job

Rewrite `OrderSummaryService` in
[`BackendArchitect.Practice/Exercise03/OrderSummaryService.cs`](BackendArchitect.Practice/Exercise03/OrderSummaryService.cs)
so that:

1. **`GetSummaryAsync` returns a `Task<OrderSummary>`** and is awaited properly — no `.Result`, no `.Wait()`.
2. **The three independent fetches run concurrently**, not one after another.
   *(Customer, orders and prices don't depend on each other. The total must be ≈ the slowest single call,
   not the sum.)*
3. **No `async void`** anywhere — `LogAudit` must become awaitable so its failures can be observed.
4. **No `Task.Run` around genuinely async work** — it adds a thread for nothing.
5. **A `CancellationToken` flows through** every async call, so a cancelled request stops the work.
6. `TotalValue` = the sum of each order's quantity × that product's price.

## Acceptance criteria

- [ ] The 3 given tests pass
- [ ] You've added tests for **concurrency** (it's meaningfully faster than sequential) and
      **cancellation** (a cancelled token stops it)
- [ ] No `.Result`, `.Wait()`, `async void` or `Task.Run` anywhere in your solution
- [ ] Zero warnings

## Hints

- Start all three calls first, keep the `Task<T>` handles, **then** `await Task.WhenAll(...)`.
- After `WhenAll`, awaiting each task again is free — it's already finished.
- `CancellationToken` is the **last parameter** by convention, and defaults to `default`.
- For the audit log, the fix is one word: `void` → `Task`.
- To assert cancellation: `await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ...)`.

## What I'll review
1. **Correctness** — all six requirements, and the totals are right
2. **Concurrency** — are the three calls genuinely overlapped?
3. **Your tests** — did you prove the concurrency and the cancellation, or just the happy path?
4. **Idiomatic C#** — token flow, naming (`...Async`), no blocking anywhere

## ⚠️ Don't peek
`src/BackendArchitect/Concurrency/AsyncAwait/AsyncPatterns.cs` shows the sequential-vs-concurrent shape.
Try it yourself first.
