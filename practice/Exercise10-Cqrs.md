# Exercise 10 — a level-3 read model, with the bill attached

**Topic:** [§8.3 CQRS](../src/BackendArchitect/Distributed/Cqrs/Cqrs.md)
**Difficulty:** ⭐⭐
**Status:** 🔴 in progress — stubs and starter tests are in place.

---

## Scenario

A level-3 slice of an order system: a write store, an event stream, a projector you control, and a
read store. Build it so every cost of the pattern is visible and handled — not hidden.

```
   Handle(PlaceOrder) ──► write store ──► event ──► RunProjector() ──► read store
                                                                           ▲
   Query(orderId) ─────────────────────────────────────────────────────────┘
```

---

## What you implement

```csharp
public sealed class OrderSystem
{
    public OrderSummary  Handle(PlaceOrder command);
    public OrderSummary? Query(int orderId);
    public OrderSummary? QueryAtLeastVersion(int orderId, long version);
    public int  RunProjector();
    public void StallProjector();
    public ProjectorHealth Health { get; }
    public int  RebuildReadModel();
}
```

## Requirements

| # | Requirement |
|---|---|
| 1 | `Handle` returns the order it wrote — no query, no staleness |
| 2 | `Query` returns `null` until the projector has run. **That is correct behaviour, not a bug** |
| 3 | `QueryAtLeastVersion` waits for the projection to reach the version, then reads |
| 4 | `RunProjector` is **idempotent** — running it twice leaves exactly one row per order |
| 5 | A stalled projector leaves `Query` answering **old data**, with only `Health` revealing it |
| 6 | `Health` reports published, projected and lag — freshness, not liveness |
| 7 | `RebuildReadModel` discards the read store and reprojects the whole stream correctly |

## Acceptance criteria

- ✅ Requirements 1–7 hold
- ✅ 🌟 One test proves the **stalled** case: `Query` returns valid-looking data for old orders while
  the read model is frozen. That is the failure mode no health check catches
- ✅ Requirement 4 is tested by calling `RunProjector()` twice, not by reading the code
- ✅ In one sentence: say which of the five read-your-writes fixes requirement 1 implements, and why
  it's the cheapest

## Hints (read only when stuck)

<details>
<summary>Requirement 1 — why the command returns the order</summary>

The write side just created it: it knows the id, the customer, the total and the version. Going back
to the read store to ask about data you wrote one millisecond ago is the mistake. The reference demo
measures it — `query the read store` misses 50 of 50; `return from the command` misses none and waits
for nothing.
</details>

<details>
<summary>Requirement 4 — where duplicates come from</summary>

The projector is a consumer group, and at-least-once means it will see the same event twice after any
crash, redeploy or rebalance (§8.2b). Key the read store by order id and overwrite, or track the last
applied version per order. An append-style projection that blindly adds a row is wrong — prove it by
calling `RunProjector()` twice.
</details>

<details>
<summary>Requirement 5 — the test that matters</summary>

Assert *both* halves: `Query(oldOrderId)` still returns data (the read store is up and fast) **and**
`Health.Lag > 0`. If your test only asserts that the new order is missing, it hasn't captured why this
goes unnoticed for three days.
</details>

<details>
<summary>Requirement 7 — why rebuild is the point</summary>

Being able to throw the read model away and regenerate it from the event stream is the strongest
argument for level 3: a bug in the projection is fixable without touching the source data. Keep the
events, delete the projection, reprocess from offset zero.
</details>
