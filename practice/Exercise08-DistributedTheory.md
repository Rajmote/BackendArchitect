# Exercise 08 — inventory that refuses rather than oversells

**Topic:** [§8.1 Distributed systems theory](../src/BackendArchitect/Distributed/Theory/DistributedTheory.md)
**Difficulty:** ⭐⭐
**Status:** 🔴 in progress — stubs and starter tests are in place.

---

## Scenario

A 5-node inventory service is running during a **3|2 network partition**. Both sides are alive and
serving traffic; they just cannot reach each other.

Reserving stock is a **CP** operation — refusing a reservation is far cheaper than selling the same
unit twice. Clients retry on timeout, because they cannot tell "never arrived" from "done, response
lost".

---

## What you implement

```csharp
public sealed class InventoryService
{
    public InventoryService(int nodeCount, int minoritySize, int stock);

    public int Quorum { get; }
    public int Stock { get; }
    public ReservationResult Reserve(string requestId, NodeSide side);
    public bool Commit(int fencingToken);
}

public sealed record ReservationResult(bool Succeeded, int? FencingToken, string? Error);
public enum NodeSide { Majority, Minority }
```

## Requirements

| # | Requirement |
|---|---|
| 1 | `Quorum` is a majority — 3 for 5 nodes |
| 2 | Only a side that can reach a quorum may reserve; the minority side **refuses** (a failed result, never an exception) |
| 3 | A repeat of the same `requestId` returns the **original** result and reserves **nothing extra** |
| 4 | Stock is never oversold — 100 reservations against 10 units, every one retried, leaves exactly 10 taken |
| 5 | Every successful reservation gets a **strictly increasing** fencing token |
| 6 | `Commit` **rejects** any token lower than the highest already committed |

## Acceptance criteria

- ✅ Requirements 1–6 hold
- ✅ Requirement 3 is tested by calling `Reserve` **twice with the same id** and asserting *both* that
  stock moved once and that the two results are identical
- ✅ Say in one sentence why requirement 6 lives in `Commit` and not in `Reserve`
- ✅ No exception is ever thrown for a refusal — a refusal is a normal outcome of a CP system

## Hints (read only when stuck)

<details>
<summary>Requirement 3 — why the result must be identical, not merely successful</summary>

The retry is the *same* operation, so it must return the *same* token. If it returned a new token, a
caller that retried would hold a token the first call never issued, and two callers could believe they
hold different valid claims on one unit. Store the result against the request id, exactly as your
idempotency handler in Exercise 01 stores receipts against the key.
</details>

<details>
<summary>Requirement 6 — why fencing belongs in Commit</summary>

The lock cannot protect you, because the holder can be paused (GC, VM freeze) past its lease and never
observe it:

```
t=0   A reserves, token 33     t=60  lease expires, B reserves, token 34
t=10  A pauses for 90s         t=65  B commits with 34
t=100 A wakes and commits with 33  ← must be REJECTED
```

A was never wrong to think it held the claim. Only the **resource** knows that 34 has already been
seen — so only the resource can reject 33. **Safety lives in the resource, not in the lock.**
</details>

<details>
<summary>Requirement 4 — where the check-then-act hides</summary>

"Is there stock? then decrement" is check-then-act, now across a network. In this single-process
exercise one `lock` or `Interlocked` closes it (§4.3) — but note in your write-up that in a real
multi-replica deployment an in-process lock protects **one instance only**, and the check has to move
into storage as a conditional write.
</details>
