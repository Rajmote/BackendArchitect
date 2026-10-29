# Distributed Systems · CQRS

> **Where this sits:** Technology `Distributed Systems` → Main topic `CQRS` → Sub topic `The four levels, and when not to`.
> Runnable code: [`ReadModelLevels.cs`](ReadModelLevels.cs) · [`OrderProjection.cs`](OrderProjection.cs) ·
> [`LevelDecision.cs`](LevelDecision.cs) · [`CqrsDemo.cs`](CqrsDemo.cs).
>
> The most cargo-culted pattern on the roadmap. Half the teams using it don't need it, and half the
> teams who need it built it by accident.

---

## The one sentence

> **🌟 Use a different model to write data than you use to read it.**

That is the entire claim. Note what it does **not** say: nothing about two databases, nothing about
message buses, nothing about event stores. Those are implementations people chose.

### Why the two sides genuinely differ

```
WRITE side                          READ side
  "place this order"                  "show me the order list"
  one row at a time                   thousands of rows
  must enforce invariants             no rules to enforce - just shape the data
  normalized (no duplication)         denormalized (pre-joined, fast)
  ~100 per second                     ~10,000 per second
  correctness matters most            latency matters most
```

> 🧠 **CQRS is the observation that one model is being asked to do two jobs with opposite
> requirements.** Normalize for writes and every read is a 6-table join; denormalize for reads and
> every write updates five places.

---

## 1. 🌟 CQRS is a spectrum, not a switch

Four levels. **Most teams should stop at level 2.**

### Level 0 — one model, one everything

```csharp
public Order Get(int id);
public void Save(Order order);     // the same Order class for both
```
✅ Plain CRUD. Correct for most applications, and not CQRS.

### Level 1 — separate methods, same model

```csharp
public void Handle(PlaceOrderCommand command);     // changes state, returns nothing
public OrderDto Handle(GetOrderQuery query);       // returns data, changes nothing
```
**Free** — pure discipline, and it makes [§3.1's safe/idempotent split](../../Apis/Http/Fundamentals/HttpFundamentals.md)
visible in your service layer.

### Level 2 — separate read models, same database ⭐ the sweet spot

```csharp
public class Order { public void Cancel() { /* invariants */ } }           // write: rich domain object
public sealed record OrderListItem(int Id, string Customer, decimal Total); // read: flat DTO
```

One database. No sync. No eventual consistency. Measured, 100 list-page loads:

```
  level 0/1 — load aggregates : 6 table(s), 240,000 rows, eventually consistent = False
  level 2 — flat read model   : 2 table(s),   5,000 rows, eventually consistent = False
  level 3 — separate store    : 1 table(s),   5,000 rows, eventually consistent = True
```

**Level 2 removes 98% of the work and adds no staleness. Level 3 scans the same rows and adds a
projector.**

> 🌟 **Level 2 delivers most of CQRS's value for roughly none of its cost.** When someone says "we need
> CQRS", this is usually what they actually need.

### Level 3 — separate read *store* 💰

```
   command ──► write store (SQL, normalized)
                     │
                     └── events ──► projector ──► read store (Cosmos, Redis, Elasticsearch)
                                                        ▲
   query ───────────────────────────────────────────────┘
```

Two datastores, a synchronisation mechanism, and — unavoidably — **eventual consistency between them.**

---

## 2. ⚠️ The bill for level 3: your own writes disappear

```
  query the read store      : 50/50 orders missing
```

**Every single one.** Place an order, land on the list, and it isn't there. This is
[§8.1's read-your-writes problem](../Theory/DistributedTheory.md), and it is not an edge case — it's
the *normal* path, because a user's next action always follows their last one.

> 🌟 **With a separate read store, "I saved it and it's not in the list" is the default behaviour.**

### It is not a defect — it is an obligation

```
Engineer: "That's not a bug, it's eventual consistency."
User:     "I placed an order and it vanished."
```

Both are correct. The architecture is behaving as designed, **and the product is broken.** Choosing
level 3 means you have signed up to handle this in the UX; shipping without handling it is the bug.

⚠️ And "the user can refresh" is **not** a fix. Refreshing doesn't cause the sync — the projector was
catching up anyway. The user is performing your retry without knowing it.

### The five ways out, cheapest first

```
  return from the command   :  0/50 missing,  0 projection wait(s)   ← free
  wait for the projection   :  0/50 missing, 50 projection wait(s)   ← correct, costs latency
  synchronous projection    :  0/50 missing,  0 projection wait(s)   ← ⚠️ see below
```

**① Return the result from the command.** The write side already has the order:

```csharp
var order = await _handler.Handle(command);
return Created($"/orders/{order.Id}", order);   // 201 + body. No query.
```

🌟 **Going back to the read store to ask about data you just wrote is the mistake.** Most "read after
write" is really *"show me what I just did"*, and this covers it for free.

**② Optimistic UI** — the client inserts locally and reconciles. Client-side only; wrong if the write can fail.

**③ Read your own writes from the write store** — route this user's queries to SQL for a few seconds.

**④ Wait for the projection** — the command returns a version, the query blocks until the read model
reaches it. That is a [Cosmos session token](../../Databases/Cosmos/ConsistencyLevels/ConsistencyLevels.md),
and the demo shows the cost: **50 waits for 50 writes.**

**⑤ Synchronous projection** — update the read model in the same transaction.

> 🧠 **⑤ undoes the reason you went to level 3.** Look at its row: identical to level 2's behaviour. If
> you need synchronous projections, you wanted level 2 — and level 2 is simpler and cheaper.

---

## 3. How the two sides stay in sync

```
SYNCHRONOUS — same transaction
   command ──► write store ──► read model      ✅ always consistent
                              (one transaction) ❌ slower writes, coupled, same DB only

ASYNCHRONOUS — via events
   command ──► write store ──► event ──► projector ──► read store
                                                      ✅ independent, scalable, replayable
                                                      ⚠️ eventually consistent, and you now own a
                                                         projector that can fall behind
```

The async version is [§8.2b](../Messaging/Logs/Logs.md) wearing a different hat — **the projector is a
consumer group**, so everything there applies:

- **Projection lag is consumer lag** — same metric, same alert, same per-partition diagnosis
- **The projector must be idempotent** — at-least-once means it will see events twice
- **A poison event blocks the projection**, and the read model silently stops updating
- **Replay rebuilds the read model** — delete it, reprocess the log, and it's correct again

> 🌟 **Being able to delete the read model and rebuild it is the single best argument for level 3.** It
> makes the read side disposable, so a bug in a projection is fixable without touching source data.

---

## 4. ⚠️ A stalled projector is invisible

```
  projector healthy                   : projected 100/100, lag  0, read store healthy = True
  projector stalled on a poison event : projected  50/100, lag 50, read store healthy = True
```

Note the last column is `True` in **both** rows. When a projector stalls:

```
API health check   ✅ 200 OK          write store      ✅ accepting orders
read store         ✅ up, 12ms        error rate       ✅ 0%
order list         ✅ 200 OK with data — from Saturday
```

**Every dashboard is green.** Nothing throws, nothing 500s. The read store is up and fast; it is simply
answering with old data.

> 🌟 **A frozen read model is invisible to every health check you already have.** Stale data is
> indistinguishable from correct data unless you measure *freshness* specifically.

What must exist from day one:

| Alert | Why |
|---|---|
| **Projection lag, in time** | "the read model is 3 hours behind" is actionable; "374 events" isn't |
| **Projector liveness** | "events projected in the last 5 min = 0 while published > 0" |
| **Projector DLQ depth** | ⚠️ and if you didn't build one, a poison event *has* to stall — that gap turns a one-event problem into a three-day outage |
| **Read-model freshness** | `max(ProjectedAt)` — catches causes you didn't anticipate |

> 🧠 **Level 3 adds a component with no user-visible failure mode.** Its lag metric is part of the cost,
> not an optimisation.

---

## 5. CQRS is not event sourcing

The commonest conflation in the field:

| | Question it answers | |
|---|---|---|
| **CQRS** | *how do I **model** reads vs writes?* | about **models** |
| **Event sourcing** | *how do I **store** state?* | about **storage** (§8.4) |

```
CQRS without event sourcing   ✅ very common — SQL write store, Redis read store
Event sourcing without CQRS   ✅ possible, awkward
Both together                 ✅ they pair well, which is why people fuse them
Neither                       ✅ most applications
```

> 🌟 **You can adopt CQRS and keep your boring SQL database.** There is no event store requirement
> anywhere in the pattern.

⚠️ And the related error: *"we'll replay events to rebuild state on every read."* Nobody does this, even
in real event sourcing — an aggregate with 50,000 events would take seconds to load, which is what
**snapshots** are for. In CQRS the read model exists **precisely so you never replay on a read**:

```
events ──► projector ──► read model ──► query reads it directly   ✅ one lookup
NOT:  query ──► replay 50,000 events ──► answer                   ❌
```

Replay rebuilds a **projection** — offline and occasional — it is not a query path.

> 🧠 The failure mode: a team adopts "CQRS" meaning *event sourcing + separate read store + a message
> bus*, hits every problem of all three at once, and concludes CQRS doesn't work. **It is three
> decisions. Make them one at a time.**

---

## 6. 🌟 Choosing the level — per use case, not per system

```
  order list page      -> level 2  (the query is slow - index, flat DTO or materialized view)
  order detail page    -> level 2  (a separate read model in the same database is enough)
  full-text search     -> level 3  (needs a different kind of store, and staleness is acceptable)
  analytics dashboard  -> level 3  (lopsided read load worth scaling independently)
  account balance      -> level 2  (different store would suit, but it cannot tolerate staleness)
```

**One system, five use cases, five answers.**

> 🌟 **"We're a CQRS shop" is the same mistake as "we're an AP shop"** ([§8.1](../Theory/DistributedTheory.md)).
> It's a decision per feature.

### The discriminator

> 🌟 **Level 3 is justified by a different *technology* or a different *scaling profile* — never by
> "the query is slow".**

"Slow" is an optimisation problem. Work the ladder first:

```
① read the query plan          — is it even using an index? (§2.1.2)
② add a covering index         — hours of work, often 8s → 50ms
③ project to a flat DTO        — stop loading the object graph for a list page
④ indexed / materialized view  — the DATABASE maintains the denormalization, transactionally,
                                 with no projector to operate at 3am
⑤ cache the page               — the cheap version of what level 3 does
────────────────────────────── everything above is level 2 ──────────────────────────────
⑥ separate read store          — two datastores, a projector, eventual consistency
```

④ deserves emphasis: **an indexed view *is* a maintained denormalized projection**, kept in sync by the
database with no eventual consistency. The cost is slower writes — which at a 100:1 ratio is exactly
the trade you wanted.

### The worked case: full-text search

Product wants typo tolerance and relevance ranking:

```
"typo tolerance"     → fuzzy matching, edit distance
"relevance ranking"  → TF-IDF / BM25
"full-text"          → tokenizing, stemming, stop words, language analysers
```

`LIKE '%smith%'` gives none of that and can't use an index. **This is a capability gap, not a
performance gap** — no index, view or cache closes it.

And all four conditions hold at once, which is what makes search the textbook case:

```
✅ different technology required  — SQL can't rank or fuzzy-match
✅ staleness acceptable           — and EXPECTED: Google doesn't index your page instantly
✅ read-heavy                     — search is all reads
✅ rebuildable                    — reindex from the source any time
```

⚠️ But scope it: **only search goes to level 3.** The list, detail page and reports stay at level 2 on
SQL.

### When NOT to

```
✅ reads and writes have genuinely different shapes (not just "reads are slow")
✅ the ratio is lopsided — 100:1 or more
✅ you need to scale them independently
✅ the read side needs a different technology
✅ the team can operate two datastores and a projector at 3am
✅ the business can tolerate a stale read, and someone has SAID SO OUT LOUD
```

⚠️ The real cost is organisational: a junior joining now has to understand two models, the projector,
and why the list is sometimes stale. **Complexity you add is paid by everyone, forever.**

---

## 7. WHO decides — the architect's view

- **Start at level 2.** Move to level 3 when you can name the specific problem it solves
- **Decide the level per use case**, and write the decision down as an ADR
- **Staleness is a product decision.** If nobody can say how stale is acceptable, you aren't ready
- **Owe every projector a lag alert on day one** — it has no user-visible failure mode
- **Return data from commands.** It is free and removes most read-after-write pain
- **Keep CQRS and event sourcing as separate decisions**, taken months apart if at all

> 🌟 **The question an architect adds is rarely the answer — it's the question that stops the team
> committing a quarter to the wrong one.**

---

## 8. Demo output

```
100 order-list page loads:
  level 0/1 — load aggregates : 6 table(s), 240,000 rows, eventually consistent = False
  level 2 — flat read model   : 2 table(s),   5,000 rows, eventually consistent = False
  level 3 — separate store    : 1 table(s),   5,000 rows, eventually consistent = True

Place an order, then immediately show it (50 times, projection 3 behind):
  query the read store      : 50/50 orders missing,  0 projection wait(s)
  return from the command   :  0/50 orders missing,  0 projection wait(s)
  wait for the projection   :  0/50 orders missing, 50 projection wait(s)
  synchronous projection    :  0/50 orders missing,  0 projection wait(s)

100 orders written, and the read model's freshness:
  projector healthy                   : projected 100/100, lag  0, read store healthy = True
  projector stalled on a poison event : projected  50/100, lag 50, read store healthy = True

One system, five use cases, five answers:
  order list page      -> level 2
  order detail page    -> level 2
  full-text search     -> level 3
  analytics dashboard  -> level 3
  account balance      -> level 2
```

---

## 9. Warm-up questions

1. A team wants a Cosmos read store because a 6-table join takes 8 seconds. What do you ask first, and
   what's the cheaper ladder?
2. A user places an order and it isn't in the list. Bug or not? What's the cheapest fix?
3. "We're adopting CQRS so we need an event store." What's confused — and what's wrong with
   "replay on every read"?
4. The read model froze on Saturday and nobody noticed until Tuesday. Why did every dashboard stay
   green, and what should have alerted?
5. Product wants full-text search with typo tolerance. Which level, why, and what stays behind?
6. Why does a *synchronous* projection suggest you wanted level 2?
7. Which of these justifies level 3: "reads are slow", "reads are 300× writes and can be a minute
   stale", "we need geospatial queries"?

---

## 10. The three to remember

1. 🌟 **CQRS is a spectrum. Level 2 — separate read models, one database — is what most teams need**
2. 🌟 **Level 3 is justified by a different *technology* or *scaling profile*, never by "the query is
   slow"**
3. 🌟 **CQRS is about models; event sourcing is about storage. Independent decisions**
