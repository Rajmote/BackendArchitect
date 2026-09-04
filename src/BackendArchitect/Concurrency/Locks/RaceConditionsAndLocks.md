# Concurrency · Race conditions & locks

> **Where this sits:** Technology `Concurrency & Async` → Main topic `Race conditions & locks` → Sub topic `Shared mutable state`.
> Runnable code: [`Counter.cs`](Counter.cs) · [`SessionCache.cs`](SessionCache.cs) ·
> [`AccountTransfer.cs`](AccountTransfer.cs) · [`RaceConditionsDemo.cs`](RaceConditionsDemo.cs).
>
> Builds on [§4.1 async/await](../AsyncAwait/AsyncAwait.md) and [§4.2 Tasks & streams](../Streams/TasksAndStreams.md).

---

## The one sentence

> **A race condition is when the correctness of your program depends on the *timing* of concurrent
> operations.**

The word "race" is literal: two threads race, and **who wins changes the answer**.

---

## 1. WHY — you have already met this four times

| Where | The sequence that broke |
|---|---|
| [Ticket booth](../../Databases/SQL/IsolationLevels/IsolationLevels.md) — oversold seats | read availability → decrement |
| [Idempotency handler](../../../../practice/Exercise01-IdempotencyKeys.md) — customer charged twice | `TryGetValue` → charge → store |
| [Circuit breaker](../../Reliability/Resilience/Resilience.md) — ten probes instead of one | read state → claim the probe |
| `Sold++` in the isolation demo | read → add → write |

Four different features. **One bug, wearing four costumes.**

---

## 2. WHAT actually goes wrong — `count++` is three operations

This line looks indivisible. It isn't:

```csharp
_counter++;
```

The CPU does three separate things:

```
1. READ   _counter into a register     (say, 5)
2. MODIFY add 1                        (6)
3. WRITE  back to memory               (6)
```

Two threads can interleave *between* those steps:

```
Thread A: read 5
Thread B: read 5          ← both saw 5
Thread A: write 6
Thread B: write 6         ← two increments, one result. An update was LOST.
```

### 🧠 Lost updates are permanent

Not "eventually consistent". Not "settled later". The second write **overwrote** the first, and nothing
in the runtime remembers that an increment was owed. **A race doesn't delay the right answer — it
destroys it.**

Measured, 8 threads × 200,000 increments (see the demo output below):

```
expected 1,600,000
actual     421,132       ← 1,178,868 increments simply gone
```

And a **different number every run**. That non-determinism is the signature of a race.

---

## 3. WHERE it lives — the check-then-act shape

> 🌟 **Any `read → decide → write` sequence is a race unless something makes it one indivisible step.**

Learn to see the **gap**:

```csharp
if (_seen.TryGetValue(idempotencyKey, out var existing))   // ← CHECK
    return existing;
                                                           // ← 🕳️ THE GAP
var receipt = _gateway.Charge(request);
_seen[idempotencyKey] = receipt;                           // ← ACT
```

In that gap the shared state says *"nothing has been charged"* — and every thread arriving believes it.

| | check | act | the gap |
|---|---|---|---|
| `_counter++` | read 5 | write 6 | between read and write |
| Idempotency | "not seen" | store receipt | between lookup and store |
| Ticket booth | "seats available" | decrement | between read and decrement |

**The drill:** when you read shared state and then write it, ask *"what if another thread ran the whole
sequence inside my gap?"*

---

## 4. HOW to fix it — three tools, in order of preference

### 🥇 `Interlocked` — atomic operations on a single variable

```csharp
Interlocked.Increment(ref _counter);            // atomic read-modify-write
Interlocked.Add(ref _total, amount);
Interlocked.Exchange(ref _flag, 1);             // atomic set, returns the old value
Interlocked.CompareExchange(ref _state, 2, 1);  // "set to 2 ONLY if it's currently 1"
Volatile.Read(ref _counter);                    // a safe read of a value others are writing
```

One atomic CPU instruction (`lock xadd`). No blocking, no context switch. **Measured 5× faster than
`lock` under contention** — 45 ms vs 248 ms in the demo.

### 🥈 `lock` — mutual exclusion for a *block*

When the atomic unit spans several operations or several fields:

```csharp
private readonly Lock _gate = new();   // .NET 9+; before that, a plain `object`

lock (_gate)
{
    if (_available <= 0) return false;   // check
    _available--;                        // and act — now one indivisible step
    return true;
}
```

Two rules, both learned the hard way in the [circuit-breaker exercise](../../../../practice/Exercise02-CircuitBreaker.md):

- **Keep the critical section small** — everything inside is serialised
- 🌟 **Never hold a lock across I/O** (`await`, HTTP, database) — you would queue every caller behind one
  slow network call. *(You cannot `await` inside `lock` at all: the compiler forbids it. That is the
  language protecting you.)*

### 🥉 `Concurrent*` collections — when the shared state *is* a collection

```csharp
ConcurrentDictionary<string, int> _hits = new();
_hits.AddOrUpdate(key, 1, (_, value) => value + 1);      // atomic
```

⚠️ **The trap:** individual operations are atomic, **sequences are not**.

```csharp
if (!_sessions.ContainsKey(userId))      // ❌ still check-then-act
    _sessions.TryAdd(userId, new Session(userId));

_sessions.GetOrAdd(userId, Create);      // ✅ one atomic operation
```

> 🧠 **Thread-safe collection ≠ thread-safe code.** The collection guarantees *its own* internal state.
> It cannot guarantee the logic you wrap around it.

Notice what the damage actually is. In the demo, 8 threads race for one key:

```
CheckThenAct : constructed 8, callers got 1 distinct session
```

The dictionary is **perfectly fine** — everyone got the same session. But `new Session(...)` ran **eight
times**, and seven of those were thrown away. If a `Session` opens a database connection or reserves a
licence seat, you have just orphaned seven of them. **The bug is invisible from the outside**, which is
what makes it dangerous.

### `GetOrAdd`'s small print — and `Lazy<T>`

`GetOrAdd` guarantees that only one value is **stored**, not that the factory runs once:

```
GetOrAdd     : constructed 8, callers got 1 distinct session
```

Still eight constructions. If constructing is expensive or has side effects, defer it:

```csharp
private readonly ConcurrentDictionary<string, Lazy<Session>> _sessions = new();

public Session GetOrCreate(string userId) =>
    _sessions.GetOrAdd(userId, id => new Lazy<Session>(() => new Session(id))).Value;
```

The `Lazy` wrapper may be created several times, but only the **winner's** `.Value` is ever evaluated:

```
LazyGetOrAdd : constructed 1, callers got 1 distinct session   ✅
```

---

## 5. WHEN locking bites back — deadlock

Two locks acquired in **opposite orders**:

```csharp
public void Transfer(Account from, Account to, decimal amount)
{
    lock (from)
        lock (to)
        {
            from.Balance -= amount;
            to.Balance   += amount;
        }
}
```

Trace it with the **objects**, not the parameter names:

```
Thread A: Transfer(alice, bob, 100)     Thread B: Transfer(bob, alice, 50)
────────────────────────────────────    ────────────────────────────────────
lock (alice)  ✅ acquired                lock (bob)    ✅ acquired
lock (bob)    ⏳ waiting for B...        lock (alice)  ⏳ waiting for A...
```

**A holds alice and needs bob. B holds bob and needs alice.** Neither can finish, so neither ever lets
go. 💀

> 🧠 The trap is that *"lock `from`, then lock `to`"* **sounds** like a consistent order. It is
> consistent in terms of the **parameters** and inconsistent in terms of the **objects** — and objects
> are the only thing the locks know about.

### Why it's worse than a race

| | Race condition | Deadlock |
|---|---|---|
| Symptom | wrong answer | **no answer** |
| Exception? | no | no |
| Self-heals? | (the damage is done) | **never** — only a restart |
| In production | silently corrupt data | threads park, pool drains, **the service stops answering** |

### The fix — a total order every caller agrees on

```csharp
public void Transfer(Account from, Account to, decimal amount)
{
    var (first, second) = from.Id < to.Id ? (from, to) : (to, from);

    lock (first)
        lock (second)
        {
            from.Balance -= amount;
            to.Balance   += amount;
        }
}
```

Now **both** threads take `alice` before `bob`, whichever direction the money is going. Whoever wins
runs to completion; the other waits a moment and proceeds. No cycle, no deadlock:

```
lock (from) then lock (to)   : 0 completed, 2 deadlocked
lock in ascending account id : 2 completed, 0 deadlocked   ✅
```

Any total ordering works — id, account number, `RuntimeHelpers.GetHashCode` — as long as **every code
path in the system agrees on it**.

> 🌟 **One lock is easy. Two locks is where deadlocks live.** If you need two, order them globally — or
> restructure so you only need one.

⚠️ Note on the demo: a real deadlock **never times out**. `Monitor.TryEnter`/`Lock.TryEnter` with a
timeout is used in [`AccountTransfer.cs`](AccountTransfer.cs) only so the example can *detect* and
report the deadlock instead of hanging the test run. Timeouts are a diagnostic here, not the fix.

---

## 6. WHO decides — the architect's view

- **Prefer no shared mutable state at all.** Immutability (§4.4) and message passing via
  [`Channel<T>`](../Streams/TasksAndStreams.md) remove the problem instead of guarding it.
- **If you must share, keep the guarded region tiny** and never let I/O inside it.
- **Push contention down to the storage layer** where it belongs — an optimistic-concurrency ETag, a
  Cosmos [conditional write](../../Databases/Cosmos/ConsistencyLevels/ConsistencyLevels.md), or a SQL
  `UPDATE ... WHERE Version = @expected`. In a horizontally-scaled service an in-process `lock` protects
  **one instance only** — it is worthless across pods.
- **Locks are a scalability ceiling.** Everything inside one is single-threaded by definition.

> 🌟 **An in-process `lock` is a single-instance solution. The moment you run two replicas, correctness
> has to move into the database.**

---

## 7. Testing for races

Races are non-deterministic, so a test that "passes" proves nothing unless you force the interleaving:

- **Real `Thread`s, not `Task.Run`** — the pool can stagger tasks and quietly hide the race
- **A `Barrier`** so every thread starts at the same instant
- 🌟 **Verify the test can fail** — remove the lock and confirm it turns red. Otherwise it is a false green
- For a genuinely probabilistic effect, **retry a few times** and assert it happened at least once
  (`Unsynchronized_increments_lose_updates` does exactly this)

See [`LocksTests.cs`](../../../../tests/BackendArchitect.Tests/Concurrency/LocksTests.cs).

---

## 8. Demo output

```
8 threads x 200,000 increments — expected 1,600,000:
  no synchronization :   421,132  LOST 1,178,868  (28 ms)
  lock               : 1,600,000  correct  (248 ms)
  Interlocked        : 1,600,000  correct  (45 ms)
  -> a lost update is permanent; both fixes are correct, Interlocked is the cheap one

8 threads asking one ConcurrentDictionary for the same session:
  CheckThenAct : constructed 8, callers got 1 distinct session(s)
  GetOrAdd     : constructed 8, callers got 1 distinct session(s)
  LazyGetOrAdd : constructed 1, callers got 1 distinct session(s)
  -> every caller gets the same session, but the FACTORY ran several times — orphaned work

Transfer(alice→bob) and Transfer(bob→alice) at the same instant:
  lock (from) then lock (to)      : 0 completed, 2 deadlocked, £2,000 still in the bank
  lock in ascending account id    : 2 completed, 0 deadlocked, £2,000 still in the bank
  -> consistent means consistent in the OBJECTS, not the parameter names
```

Note the last column: **money is conserved in both cases.** A deadlock costs you *availability*, not
correctness. A race costs you correctness. Different failures, different fixes.

---

## 9. Warm-up questions

1. `_counter++` from 100 threads, 1,000 times each. Is the result 100,000?
2. Where exactly is the gap in a `TryGetValue` → charge → store idempotency handler?
3. `lock` or `Interlocked` for a single counter — and why?
4. `ConcurrentDictionary` is thread-safe. Why is `ContainsKey`-then-`TryAdd` still broken, and what is
   the *actual* damage?
5. `lock (from) { lock (to) { … } }`, with two opposing transfers. What happens, and why does it *look*
   correct?
6. Your `lock` protects a counter perfectly. You scale to three replicas. What breaks?

---

## 10. The three to remember

1. 🌟 **Any read → decide → write is a race** unless something makes it indivisible
2. 🌟 **Thread-safe collection ≠ thread-safe code** — the collection guards itself, not your logic
3. 🌟 **Two locks in different orders = deadlock** — order by the object, not the parameter
