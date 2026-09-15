# Concurrency · Immutability

> **Where this sits:** Technology `Concurrency & Async` → Main topic `Immutability` → Sub topic `Deep immutability & copy-on-write`.
> Runnable code: [`MutabilityTraps.cs`](MutabilityTraps.cs) · [`FeatureFlags.cs`](FeatureFlags.cs) ·
> [`ImmutabilityDemo.cs`](ImmutabilityDemo.cs).
>
> [§4.3](../Locks/RaceConditionsAndLocks.md) gave you tools to **guard** shared mutable state.
> This one **removes the problem**.

---

## The one sentence

> **If nothing can change, there is nothing to race on.**

Every bug in §4.3 needed two ingredients: **shared** + **mutable**. Take either away and the bug becomes
impossible. Locking guards the second ingredient; immutability **deletes** it — and unlike a lock, it
cannot be forgotten by the next developer.

---

## 1. WHAT — the definition that matters

> **An object whose *observable* state cannot change after construction.**

Observable is the operative word. It is not about how many `readonly` keywords you typed — it is about
whether anyone holding a reference can ever see a different value than they saw before.

```csharp
public class Money                                  // mutable: a liability
{
    public decimal Amount { get; set; }
    public string Currency { get; set; }
}

public sealed record Money(decimal Amount, string Currency);   // immutable
```

The second is safe to hand to 100 threads, cache forever, and use as a dictionary key. The first is
dangerous in all three cases.

---

## 2. HOW — the tools C# gives you

```csharp
private readonly int _id;                           // field, assignable only in the constructor
public string Name { get; }                         // get-only property
public string Email { get; init; }                  // set in an object initializer, then frozen
public required string Country { get; init; }       // init-only AND the compiler forces you to set it

public sealed record Customer(int Id, string Name); // all of the above + value equality + `with`
```

`record` is the workhorse — four things for one keyword:

```csharp
var alice = new Customer(1, "Alice");
var same  = new Customer(1, "Alice");

alice == same                                   // true — VALUE equality
alice.ToString()                                // "Customer { Id = 1, Name = Alice }"
var renamed = alice with { Name = "Alicia" };   // a new Customer; alice is untouched
```

### 🌟 `with` produces a value — it does not mutate

```csharp
var renamed = alice with { Name = "Alicia" };
// alice.Name   is still "Alice"
// renamed.Name is "Alicia"
```

The compiler generates roughly `new Customer(alice.Id, "Alicia")`. Both objects exist. The old one
survives *because that is the point* — any thread already holding it keeps a stable, correct value.

> 🌟 **In immutable code, ignoring the return value means you did nothing.**

You already know this rule from `string`:

```csharp
name.ToUpper();                  // ❌ result discarded — name unchanged
var upper = name.ToUpper();      // ✅

date.AddDays(1);                 // ❌ DateTime is immutable too
immutableList.Add("x");          // ❌ returns a NEW list; the original is still empty
list.Add("x");                   // ✅ List<T> mutates — the odd one out
```

That `immutableList.Add` line is the one that bites in real code: it compiles, looks like it worked, and
the list stays empty forever.

---

## 3. ⚠️ WHERE it goes wrong — five traps, all the same bug

> 🌟 **`readonly`, `init`, `record` and `with` all protect the ARROW. None of them protects what the
> arrow points at. Only the target's own type does that.**

Think of a **house number painted on a signpost**. `readonly` means nobody can repaint the sign to point
at a different house. Anyone can still walk in and rearrange the furniture. 🏠

Every trap below is that one sentence in a different costume. All are measured in
[`MutabilityTraps.cs`](MutabilityTraps.cs).

### Trap 1 — `readonly` freezes the field, not the object

```csharp
public readonly Dictionary<string, string> Settings = new();

config.Settings = new Dictionary<string, string>();   // ❌ blocked
config.Settings["timeout"] = "5";                     // ✅ compiles. Mutated.
config.Settings.Clear();                              // ✅ compiles. Wiped.
```

All four of these are mutable, no matter how many `readonly`s you add:

```csharp
public readonly List<Order> Orders = new();
public readonly StringBuilder Log = new();
public readonly Customer Owner;              // if Customer has settable properties
public readonly int[] Scores = new int[10];  // arrays are NEVER immutable
```

### Trap 2 — `record` is only as immutable as its property *types*

```csharp
public sealed record Basket(int Id, List<string> Items);

basket.Items = new List<string>();   // ❌ init-only
basket.Items.Add("Muffin");          // ✅ compiles. Mutated.
```

Identical to Trap 1: `init` freezes the arrow. A record containing a `List`, an array, or a mutable
class **is a mutable record**.

### Trap 3 — `IReadOnlyList<T>` is a window, not a photograph

```csharp
private readonly List<Order> _orders = new();
public IReadOnlyList<Order> Orders => _orders;     // ← publishes the live list
```

```csharp
var snapshot = service.Orders;   // Count is 2
service.Add(newOrder);
snapshot.Count                   // 3 — it was never a snapshot
```

Nothing was copied. `IReadOnlyList<T>` is an *interface*, and you handed out the same `List<Order>` seen
through a narrower door.

| | Means | Does NOT mean |
|---|---|---|
| `IReadOnlyList<T>` | *you* can't write to it | it won't change |
| `ImmutableArray<T>` | it **will never change**, for anyone | — |

Two consequences, both real:

```csharp
foreach (var order in snapshot)        // 💥 InvalidOperationException: Collection was modified
    Process(order);

var sneaky = (List<Order>)service.Orders;   // ✅ the runtime type is still List<Order>
sneaky.Clear();                             // your encapsulation was never there
```

> 🧠 **Returning an interface over a private mutable field does not encapsulate it — it publishes it
> with a polite note attached.**

### Trap 4 — `with` is a *shallow* copy

```csharp
var original = new Basket(1, ["Latte"]);
var copy = original with { Id = 2 };

copy.Items.Add("Muffin");
original.Items.Count       // 2 — both records point at the SAME list
```

```
original ──► Basket { Id = 1, Lines ─┐
                                     ├──► one shared List ["Latte", "Muffin"]
copy     ──► Basket { Id = 2, Lines ─┘
```

`Id` is an `int`, so copying it copies the number. `Items` is a reference, so copying it copies **the
arrow**. `with` goes exactly one level deep.

### Trap 5 — a mutable key is a permanent memory leak

```csharp
public sealed record MutableKey { public int Id { get; set; } }

var key = new MutableKey { Id = 1 };
var cache = new Dictionary<MutableKey, string> { [key] = "receipt-1" };

key.Id = 2;                   // the hash code has changed
cache.ContainsKey(key)        // false — the entry is in the wrong bucket, forever
```

The entry is still in memory. It is simply **unreachable**, and no exception ever points at it.

⚠️ **But it is narrower than it looks.** A record hashes its fields with `EqualityComparer<T>.Default`,
and for `List<T>` that is **reference**-based — so mutating the *contents* of a list property does
**not** move the key:

```
mutable record as a key : found=True -> found=False   ENTRY LOST
record + List as a key  : found=True -> found=True    still reachable
```

The trap fires when the property's own hash depends on mutable state, not merely when the property
points at something mutable.

### ✅ The fix — deep immutability

```csharp
public sealed record Basket(int Id, ImmutableArray<string> Items);
```

Now the shallow copy is **enough**, because there is nothing mutable left to share:

```
`with` + List<string>   : [Latte] -> [Latte, Muffin]   LEAKED
`with` + ImmutableArray : [Latte] -> [Latte]           held
```

> 🧠 **Checklist for a genuinely immutable type: every property is `init`/get-only, AND every
> property's *type* is itself immutable.** One `List`, array, or mutable class anywhere in the tree
> breaks the whole thing.

---

## 4. ⚠️ Immutable does not mean structurally equal

A surprise worth knowing, discovered by a failing test rather than assumed:

```csharp
var one  = new FrozenBasket(1, ["Latte"]);
var same = new FrozenBasket(1, ["Latte"]);

one == same                       // FALSE
one.Items.SequenceEqual(same.Items)   // true
```

`ImmutableArray<T>` compares by the **underlying array reference**, not by contents. So a record with an
`ImmutableArray` property does **not** get value equality over its elements — and is a poor dictionary
key unless the callers share the same array instance:

```csharp
ImmutableArray<string> shared = ["Latte"];
new FrozenBasket(1, shared) == new FrozenBasket(1, shared)   // true
```

> 🌟 **Immutability and value equality are separate guarantees.** A type can have either, both, or
> neither. `record` gives value equality *over its fields* — but each field still compares however
> **its own type** compares.

---

## 5. WHICH collection

| Type | Use it when | Cost |
|---|---|---|
| `ImmutableArray<T>` | small, read constantly, rarely rebuilt | reads **O(1)** — array-speed. `Add` = **O(n)** full copy |
| `ImmutableList<T>` | changes often, could be large | reads O(log n), `Add` **O(log n)** — shares structure |
| `ImmutableDictionary<K,V>` | a map that gets updated | O(log n) both ways |
| `FrozenDictionary<K,V>` | built **once**, read **millions** of times | slow to build, **fastest lookup** |

`ImmutableList.Add` does not copy the list — it is a tree, and the new version **shares almost all its
nodes** with the old one:

```csharp
var v1 = ImmutableList.Create(1, 2, 3);
var v2 = v1.Add(4);        // v1 is still [1,2,3]; they share nodes — no full copy
```

That is a **persistent data structure**, and it is why immutability costs less than it sounds.

📦 No package needed — `System.Collections.Immutable` and `System.Collections.Frozen` ship with the
runtime.

---

## 6. 🌟 WHY it matters — copy-on-write replaces the lock

Feature flags: read on every request, written a few times a day.

**§4.3 thinking — guard the mutable map:**
```csharp
private readonly Dictionary<string, bool> _flags = new();
private readonly Lock _gate = new();

public bool IsEnabled(string flag)
{
    lock (_gate) { return _flags.GetValueOrDefault(flag); }   // ← every request serialises here
}
```

**§4.4 thinking — swap an immutable map:**
```csharp
private ImmutableDictionary<string, bool> _flags = ImmutableDictionary<string, bool>.Empty;

public bool IsEnabled(string flag) =>
    Volatile.Read(ref _flags).GetValueOrDefault(flag);        // ← no lock. Ever.

public void Set(string flag, bool value) =>
    ImmutableInterlocked.Update(ref _flags, current => current.SetItem(flag, value));
```

The reader takes **no lock at all**. It grabs the current reference and reads a snapshot that **can
never change**; a writer publishing a new version cannot disturb a read already in flight. The only
atomic operation needed is swapping one reference.

Measured — 8 reader threads, 500,000 reads each, one writer publishing 200 updates throughout:

```
lock + Dictionary   :   884 ms for 4,000,000 reads
ImmutableDictionary :   305 ms for 4,000,000 reads     ← ~2.9× faster
FrozenDictionary    :   350 ms for 4,000,000 reads
```

Note that the immutable dictionary wins **despite** O(log n) lookups against `Dictionary`'s O(1) — the
lock contention costs far more than the algorithmic difference. `FrozenDictionary` has the fastest
lookups of the three but pays to rebuild on every write; it pulls ahead when writes are genuinely rare
(configuration loaded at startup, a routing table, a lookup of country codes).

> 🌟 **Reads are free because the thing being read is frozen.** Exactly the trade you want in a
> read-heavy system: reads (millions) cost nothing, writes (a few) pay for a copy.

---

## 7. WHO decides — the architect's view

- **Immutability is the cheapest concurrency strategy** — no lock to forget, no critical section to get
  wrong, no code review needed to keep it correct
- **Safe to cache and share** — hand the same instance to every caller, no defensive copy
- **Value equality for free** — records make good cache keys, dictionary keys and test assertions
  (subject to §4 above)
- **Event sourcing and audit trails** — an event is a fact that already happened; a fact you can edit is
  not a fact
- **Functional core, imperative shell** — pure immutable domain logic in the middle, mutation pushed out
  to the edges where the I/O lives
- ⚠️ **The honest cost is allocation.** Every change makes an object. In a hot loop that is GC pressure
  — reach for `readonly struct` for small values, `ImmutableList` where structural sharing pays, and
  `ImmutableArray.CreateBuilder` when constructing in a loop

> 🌟 **Lock when you must share mutable state. Prefer to make it immutable so you never have to.**

---

## 8. Demo output

```
Does the 'immutable' value survive an attempt to change it?
  readonly Dictionary field   : [timeout=30]  -> [retries=3, timeout=5]  LEAKED
  record + List<string>       : [Latte]       -> [Latte, Muffin]         LEAKED
  IReadOnlyList<T> view       : view has 2    -> view has 3              LEAKED
  IReadOnlyList<T> cast back  : [order-1]     -> [order-1, injected]     LEAKED
  `with` + List<string>       : [Latte]       -> [Latte, Muffin]         LEAKED
  `with` + ImmutableArray     : [Latte]       -> [Latte]                 held

A record used as a dictionary key, then mutated:
  mutable record as a key     : found=True -> found=False  ENTRY LOST
  record + List as a key      : found=True -> found=True   still reachable

8 reader threads x 500,000 reads, while one writer publishes 200 updates:
  lock + Dictionary   :   884 ms for 4,000,000 reads
  ImmutableDictionary :   305 ms for 4,000,000 reads
  FrozenDictionary    :   350 ms for 4,000,000 reads
```

Five "immutable" things leaked. One held. The difference is a single word in a type declaration.

---

## 9. Warm-up questions

1. `public readonly Dictionary<string,string> Settings = new();` — safe to share across 20 threads?
2. `record Basket(int Id, List<string> Items)` — does `basket.Items.Add("x")` compile?
3. You return `IReadOnlyList<T>` over a private `List<T>`. Name two ways a caller still breaks you.
4. After `var b = a with { Name = "X" };` — what is `a.Name`?
5. `copy = original with { Id = 2 }`, then `copy.Items.Add(...)`. What is `original.Items.Count`?
6. Two `FrozenBasket(1, ["Latte"])` values. Are they `==`? Why not?
7. Your reads take a lock and your service is read-heavy. What do you change, and what does the reader
   look like afterwards?

---

## 10. The three to remember

1. 🌟 **`readonly`, `init`, `record` and `with` protect the arrow — only the target's own type protects
   the target**
2. 🌟 **`IReadOnlyList<T>` says "you may not write". It does not say "this will not change"**
3. 🌟 **Copy-on-write makes readers lock-free: freeze the value, and swapping one reference is the only
   atomic operation you need**
