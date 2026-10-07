# Distributed Systems · theory

> **Where this sits:** Technology `Distributed Systems` → Main topic `Theory` → Sub topic `Failure, CAP, consistency, replication, consensus`.
> Runnable code: [`RemoteCallOutcomes.cs`](RemoteCallOutcomes.cs) · [`QuorumCluster.cs`](QuorumCluster.cs) ·
> [`ReplicaLag.cs`](ReplicaLag.cs) · [`DistributedTheoryDemo.cs`](DistributedTheoryDemo.cs).
>
> **First topic of Month 4.** Everything built so far assumed one process. Here the network arrives —
> and the network lies.

---

## 1. 🌟 The one thing that makes this hard

You call another service. You get a **timeout**.

```csharp
try
{
    await _payments.ChargeAsync(order);   // ⏱️ timeout after 30s
}
catch (TimeoutException)
{
    // ...did the customer get charged, or not?
}
```

**You cannot know.** Three different realities produce an identical timeout:

```
① the request never arrived              →  NOT charged     💷 safe to retry
② it arrived, was processed, and the      →  CHARGED ✅       💷 retrying charges TWICE
   response was lost on the way back
③ it arrived and is still running         →  about to charge 💷 retrying races it
```

⚠️ Knowing the **cause** does not help. A slow database, a congested network, an exhausted connection
pool — any cause produces any of the three. The question that matters is never *why was it slow*, it is
**did the side effect happen**.

| | Outcomes | Recoverable? |
|---|---|---|
| **Local call** | returned, or threw | yes — you know which |
| **Remote call** | succeeded, failed, or **unknown** | ⚠️ the third, never |

An ordinary exception is **information**. A timeout is the **absence** of information, and it is
*permanent* — waiting longer never resolves it, and asking again is a new call with its own three
outcomes.

> 🌟 **Everything in distributed systems is a strategy for coping with "unknown".** Retries,
> idempotency, consensus, CAP, sagas — all of it descends from this one problem.

### How each technique dodges it

| Technique | How it stops you needing to know |
|---|---|
| **Idempotency keys** ([§3.1](../../Apis/Http/Fundamentals/HttpFundamentals.md)) | the retry *is* the same operation, not a second one |
| **At-least-once + idempotent consumer** | duplicates are expected and absorbed |
| **Consensus** | a quorum agrees, so there *is* a fact to read |
| **Sagas** | assume partial completion; compensate explicitly |
| **CP systems** | refuse the write rather than be unsure |

Measured — 100 payments, 1 in 10 losing its *response* after the charge succeeded:

```
  plain retry           : 110 calls -> 110 charges, 10 customer(s) charged twice
  with idempotency key  : 110 calls -> 100 charges,  0 customer(s) charged twice
```

**The network behaved identically in both runs** — same 110 calls. The only difference is the server's
interpretation of the second one.

---

## 2. The 8 fallacies of distributed computing

| # | The lie | What it costs |
|---|---|---|
| 1 | **The network is reliable** | no retries, no breaker → cascading failure ([§6.2](../../Reliability/Resilience/Resilience.md)) |
| 2 | **Latency is zero** | chatty APIs; 100 calls × 2 ms = 200 ms of pure network |
| 3 | **Bandwidth is infinite** | returning the whole object graph "just in case" |
| 4 | **The network is secure** | no TLS between services, no authn inside the perimeter |
| 5 | **Topology doesn't change** | hard-coded IPs, no discovery, no reconnect |
| 6 | **There is one administrator** | your upgrade window is not their upgrade window |
| 7 | **Transport cost is zero** | serialization and egress are real money |
| 8 | **The network is homogeneous** | assuming everyone speaks your protocol |

⚠️ **#1 and #2 are the two that show up in code review.** A loop making a call per item is fallacy 2
wearing a `for`:

```csharp
foreach (var id in orderIds)                       // ❌ 500 orders = 500 round trips
    orders.Add(await _api.GetOrderAsync(id));

var orders = await _api.GetOrdersAsync(orderIds);  // ✅ one
```

Which is [GraphQL's N+1](../../Apis/GraphQL/Basics/GraphQlBasics.md). Same fallacy, different syntax.

---

## 3. CAP — with the slogan corrected

| | |
|---|---|
| **C**onsistency | every read sees the latest write |
| **A**vailability | every request gets a non-error response |
| **P**artition tolerance | the system keeps working when the network splits |

> 🌟 **"Pick two" is misleading. P is not a choice — networks partition whether you like it or not.
> You only ever choose between C and A, and only *during* a partition.**

A partition is when nodes are alive but cannot reach each other:

```
   ┌─────────┐        ╳╳╳╳        ┌─────────┐
   │ 3 nodes │ ──────╳ SPLIT ╳────│ 2 nodes │
   │ alive ✅ │        ╳╳╳╳        │ alive ✅ │
   └─────────┘                    └─────────┘
```

**CP — refuse the write.** *"I can't confirm this is safe, so I won't accept it."*
Bank ledger, inventory count, seat booking.

**AP — accept the write.** *"Take it, we'll sort it out."*
Shopping cart, social feed, view counter, DNS. Someone must then **resolve conflicts** on heal.

Measured, 5 nodes split 3|2, the same 10 keys written on both sides:

```
  CP (refuse)   : 10 accepted, 10 refused,  0 key(s) disagree once the split heals
  AP (accept)   : 20 accepted,  0 refused, 10 key(s) disagree once the split heals
```

Read the trade straight off the table. CP threw away **half its writes**; AP kept all 20 and bought
**10 conflicts** nobody can resolve without a policy or a human.

> 🧠 **A key only one side has is NOT a conflict** — it just replicates across on heal. A conflict is
> both sides holding *different* values with no way to tell which the world should believe.

⚠️ And CAP is **per-operation**, not per-system:

```
add to cart     → AP   (a duplicate line item is survivable)
take payment    → CP   (charging twice is not)
```

> 🌟 **"We're an AP shop" means someone stopped thinking.** It's an ADR per operation, not a preference.

### PACELC — the half people skip

CAP says nothing about normal operation, which is 99.9% of the time:

> **If Partition → A or C. Else → Latency or Consistency.**

Even on a healthy network, making a write visible everywhere takes time. Wait for every replica →
consistent but slow. Don't → fast but briefly stale. And you have already turned this dial:

```
Cosmos Strong    = PC/EC   ← consistent always, pay in latency
Cosmos Session   = PC/EL   ← the default
Cosmos Eventual  = PA/EL   ← fastest, weakest
```

[§2.3.4](../../Databases/Cosmos/ConsistencyLevels/ConsistencyLevels.md) was PACELC with a nicer UI.

---

## 4. Consistency models — a ladder, not a binary

| Model | Guarantee | Cost |
|---|---|---|
| **Linearizable** | acts like one copy; reads see the latest committed write | slowest — needs consensus |
| **Sequential** | everyone sees the same order (not necessarily the latest) | |
| **Causal** | if A caused B, nobody sees B before A | much cheaper than linearizable |
| **Read-your-writes** | *you* see your own writes; others may lag | a session token |
| **Eventual** | if writes stop, replicas converge | cheapest |

**Causal** is the sweet spot most teams want and few ask for: it forbids the genuinely confusing
anomalies (a reply appearing before the comment) without paying for global ordering.

### ⚠️ What "eventually consistent" actually promises

> **IF WRITES STOP, the replicas will converge.**

Read it carefully: conditional on **writes stopping**, with **no time bound anywhere**. "It'll catch up
in a few milliseconds" is an observation about a healthy day being quoted as a guarantee. Measured:

```
  writes stop       : lag 0, converged = True
  writes never stop : lag 3, converged = False
```

In a live system writes never stop, so the lag never reaches zero. It is bounded, not eliminated.

### The bug it produces

```csharp
await _orders.CreateAsync(order);                  // → the leader
var saved = await _ordersReadReplica.GetAsync(id); // → a replica that hasn't caught up
                                                   // 💥 null. The order you just created.
```

```
  read from replica : 50 of 50 reads returned null for an order that EXISTS
  read-your-writes  :  0 of 50
```

**Create-then-read is the commonest eventual-consistency bug in production.**

### What to say instead

> ❌ *"it's eventually consistent"*
> ✅ *"replication lag is p99 **80 ms**, alerted at 500 ms — and this screen tolerates 2 seconds"*

Two real questions: **what is the lag distribution** (a histogram — [§5.1](../../Observability/Telemetry/Observability.md), because the average will lie), and **what does the business tolerate**. An order
confirmation screen: no. A "customers also bought" panel: easily.

> 🌟 **"Eventually consistent" is not an architecture decision — it's an unanswered question.** The
> decision is *how much lag, and who agreed to it.*

---

## 5. Replication vs partitioning — two different words

```
REPLICATION — the SAME data, copied        PARTITIONING — DIFFERENT data, split
   node 1: orders 1..1,000,000                node 1: orders     1..333,333
   node 2: orders 1..1,000,000                node 2: orders 333,334..666,666
   node 3: orders 1..1,000,000                node 3: orders 666,667..1,000,000
```

| | Buys | Costs |
|---|---|---|
| **Replication** | **availability** + read scale | copies that can disagree |
| **Partitioning** | **write scale** + storage | cross-partition queries and transactions get hard |

> 🌟 **Replication survives a node dying. Partitioning survives the data not fitting.**

Replication does nothing for write throughput — every write still goes to every copy. Partitioning does
nothing for availability — lose a partition and that third of the orders is simply **gone**. So real
systems do both: partition the data, then replicate each partition.

Replication topologies:

```
single-leader   writes → leader → replicas    simple; the leader is a bottleneck and an SPOF
multi-leader    writes → any leader           geo-distributed; needs CONFLICT RESOLUTION
leaderless      writes → several nodes        Dynamo/Cassandra; quorums, R + W > N
```

And the partition-key rules are the ones you already know from
[§2.3.1](../../Databases/Cosmos/PartitionKeys/PartitionKeys.md) and
[§4.5](../../Concurrency/Pipelines/Pipelines.md): **spread evenly, keep related data together.** A bad
key gives a hot partition — same word, same failure, three different technologies.

---

## 6. Consensus — agreeing when the network can't be trusted

Some things need exactly one answer: who is the leader, was this committed, which config is live. The
algorithms are **Raft** and **Paxos**, and the mechanism is one line:

> **A majority (quorum) must agree. With 5 nodes, 3 must confirm.**

Why a majority is enough: **two majorities of the same set must overlap.** Any 3 of 5 shares at least
one node with any other 3 of 5, so two conflicting decisions can never both be accepted.

```
5 nodes, split 3 | 2
  majority side (3) → forms a quorum → accepts writes ✅
  minority side (2) → cannot reach 3 → REFUSES         ❌  (CP, deliberately)
```

> 🧠 That is why quorum systems use **odd** node counts: 4 nodes tolerate the same single failure as 3
> and cost more.

⚠️ **Consensus is expensive** — several round trips per decision. You don't run application traffic
through it; you use it for the small critical things (leader election, etcd/ZooKeeper, config) and
build everything else on top.

### ⚠️ And this is why a hand-rolled distributed lock is unsafe

"Each instance writes its name to a shared table; first one wins" is
[§4.3's check-then-act](../../Concurrency/Locks/RaceConditionsAndLocks.md) across a network. A `UNIQUE`
constraint fixes *that* part — but a lock needs a **lease**, and a lease brings this:

```
t=0    A claims the lock, lease 60s          ✅
t=5    A starts the job
t=10   A hits a 90-second GC pause            ⏸️  (A cannot observe time passing)
t=60   Lease expires. B claims it legitimately ✅
t=65   B starts the job
t=100  A wakes up, still believes it holds it  💥  BOTH are running
```

**A was never wrong** — it simply could not observe the pause. No amount of re-checking helps: there is
always a gap between "check" and "act" that a pause fits into. This is §1's *unknown* again.

The real fix is a **fencing token** — the lock issues a monotonically increasing number and the
**resource** rejects stale ones:

```
A holds token 33 → writes with 33
B holds token 34 → writes with 34
A wakes, writes with 33 → ❌ REJECTED, 33 < 34
```

Note where the safety lives: **in the resource, not the lock.** If your storage cannot reject a stale
token, your lock cannot be made safe.

> 🌟 **Don't build distributed coordination.** In order: make the job **idempotent** so running twice is
> harmless; else use a proven primitive (Azure Blob lease, etcd, ZooKeeper, a scheduler with
> single-execution guarantees); only then a leased lock *plus* fencing tokens.

---

## 7. WHO decides — the architect's view

- **Design for "unknown", not for failure.** Three outcomes, always. Idempotency turns the third into
  the second
- **Decide C-vs-A per operation and write it down.** An ADR, not a preference
- **"Eventually consistent" is an SLA question** — ask *how long*, then ask who signed up for it
- **Say which word you mean** — replication or partitioning. They solve different problems
- **Idempotency is the load-bearing primitive.** At-least-once delivery only works because of it
- **Buy consensus, never build it.** Raft is famously "the understandable one" and still takes experts
  years to get right

> 🌟 **In one process, failure is an exception. Across a network, failure is a *state* — and the hardest
> state is "I don't know."**

---

## 8. Demo output

```
100 payments, 1 in 10 loses its RESPONSE after the charge succeeded:
  plain retry           : 110 calls -> 110 charges, 10 customer(s) charged twice
  with idempotency key  : 110 calls -> 100 charges,  0 customer(s) charged twice

5 nodes split 3|2, both sides alive, 10 keys written on each side:
  CP (refuse)   : 10 accepted, 10 refused,  0 key(s) disagree once the split heals
  AP (accept)   : 20 accepted,  0 refused, 10 key(s) disagree once the split heals

Write an order, then immediately read it back (50 times, replica 3 behind):
  read from replica : 50 of 50 reads returned null for an order that EXISTS
  read-your-writes  :  0 of 50 reads returned null for an order that EXISTS

The guarantee is: IF WRITES STOP, the replicas converge.
  writes stop       : lag 0, converged = True
  writes never stop : lag 3, converged = False
```

---

## 9. Warm-up questions

1. A payment call times out. Name the three possible states of the payment, and why the *cause* of the
   timeout doesn't help.
2. 5 nodes split 3|2, a write arrives at the 2-node side. Name both behaviours and the trade-off.
3. Why is "pick two" a bad way to teach CAP?
4. `replicate the orders table` vs `partition the orders table` — what does each buy, and what does
   each fail to buy?
5. Someone says "it's eventually consistent, it'll catch up in a few ms". What's wrong with the
   sentence, and what would you ask for instead?
6. Why is 3 the quorum of 5, and why not use 4 nodes?
7. A colleague builds a distributed lock on a `UNIQUE` constraint. The race is gone. What is still
   broken, and what are your three options in priority order?

---

## 10. The three to remember

1. 🌟 **A remote call has three outcomes, and "unknown" never resolves** — idempotency is how you stop
   caring
2. 🌟 **P isn't a choice. You pick C or A, during a partition, per operation**
3. 🌟 **Replication survives a node dying; partitioning survives the data not fitting**
