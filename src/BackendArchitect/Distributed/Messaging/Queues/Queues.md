# Distributed Systems · Messaging — Part A: queues

> **Where this sits:** Technology `Distributed Systems` → Main topic `Messaging & streaming` → Sub topic `Queues`.
> Runnable code: [`MessageQueue.cs`](MessageQueue.cs) · [`QueueScenarios.cs`](QueueScenarios.cs) ·
> [`QueuesDemo.cs`](QueuesDemo.cs).
>
> **§8.2 is in two parts.** This is **Part A — queues** (Azure Service Bus, RabbitMQ, SQS).
> **Part B — logs** (Kafka, Event Hub) is a separate note; nothing here involves partitions or offsets.
>
> Builds on [§4.5 pipelines](../../../Concurrency/Pipelines/Pipelines.md) and
> [§8.1 theory](../../Theory/DistributedTheory.md).

---

## The one sentence

> **A queue is a to-do list. A producer adds an item, one consumer takes it, and it's gone.**

```
   producer                   QUEUE                   consumer
      │                ┌───┬───┬───┬───┐                 │
      ├── send(D) ────►│ D │ C │ B │ A │ ────► take(A) ──┤
      │                └───┴───┴───┴───┘                 │
                         ▲             ▲
                      newest        oldest (next out)
```

| | |
|---|---|
| **Each message goes to exactly one consumer** | not a broadcast |
| **Once acknowledged, it's deleted** | storage shrinks as work completes |
| **The broker tracks everything** | the consumer holds no bookkeeping |

That last line is the one to hold on to: **the queue remembers what has been done.** A consumer can
crash, restart, or be replaced by a different machine — it keeps no state, because the broker has it.

---

## 1. WHAT — the lifecycle of one message

Every queue feature falls out of this diagram.

```
          ┌──────────┐
          │  ACTIVE  │  waiting, visible to any consumer
          └────┬─────┘
               │  a consumer receives it
               ▼
          ┌──────────┐
          │  LOCKED  │  invisible to everyone else, for `lock duration` (default 30s)
          └────┬─────┘
               │
     ┌─────────┼──────────────┬────────────────────┐
     │         │              │                    │
  Complete()  Abandon()    lock EXPIRES         DeadLetter()
     │         │              │                    │
     ▼         ▼              ▼                    ▼
  DELETED   back to        back to ACTIVE      the dead-letter
    ✅      ACTIVE         (consumer died        queue 📮
            (explicit       or was too slow)
             retry)
```

> 🌟 **A queue doesn't hand a message away — it lends it.** The message stays in the queue while
> locked, so a dead consumer costs nothing: the lock expires and the message returns. No heartbeat,
> no cleanup job, no coordination.

---

## 2. HOW — the two receive modes *are* your delivery guarantee

### Receive-and-delete → at-most-once

```
   queue ──► deleted immediately ──► consumer processes
                                           💥 crash → the message is GONE
```

One round trip, fastest. Use it when losing a message is acceptable: telemetry, metrics, log shipping.

### Peek-lock → at-least-once ✅ the default, and what you want

```
   queue ──► LOCKED (still in the queue) ──► processes ──► Complete() ──► deleted
                                                💥 crash
                                          lock expires → ACTIVE → redelivered
```

Measured — 50 messages, the consumer crashing on 1 in 5 before completing:

```
  receive-and-delete  : 40/50 applied, 10 lost,  0 redelivered
  peek-lock           : 50/50 applied,  0 lost, 10 redelivered
```

### ⚠️ But look at what at-least-once costs you

```
Crash AFTER the work, BEFORE Complete()
   → the database row EXISTS
   → the lock expires anyway
   → another consumer runs the handler AGAIN
```

The broker cannot tell "the consumer died before doing anything" from "the consumer finished and died
before acknowledging" — they produce an identical expired lock. That is
[§8.1's *unknown*](../../Theory/DistributedTheory.md), now on the broker side.

> 🌟 **Peek-lock guarantees you never lose a message. It cannot guarantee you process it only once.**
> The consumer must be **idempotent** — that is the other half of the contract.

```csharp
await _db.InsertIfNotExistsAsync(new Payment(order.Id, ...));   // PK on OrderId
if (await _processed.ContainsAsync(message.MessageId)) return;  // Exercise 01's shape
order.Status = OrderStatus.Paid;                                // set, don't increment
```

⚠️ The middle one is **check-then-act across processes** ([§4.3](../../../Concurrency/Locks/RaceConditionsAndLocks.md)).
Two redeliveries landing on two consumers both see "not processed". The check must be atomic **in the
database** — a unique constraint or a conditional insert — not an `if` in your handler.

| | Who guarantees it | What's left to you |
|---|---|---|
| Not lost | the **queue** (lock + redelivery) | nothing |
| Not stuck forever | the **queue** (delivery count → DLQ) | watch the DLQ |
| Not applied twice | ⚠️ **you** | idempotency |

---

## 3. ⚠️ Lock duration — the gotcha

The lock has a timeout: Service Bus defaults to **30 seconds**, max 5 minutes.

```
t=0    consumer A receives, lock until t=30
t=5    starts a slow payment call
t=30   ⏰ LOCK EXPIRES — the message returns to the queue
t=31   consumer B receives the SAME message and starts charging     💷 charge #2
t=45   A finishes, calls Complete() → 💥 MessageLockLostException
t=61   expires again → consumer C                                   💷 charge #3
...
after 10 deliveries → dead-lettered as "failed"
```

**The message was charged up to 10 times before being dead-lettered.** The DLQ says it failed; the
customer's statement says otherwise.

> 🌟 **Lock expiry doesn't just retry — it runs *concurrently* with the handler that's still working.**
> The first consumer is never told to stop.

Measured, with a handler that takes 45 ticks against a 30-tick lock:

```
  no renewal    : processed 100 times for 10 messages -> 90 duplicate side effect(s)
  lock renewed  : processed  10 times for 10 messages ->  0 duplicate side effect(s)
```

⚠️ The demo makes **every** handler slow so the effect is visible. In the realistic case it's a **p99**
problem: the typical call is 2 s and 1% take 45 s — rare per message, and at 10,000 messages/day that
is **100 broken messages every day**. The average says "fine"; the
[p99 says you have a daily incident](../../../Observability/Telemetry/Observability.md).

### The options, in order

| | Fix | Catch |
|---|---|---|
| ① | **Renew the lock** while working (`MaxAutoLockRenewalDuration`) | renewal is a network call — a blip and you're racing again |
| ② | **Raise the lock duration**, set from the p99 | capped at 5 min; a genuinely dead consumer now holds it invisible for longer |
| ③ | 🌟 **Move the slow work out of the handler** — receive, persist "ChargeRequested", `Complete()` in ~10 ms, let a worker do the 45 s call | the structural fix: nothing slow happens under a lock |
| ④ | **Idempotency** | required regardless — ①–③ lower the probability, none removes it |

> 🧠 **Tuning timeouts lowers the probability. Idempotency removes the consequence.**

---

## 4. Poison messages — delivery count and the DLQ

```
DeliveryCount 1 → fail → back to the queue
DeliveryCount 2 → fail → back to the queue
...
DeliveryCount 10 (MaxDeliveryCount) → 📮 DEAD-LETTER QUEUE, automatically
```

The DLQ is **built in** on Service Bus — a real queue you can read, fix and resubmit from. Messages
land there with a reason and don't expire by default.

But letting a **malformed** message use the whole budget is waste:

```
  let it retry to the limit   : 10 handler run(s), 1 dead-lettered
  classify, then dead-letter  :  1 handler run(s), 1 dead-lettered
```

Ten attempts, ten lock durations of delay, and it sat at the head of the queue throughout — for a
missing field that was never going to appear.

```csharp
try
{
    var order = Validate(message);
    await ProcessAsync(order);
}
catch (ValidationException ex)
{
    // PERMANENT — retrying cannot help.
    _logger.LogError(ex, "Order {MessageId} is malformed", message.MessageId);
    await receiver.DeadLetterMessageAsync(message, "InvalidSchema", ex.Message);
}
catch (Exception ex)
{
    // TRANSIENT — database blip, timeout, 503. Let the queue redeliver.
    _logger.LogWarning(ex, "Order {MessageId} failed, will retry", message.MessageId);
    await receiver.AbandonMessageAsync(message);
}
```

> 🌟 **Retry what might succeed; dead-letter what never will.** `MaxDeliveryCount` is a safety net for
> the failures you *didn't* classify, not the primary mechanism.

Same transient-vs-permanent split as [§6.2](../../../Reliability/Resilience/Resilience.md): retrying a
400 is just a slower 400. Note the log levels too — permanent is `Error` (someone must look), transient
is `Warning` (the system will handle it).

The two DLQ fields travel **with** the message, so whoever opens it in six weeks sees why:

```csharp
await receiver.DeadLetterMessageAsync(message,
    deadLetterReason: "InvalidSchema",                   // short, groupable
    deadLetterErrorDescription: "missing customerId");   // the human detail
```

⚠️ **Alert on DLQ depth > 0.** An empty DLQ usually means nobody is looking, not that nothing failed.

---

## 5. Scaling — competing consumers

```
                    ┌──────────────┐
   ┌───┬───┬───┐    │  consumer A  │ ◄─┐
   │ C │ B │ A │ ───┤  consumer B  │ ◄─┼── all competing for the same queue
   └───┴───┴───┘    │  consumer C  │ ◄─┘
                    └──────────────┘
```

> 🌟 **There is no ceiling.** 50 consumers on one queue is fine — the lock guarantees no two get the
> same message, and throughput scales until the broker or your database is the limit.

Autoscale on queue depth and bursts handle themselves.

### ⚠️ What scaling costs you: ordering

```
   queue: [ Created ][ Updated ][ Cancelled ]     ← the SAME order

   consumer A takes Created    → 300ms
   consumer B takes Updated    →  10ms  ✅ finishes first
   consumer C takes Cancelled  →  10ms  ✅ finishes second

   processed: Updated, Cancelled, Created   💥
```

**A queue is FIFO going in, not coming out.** The broker dequeued them in order; the *concurrency*
reordered them. No dequeue policy — priority included — can fix that, because the problem is
overlapping handlers, not handover order.

> 🌟 **Ordering survives only if a related group of messages is handled by ONE consumer, one at a time.**

### The fix: sessions

```csharp
await sender.SendMessageAsync(new ServiceBusMessage(body) { SessionId = order.Id });
```

```
   session "order-42"  ─────► consumer A   locked to A, strictly in order ✅
   session "order-99"  ─────► consumer B   in parallel, independently ✅
```

```
  competing consumers   : 12 of 12 orders processed out of sequence
  sessions              :  0 of 12 orders processed out of sequence
```

> 🌟 **Ordering holds within a session; parallelism holds across sessions.**

Which is [§4.5's lane-per-key](../../../Concurrency/Pipelines/Pipelines.md), now enforced by the broker.

⚠️ A **hot session** is a bottleneck — one consumer, serial — so `SessionId = "all-orders"` destroys
throughput. And a stuck consumer stalls its session until the lock expires.

### Often you can avoid needing order at all

```csharp
order.Version++;                    // ❌ order-dependent
order.Status = Cancelled;           // ✅ commutative — any sequence is safe
if (incoming.Version <= stored.Version) return;   // ✅ detect disorder instead
```

§8.1's trio again: **preserve order (sessions), detect disorder (versions), or stop caring.**

---

## 6. Queue vs topic — one worker, or several teams?

```
   QUEUE — one logical consumer
      producer ──► [ queue ] ──► billing

   TOPIC — one copy per subscription
                            ┌──► [ sub: billing   ] ──► billing service
      producer ──► [ topic ]├──► [ sub: shipping  ] ──► shipping service
                            └──► [ sub: analytics ] ──► analytics service
```

Putting three *different* services on one queue is a silent data-loss bug:

```
  one shared queue        : billing 10, shipping 10, analytics 10    💥 of 30 orders
  topic + 3 subscriptions : billing 30, shipping 30, analytics 30    ✅
```

All three services are **running, healthy, logging success** — and each handled a random third. No
error, no DLQ, no alert. You find out when revenue doesn't match orders.

> 🌟 **Competing consumers is right for scaling ONE job and catastrophic for three DIFFERENT jobs.**
> Three instances of billing: correct. Billing + shipping + analytics: data loss.

Each subscription is a full queue in its own right — own locks, own delivery counts, own DLQ, own
filter. Shipping being slow cannot affect billing.

```sql
-- a subscription can take only what it cares about
sys.Label = 'OrderPlaced' AND amount > 1000
```

And you still scale each one: **3 instances of billing competing on the billing subscription** is
exactly right. Competing consumers *inside* a subscription; broadcast *across* subscriptions.

---

## 7. The metric: queue depth

```
active messages:      1,240  ← waiting, nobody has them
locked messages:         18  ← in flight right now
dead-lettered:            3  🚨 someone needs to look
```

| Signal | Means |
|---|---|
| depth **rising** | consumers can't keep up → add consumers |
| depth **flat and high** | steady overload, or consumers stuck |
| **lock expiry rate** rising | the handler is slower than the lock duration |
| **DLQ > 0** | a bug or bad data — always investigate |

**Alert on the trend, not the number.** 5,000 and falling is fine; 50 and rising is not. This is
[§4.5's queue depth](../../../Concurrency/Pipelines/Pipelines.md) with a broker behind it, and the same
rule applies: the queue that fills points at the bottleneck.

---

## 8. WHO decides — the architect's view

- **Peek-lock by default.** Receive-and-delete only where losing a message is genuinely acceptable
- **Set the lock duration from the p99**, and keep handlers short — accept durably, process elsewhere
- **Classify every failure** as transient or permanent. "It'll never throw" is not a classification
- **Alert on DLQ depth and on lock-expiry rate.** Both are silent otherwise
- **Decide ordering before you scale out** — retrofitting sessions is painful
- **One queue per job, not per message type.** Different consumers wanting the same message means a
  topic

> 🌟 **A queue carries a COMMAND — an instruction for exactly one worker.** "Do this" goes on a queue;
> "this happened" is Part B's question.

---

## 9. Demo output

```
50 messages, the consumer crashes on 1 in 5 before completing:
  receive-and-delete  : 40/50 applied, 10 lost,  0 redelivered
  peek-lock           : 50/50 applied,  0 lost, 10 redelivered

10 messages, handler takes 45 ticks, lock lasts 30:
  no renewal    : processed 100 times for 10 messages -> 90 duplicate side effect(s)
  lock renewed  : processed  10 times for 10 messages ->  0 duplicate side effect(s)

30 OrderPlaced messages, needed by billing, shipping AND analytics:
  one shared queue        : billing 10, shipping 10, analytics 10
  topic + 3 subscriptions : billing 30, shipping 30, analytics 30

One malformed message that can never succeed:
  let it retry to the limit   : 10 handler run(s), 1 dead-lettered
  classify, then dead-letter  :  1 handler run(s), 1 dead-lettered

12 orders x 3 events (Created, Updated, Cancelled), 5 consumers:
  competing consumers   : 12 of 12 orders processed out of sequence
  sessions              :  0 of 12 orders processed out of sequence
```

---

## 10. Warm-up questions

1. A consumer processes a message successfully and crashes before `Complete()`. What happens, and what
   does it demand of your code?
2. Handler p99 is 45 s, lock duration is 30 s. How often does it break, and what breaks *besides* the
   DLQ?
3. Three services need `OrderPlaced` and share one queue. What exactly goes wrong — and why is it worse
   than an outage?
4. A malformed message with `MaxDeliveryCount = 10`. What happens by default, and what should the
   handler do?
5. Three events for one order, five consumers. What goes wrong, and which feature fixes it? Why doesn't
   priority?
6. Name the three guarantees a queue gives you and the one it doesn't.

---

## 11. The three to remember

1. 🌟 **Peek-lock guarantees at-least-once. Idempotency is the other half of the contract**
2. 🌟 **Retry what might succeed; dead-letter what never will**
3. 🌟 **Queue = one worker does it · Topic = everyone who cares gets a copy · Session = ordering within
   a key**
