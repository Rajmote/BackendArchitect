# Exercise 05 — a lock-free pricing catalogue

**Topic:** [§4.4 Immutability](../src/BackendArchitect/Concurrency/Immutability/Immutability.md)
**Difficulty:** ⭐⭐
**Status:** 📝 brief only — stubs and starter tests land when exercises resume.

---

## Scenario

A pricing catalogue is read on **every** request and republished a few times an hour when an operator
pushes new prices. The current implementation takes a lock on every read and is the hottest contended
object in the service.

Replace it with a copy-on-write design where **readers never block**.

---

## What you implement

```csharp
public sealed class PriceCatalogue
{
    public decimal? PriceOf(string sku);
    public PriceSnapshot Current { get; }          // a frozen view, safe to hold indefinitely
    public void Publish(IEnumerable<PriceChange> changes);
    public int Version { get; }
}

public sealed record PriceChange(string Sku, decimal Price);
public sealed record PriceSnapshot(int Version, ImmutableDictionary<string, decimal> Prices);
```

## Requirements

| # | Requirement |
|---|---|
| 1 | `PriceOf` takes **no lock** — verified by reading the code, not by a test |
| 2 | A `PriceSnapshot` handed to a caller **never changes**, however many publishes follow |
| 3 | A batch `Publish` is **atomic**: no reader ever sees half a batch applied |
| 4 | `Version` increases by exactly 1 per `Publish`, with 8 writers publishing concurrently |
| 5 | Every caller that reads `Current` twice during one publish gets either the old snapshot or the new one — **never a mix** |
| 6 | The type is **deeply immutable** — `PriceSnapshot` cannot be mutated by anyone holding it |

## Acceptance criteria

- ✅ Requirements 1–6 hold
- ✅ Concurrency tests use **real `Thread`s + a `Barrier`**
- ✅ 🌟 **Verify the test can fail** — swap `ImmutableDictionary` for `Dictionary` and confirm
  requirement 3 or 5 goes red
- ✅ Say which atomic primitive you used to publish (`Volatile.Write`, `Interlocked.Exchange`, or
  `ImmutableInterlocked.Update`) and why — they are **not** interchangeable here

## Hints (read only when stuck)

<details>
<summary>Why requirement 4 rules out the simplest approach</summary>

`Volatile.Write(ref _snapshot, next)` publishes atomically but **loses concurrent publishes** — it's a
read-modify-write across two fields (the map and the version) and therefore check-then-act, straight
back to §4.3. You need either a compare-and-swap retry loop
(`Interlocked.CompareExchange` in a `while`) or one lock **shared by writers only**.
</details>

<details>
<summary>Why requirement 5 is free once you get requirement 3 right</summary>

If `Version` and `Prices` live in the **same** immutable snapshot object, one reference swap publishes
both. If they are two fields, a reader can catch the new version with the old prices. **Bundle the state
that must change together into one immutable object** — that is the whole trick.
</details>
