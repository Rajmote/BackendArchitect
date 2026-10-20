# Distributed Systems · Messaging — Part B: logs

> **Where this sits:** Technology `Distributed Systems` → Main topic `Messaging & streaming` → Sub topic `Logs`.
> Runnable code: [`EventLog.cs`](EventLog.cs) · [`LogScenarios.cs`](LogScenarios.cs) ·
> [`LogsDemo.cs`](LogsDemo.cs).
>
> **Part B of §8.2** — Kafka and Azure Event Hub. Read [Part A — queues](../Queues/Queues.md) first;
> this note contrasts against it throughout.

---

## The one sentence

> **A queue is a to-do list — you take an item and it's gone. A log is a diary — you read it, and it's
> still there tomorrow, for you and for everyone else.**

```
QUEUE                              LOG
┌───┬───┬───┬───┐                  ┌───┬───┬───┬───┬───┬───┐
│ D │ C │ B │ A │                  │ A │ B │ C │ D │ E │ F │  append-only
└───┴───┴───┴───┘                  └───┴───┴───┴───┴───┴───┘
  take A → DELETED                   0   1   2   3   4   5   ← offsets
                                             ▲       ▲
                                          group-1  group-2
                                     nothing is removed on read
```

One structural change — **reading doesn't consume** — and everything else follows:

| | Queue | Log |
|---|---|---|
| After consumption | **deleted** | **retained** (by time or size) |
| Who tracks position | the **broker** (locks) | the **consumer** (an offset) |
| Replay history | ❌ impossible | ✅ rewind the offset |
| Several independent readers | topic + a subscription each | **native** — add a consumer group |
| A consumer added *later* | sees only new messages | ✅ can read **from the beginning** |
| Scaling ceiling | none | **partition count** |
| Per-message DLQ, TTL, retry count | built in | ❌ you build it |
| One poison message blocks… | nothing | **its whole partition** |

---

## 1. WHAT — partitions

```
topic "orders"
  partition 0: [ o1 ][ o4 ][ o7 ]     ← ordered WITHIN a partition
  partition 1: [ o2 ][ o5 ][ o8 ]
  partition 2: [ o3 ][ o6 ][ o9 ]
          offset 0     1     2        ← per partition, independent counters
```

> 🌟 **Ordering is guaranteed within a partition and nowhere else.**

The producer chooses by hashing a key:

```csharp
producer.Send(new EventData(payload), partitionKey: order.CustomerId);
```

⚠️ Same rules, same failure as a [Cosmos partition key](../../../Databases/Cosmos/PartitionKeys/PartitionKeys.md)
and a [pipeline lane](../../../Concurrency/Pipelines/Pipelines.md): **spread evenly, keep related
things together.** `orderStatus` as a key puts 90% of traffic on "Completed".

**This is Part A's session with the lane made visible.** A session hides which consumer; a partition is
a countable thing you provision — and therefore a thing you can run out of.

---

## 2. Consumer groups — and the ceiling

A **consumer group** is a named team with its own set of offsets. The broker stores one position per
`(group, partition)`:

```
            partition 0        partition 1        partition 2
group "billing"   offset 3          offset 2          offset 1
group "analytics" offset 0          offset 0          offset 0   ← replaying from the start
```

Both groups see **every** message. They don't compete and don't know about each other.

> 🌟 **Within a group, a partition is assigned to exactly ONE consumer. Partition count is therefore a
> hard ceiling on parallelism inside that group.**

```
  1 consumer(s): 1 working, 0 idle
  2 consumer(s): 2 working, 0 idle
  3 consumer(s): 3 working, 0 idle
  5 consumer(s): 3 working, 2 idle     ⚠️ scaling past the partition count buys NOTHING
```

The two idle consumers aren't useless — they're **standby**: if one dies the group rebalances and a
spare takes its partition. But they add no throughput.

### Why a partition can't be shared

```
partition 1: [ o2 ][ o5 ][ o8 ]
               ▲      ▲
          consumer B  consumer C      ← both committing offsets
```

An offset is **one number meaning "everything before here is done."** B is on 0, C commits 1 → the
broker believes o2 is handled. It isn't. B crashes and **o2 is silently lost**. A single number cannot
describe two independent positions.

> 🧠 **One consumer per partition is what makes "ordered" and "offset" possible at all.**

### ⚠️ Changing the partition count is a one-way door

```
BEFORE: 3 partitions   customer-42 → hash % 3 = partition 1
AFTER:  6 partitions   customer-42 → hash % 6 = partition 4
```

The key **moved**. Its history is in partition 1 and its future in partition 4, so per-key ordering
breaks across the change. Over-provision slightly at design time; under-provisioning caps your
throughput permanently.

---

## 3. 🌟 Offsets — two lines decide your delivery guarantee

```csharp
// AT-MOST-ONCE — commit first
await consumer.CommitAsync(offset);
await ProcessAsync(message);          // 💥 crash → LOST forever

// AT-LEAST-ONCE — process first
await ProcessAsync(message);
await consumer.CommitAsync(offset);   // 💥 crash → REDELIVERED
```

**That is the entire difference.** In Part A the broker owned this through locks and you chose with an
enum; here **you own it**, and you choose by where you put one line.

```
  commit, then process  : 40/50 applied, 10 lost,  0 duplicated
  process, then commit  : 50/50 applied,  0 lost, 10 duplicated
```

### Why at-most-once is the dangerous one

```
offset 500 committed  ──►  "everything before 501 is done"
      💥 killed
restart → reads from 501
```

Message 500 is **never redelivered**. The bytes are still in the log — a log doesn't delete on read —
but nothing will ever point at them again.

> 🌟 **At-most-once loses silently, and your dashboards look BETTER the more you lose.** Lag reads
> zero, because you committed everything.

| | Crash means | How you find out |
|---|---|---|
| **at-most-once** | message **lost** | ⚠️ you don't |
| **at-least-once** | message **duplicated** | a duplicate key, a double charge — loudly |

> 🧠 **Prefer the failure mode you can detect.** Duplicates are defensible with idempotency; data you
> never knew you had is not.

At-most-once has real uses — metrics, telemetry, log shipping. For anything a customer can see, it's
wrong.

⚠️ **Batch commits widen the redelivery window.** Commit every 100 and a crash replays up to 100 — a
deliberate throughput/duplication trade.

> 🌟 **Pick at-least-once and make the consumer idempotent.** Fourth arrival at this answer:
> [§3.1 keys](../../../Apis/Http/Fundamentals/HttpFundamentals.md),
> [§4.5 pipelines](../../../Concurrency/Pipelines/Pipelines.md),
> [§8.1 timeouts](../../Theory/DistributedTheory.md), Part A's peek-lock.

---

## 4. 🌟 Retention and replay — the architectural difference

```
retention = 7 days   → anything older is deleted, read or not
retention = forever  → the log IS your database (event sourcing, §8.4)
compacted            → keep only the LATEST value per key: a queryable snapshot
```

### The superpower

```
30 orders published, then the fraud team subscribes NEXT QUARTER:
  topic + subscriptions :  0 record(s) available to them
  log (replay from 0)   : 30 record(s) available to them
```

**This is what settles "topic or log?"** A subscription only receives messages published *after it
exists*. Several consumers today → a topic will do. Several consumers **plus a future one who needs the
past** → only a log.

And it's a one-way door: choose the topic now and in three months the fraud team's history doesn't
exist, because it was deleted on acknowledgement months ago.

Replay also buys: reprocessing after a bug fix, rebuilding a projection from scratch, and debugging
production by replaying the exact sequence that broke it.

### ⚠️ The matching danger

Retention is a **deletion policy**, not a safety net:

```
A consumer group is down, 10 events arriving per hour, retention 48h:
  down 24h:   0 record(s) deleted before anyone read them
  down 60h: 120 record(s) deleted before anyone read them
```

```
Friday 18:00   consumer stops, last committed offset = 1,000,000
Sunday 18:00   retention deletes everything older than 48h — including the unread backlog
Monday 06:00   consumer asks for offset 1,000,001 → it no longer exists
```

The stored position is now **before the earliest surviving message**, so a reset policy applies:

| Policy | Result |
|---|---|
| `earliest` | resume from the oldest survivor — **12 hours silently skipped** |
| `latest` | jump to the end — **60 hours silently skipped** 💥 |

**No exception, no DLQ**, and lag reads healthy within minutes. Note which default is worse: `latest`
discards the entire outage, and teams pick it to "catch up quickly" after exactly the incident whose
backlog they were trying to recover.

> 🌟 **Past the retention window, consumer lag stops being a performance problem and becomes data loss.**

**What should have happened:** alert on lag measured in **time**, not messages (250,000 might be two
minutes or two days), at 50% and 80% of retention; alert on a consumer group having **zero members**;
and size retention for your worst recovery, not your typical one — 48 hours doesn't survive a long
weekend, and long weekends are when nobody is looking.

> 🧠 **A queue forgets what's done. A log forgets what's old.** Part A would have lost nothing here —
> messages sit in a queue until acknowledged. The window is the price of replay.

---

## 5. Consumer lag — the metric that runs your week

```
Consumer lag, broken out per partition:
  partition 0: lag    20
  partition 1: lag  5800  <- hot key, or a stuck consumer
  partition 2: lag     5
  partition 3: lag    12
  total lag 5,837 looks like 'scale out'; per-partition says 3 of 4 are healthy
```

| Signal | Means |
|---|---|
| lag high on **all** partitions | capacity → scale out (up to the partition count) |
| lag high on **one** partition | ⚠️ that partition — **scaling is the wrong lever** |
| lag approaching retention | 🚨 data loss is next |

> 🌟 **Per-partition lag is the most valuable number a log gives you** — it separates "we need more
> consumers" from "one consumer is stuck", and those have opposite fixes.

Two causes for a single hot row, distinguished by **the incoming rate per partition**:

```
partition 1 incoming ≫ others  → hot partition  → fix the key (a repartition 😬)
partition 1 incoming ≈ others  → stuck consumer → read its logs for a repeating error
```

A second tell: a stuck consumer's **committed offset stops moving entirely**; a hot partition still
makes progress, just not fast enough.

⚠️ Over a network you cannot block the producer, so **there is no backpressure** — the pressure appears
as lag. This is [§4.5's queue depth](../../../Concurrency/Pipelines/Pipelines.md) and Part A's active
count under a third name, and the rule holds: *the queue that fills points at the problem.*

---

## 6. ⚠️ A poison message blocks its partition

```
partition 1: [ 4,199 ][ 4,200 ][ 4,201 ][ 4,202 ][ 4,203 ] ...
                                   ▲
                              stuck forever
                        4,202 onward are fine — and unreachable
```

You cannot commit 4,202 while 4,201 is unhandled, because one number would claim both are done.

```
20 records in one partition, offset 7 is malformed:
  retry forever             : committed to offset  7/20, 0 dead-lettered, blocked = True
  dead-letter, then commit  : committed to offset 20/20, 1 dead-lettered, blocked = False
```

> 🌟 **One bad message stops one partition, not the topic** — better than stopping everything, worse
> than Part A, where only that one message was affected.

### What a log does NOT give you

| | Part A — queue | Log |
|---|---|---|
| Per-message retry counter | ✅ `DeliveryCount` | ❌ none |
| Automatic parking after N failures | ✅ `MaxDeliveryCount` → DLQ | ❌ none |
| A dead-letter queue | ✅ **exists already** | ❌ **you create a topic** |
| Does one bad message block others? | ❌ no | ✅ **the whole partition** |

A queue tracks each message **independently** (its own lock, its own count). A log tracks a
**position**, and a position cannot have a hole in it.

```csharp
var attempts = 0;
while (true)
{
    try { await ProcessAsync(message); break; }
    catch (Exception ex) when (IsTransient(ex) && ++attempts < 3)
    {
        await Task.Delay(Backoff(attempts));             // §6.2, with jitter
    }
    catch (Exception ex)
    {
        await _dlqProducer.SendAsync(message);           // a separate "orders-dlq" topic
        _logger.LogError(ex, "Dead-lettered partition {P} offset {O}", message.PartitionId, message.Offset);
        break;
    }
}

await consumer.CommitAsync(message.Offset);              // ✅ on BOTH paths, or nothing moves
```

> 🧠 **Parking the message is only useful because it lets you commit past it.** Dead-letter *and*
> commit.

⚠️ And the trade you're making: **dead-lettering breaks per-key ordering.** Offset 4,201 is parked and
4,202 proceeds — if they're the same order, you've processed `Cancelled` without `Created`. For
financial events, **a stalled partition and a page is sometimes the right answer.**

---

## 7. Rebalancing

When a consumer joins, leaves or dies, partitions are reassigned:

```
before:  A→[p0,p1]  B→[p2]
B dies
after:   A→[p0,p1,p2]
```

⚠️ **A stop-the-world rebalance pauses the whole group.** Deploy 10 pods one at a time and you trigger
10 rebalances. *(Cooperative/incremental rebalancing reduces this — worth enabling.)*

⚠️ **In-flight work is redelivered.** A consumer mid-message when its partition moved never committed,
so the new owner starts from the last committed offset. **Rebalancing is a redelivery event** —
idempotency again.

---

## 8. Azure, concretely

| | Model | Use for |
|---|---|---|
| **Event Hub** | **log**, Kafka-protocol compatible | event streams, telemetry, high throughput |
| **Service Bus** | **queue/topic** (Part A) | commands, workflows, built-in DLQ + sessions |
| Event Grid | push notification | reactive glue, not a stream |

```
partition        = partition
consumer group   = consumer group
checkpoint       = committed offset
throughput unit  = capacity (1 MB/s in, 2 MB/s out)
```

⚠️ **The one that catches people: Event Hub checkpoints live in Blob Storage, not the broker.**

```
   Event Hub ──► your consumer ──► checkpoint written to a BLOB CONTAINER you own
```

It is **your** state in **your** storage account. Wipe that container and every consumer restarts from
the beginning or the end. It needs backup and access control like any datastore — and two environments
pointed at one container will corrupt each other's position.

---

## 9. 🌟 Choosing

```
Is it a COMMAND — "do this, once, for me"?             → QUEUE
Is it an EVENT — "this happened, whoever cares"?       → LOG
Will anyone want to replay history?                    → LOG
Will you add consumers later who need the backlog?     → LOG
Do you need per-message DLQ, TTL, scheduling?          → QUEUE
Do you need per-message retry without blocking others? → QUEUE
Is throughput above ~10k msg/sec?                      → LOG
Do you need strict per-key ordering?                   → either (session / partition key)
```

> 🌟 **Commands go to a queue. Events go to a log.**

The naming tells you which you have. `SendInvoice`, `ChargeCard`, `ResizeImage` are **imperatives** →
queue. `OrderPlaced`, `PaymentReceived`, `ItemShipped` are **past tense** → log.

And when you can't tell, one question decides it:

> 🧠 **If a second consumer appeared, would that be a feature or a bug?**
> Two consumers on `SendWelcomeEmail` = two emails = a bug → queue.
> Two consumers on `OrderPlaced` = billing *and* shipping = the point → log.

---

## 10. WHO decides — the architect's view

- **Pick the partition count deliberately.** It is a scaling ceiling and a one-way door
- **Commit after processing**, and make the consumer idempotent. Rebalancing alone guarantees redelivery
- **Alert on lag in *time*, against retention** — not on a message count
- **Build the DLQ yourself.** A log gives you no retry count and no parking
- **Back up your checkpoint store**, and never share it between environments
- **Retention is a business decision**: how far back must anyone be able to replay?

> 🌟 **A queue forgets what's done; a log forgets what's old. Choose which kind of forgetting you can
> live with.**

---

## 11. Demo output

```
One consumer group against a 3-partition topic:
  1 consumer(s): 1 working, 0 idle
  2 consumer(s): 2 working, 0 idle
  3 consumer(s): 3 working, 0 idle
  5 consumer(s): 3 working, 2 idle

50 records, the process is killed on 1 in 5:
  commit, then process  : 40/50 applied, 10 lost,  0 duplicated
  process, then commit  : 50/50 applied,  0 lost, 10 duplicated

30 orders published, then the fraud team subscribes NEXT QUARTER:
  topic + subscriptions :  0 record(s) available to them
  log (replay from 0)   : 30 record(s) available to them

A consumer group is down, 10 events arriving per hour, retention 48h:
  down 24h:   0 record(s) deleted before anyone read them
  down 60h: 120 record(s) deleted before anyone read them

Consumer lag, broken out per partition:
  partition 0: lag    20
  partition 1: lag  5800  <- hot key, or a stuck consumer
  partition 2: lag     5
  partition 3: lag    12

20 records in one partition, offset 7 is malformed:
  retry forever             : committed to offset  7/20, 0 dead-lettered, blocked = True
  dead-letter, then commit  : committed to offset 20/20, 1 dead-lettered, blocked = False
```

---

## 12. Warm-up questions

1. `Commit()` then `Process()` — which guarantee, and what happens on a crash between them?
2. Why does at-most-once make your dashboards look *better* as you lose data?
3. 3 partitions, 5 consumers, one group. How many work, and why exactly?
4. Lag is 5,800 on one partition and under 20 on the other three. What does that rule out, and what are
   the two candidate causes?
5. A group is down 60 hours; retention is 48. What happens on restart, and why is there no error?
6. Offset 4,201 is malformed. What stalls, and what does a log not give you that a queue did?
7. Billing, shipping and analytics need `OrderPlaced` today; fraud launches next quarter wanting 6
   months. Topic or log — and which clause decides it?

---

## 13. The three to remember

1. 🌟 **Reading doesn't consume** — that one change buys replay and costs you retention, a partition
   ceiling, and your own DLQ
2. 🌟 **Commit order is the delivery guarantee; prefer the failure you can detect**
3. 🌟 **Per-partition lag separates "scale out" from "one consumer is stuck"**
