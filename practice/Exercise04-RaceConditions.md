# Exercise 04 — a seat-reservation service that must not oversell

**Topic:** [§4.3 Race conditions & locks](../src/BackendArchitect/Concurrency/Locks/RaceConditionsAndLocks.md)
**Difficulty:** ⭐⭐
**Status:** 📝 brief only — stubs and starter tests land when exercises resume.

---

## Scenario

A cinema sells seats for a screening. Requests arrive concurrently from a web front end. Three things
must hold no matter how many threads arrive at once:

1. **Never oversell.** If 10 seats exist, at most 10 reservations succeed — even with 200 simultaneous
   requests.
2. **Never lose a booking.** `SeatsSold + SeatsAvailable` always equals the capacity.
3. **One seat map per screening.** Building a screening's seat map is expensive; it must happen
   **exactly once** per screening id, however many threads ask for it first.

---

## What you implement

```csharp
public sealed class BoxOffice
{
    public BoxOffice(int capacity, Func<string, SeatMap> buildSeatMap);

    public ReservationResult Reserve(string screeningId, string customerId);
    public int SeatsSold { get; }
    public int SeatsAvailable { get; }
    public int SeatMapsBuilt { get; }
}

public sealed record ReservationResult(bool Succeeded, int? SeatNumber, string? Error);
```

## Requirements

| # | Requirement |
|---|---|
| 1 | 200 concurrent `Reserve` calls against a capacity of 10 → **exactly 10** succeed |
| 2 | The other 190 return `Succeeded == false` with a sold-out error — **not** an exception |
| 3 | `SeatsSold + SeatsAvailable == capacity` at every moment, checked after the race |
| 4 | Each successful reservation gets a **distinct** seat number, 1..capacity |
| 5 | `SeatMapsBuilt == 1` even when 200 threads hit an unseen screening id simultaneously |
| 6 | No lock is held across the (simulated slow) seat-map build **for a screening already built** |

## Acceptance criteria

- ✅ Requirements 1–6 hold
- ✅ Your concurrency tests use **real `Thread`s + a `Barrier`**, not `Task.Run`
- ✅ 🌟 **You have verified the test can fail** — remove the synchronisation and confirm it goes red.
  Say so in your write-up; an unverified concurrency test is a false green
- ✅ Use `Interlocked` where a single variable is enough, `lock` only where it isn't — and say which you
  chose for each field and why

## Hints (read only when stuck)

<details>
<summary>Which tool for which field?</summary>

`SeatsSold` alone could be `Interlocked`. But requirement 3 couples it to the seat allocation, and
requirement 1 makes "check capacity then take a seat" a **check-then-act** pair. That pair needs one
`lock`. The seat map is a separate problem — that's a `ConcurrentDictionary` + `Lazy<T>`.
</details>

<details>
<summary>Why not just Interlocked.Decrement on the remaining count?</summary>

Because you'd have to decrement *before* you know whether a seat was available, then compensate if it
went negative. That works, and it's a real technique — but the compensating path is easy to get wrong
and it briefly reports a negative count to any reader. Try both and compare.
</details>
