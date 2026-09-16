# Concurrency · Producer/consumer pipelines

> **Where this sits:** Technology `Concurrency & Async` → Main topic `Producer/consumer pipelines` → Sub topic `Stages, backpressure & failure`.
> Runnable code: [`OrderPipeline.cs`](OrderPipeline.cs) · [`PipelineFaults.cs`](PipelineFaults.cs) ·
> [`PipelinesDemo.cs`](PipelinesDemo.cs).
>
> Last topic of the concurrency block. In [§4.2](../Streams/TasksAndStreams.md) `Channel<T>` was a
> **data structure**. Here it becomes an **architecture**.

---

## 1. WHAT — stages connected by queues

```
 orders          ┌─────────┐        ┌─────────┐        ┌─────────┐
 arriving  ─────►│ validate│──ch1──►│  price  │──ch2──►│  store  │─────► done
                 └─────────┘        └─────────┘        └─────────┘
                   1 worker           4 workers          1 worker
```

A café is the same machine: **order taker → barista → server**, three stages with their own staffing,
connected by counters where cups wait.

**A worker is one loop reading from a channel and processing one item at a time:**

```csharp
async Task WorkerAsync(ChannelReader<Order> input, ChannelWriter<PricedOrder> output)
{
    await foreach (var order in input.ReadAllAsync())
    {
        var priced = await PriceAsync(order);
        await output.WriteAsync(priced);
    }
}
```

"4 workers" means that same loop started four times against the same channel. The channel hands each
item to **exactly one** of them.

So two numbers describe a stage: **latency** (one worker, one item) and **workers** (how many at once).

---

## 2. WHY — three things a pipeline buys you

1. **Stages run at different speeds without blocking each other.** Validation is 1 ms of CPU; storing is
   50 ms of I/O. Done in one method, every order pays 51 ms serially. As a pipeline, validation keeps
   working while storage waits on the database.
2. **Each stage scales independently** — the slow stage gets more workers, not the whole flow.
3. **Failure is contained** — a database blip stalls stage 3 while stages 1 and 2 keep working until the
   queue fills, which buys you time to recover.

---

## 3. 🌟 The one number that matters — the bottleneck

> **A pipeline's throughput equals its slowest stage. Nothing else.**

Do not confuse it with latency — they are different questions:

| | Question | Set by |
|---|---|---|
| **Latency** | how long does **one** order take end to end? | the **sum** of every stage |
| **Throughput** | how many orders finish **per second**? | the **slowest stage alone** |

For `validate 1ms → price 20ms → store 5ms`, one worker each: latency is **26 ms**, throughput is
**50/sec** — not 38. Once the pipeline is full, the stages work simultaneously on *different* orders:

```
time →      0ms    20ms    40ms    60ms
price:     [ A ]  [ B  ]  [ C  ]  [ D  ]      ← busy every moment, 1 order per 20ms
store:            [A]     [B]     [C]         ← 5ms of work, then idle 15ms 💤
validate:  [A][B][C][D]...                    ← 1ms of work, then idle 19ms 💤
```

An order comes out every 20 ms because store is already working while validate handles the one behind
it. And look at the idleness: **your bottleneck is busy 100% of the time and everything else is waiting
on it.** That is what a bottleneck looks like on a dashboard.

### 🌟 Queue depth tells you *where* it is

> **The queue that keeps filling sits immediately BEFORE the slow stage.**

Measured, 60 orders arriving every 8 ms:

```
  price x1 :   31.5 orders/sec  peak queues [validate 1, price 30, store 1]
  price x2 :   59.6 orders/sec  peak queues [validate 1, price  1, store 2]
  price x4 :   60.8 orders/sec  peak queues [validate 1, price  1, store 3]
  price x8 :   60.2 orders/sec  peak queues [validate 1, price  1, store 2]
```

Row 1 points straight at the guilty stage — **30 orders queued in front of `price`** while the other
queues sit at 1. No debugger, no profiler, one metric.

Rows 2–4 show the other half of the lesson:

> 🧠 **Fixing a bottleneck does not remove it — it moves it.** By `x2` the constraint is the arrival
> rate, and workers 3 through 8 buy nothing. Scale by **measuring queue depth**, never by guessing.

⚠️ The absolute rates here are depressed by Windows timer resolution (`Task.Delay` granularity is
~15 ms), so treat the numbers as *shape*, not as a benchmark. The shape is the lesson.

---

## 4. Backpressure propagates

You met backpressure in §4.2 with one channel. Across stages it walks **backwards**:

```
store is slow
   → ch2 fills
      → price blocks on WriteAsync
         → ch1 fills
            → validate blocks
               → the SOURCE stops accepting orders → 503 at the front door
```

Measured, with a slow final stage:

```
  bounded(8) : peak queues [validate 8, price 8, store 8]   ← pressure reached the front door
  unbounded  : peak queues [validate 1, price 1, store 30]  ← nothing pushed back; the backlog just grew
```

With **unbounded** channels nothing ever blocks. Orders keep arriving at 50/sec while `store` drains at
2/sec, so 48 pile up every second:

```
after  1 minute:  ~2,900 queued
after  5 minutes: ~14,400 queued
after 20 minutes: ~57,600 queued  →  💥 OutOfMemoryException
```

And the dangerous part: **nothing slows down, nothing errors, nothing warns you.** The front door still
reports 200 OK at full speed, dashboards look healthy — until the process dies and takes all 57,600
in-memory orders with it, every one of which was already acknowledged to a customer.

> 🌟 **Bounded channels convert "we ran out of memory" into "we slowed down."** One is a crash with data
> loss; the other is a graph you can act on. Unbounded is almost always the wrong default.

---

## 5. Shutdown — draining vs dropping

```csharp
channel.Writer.Complete();      // "no more items are coming" — consumers finish what's QUEUED, then exit
cancellationToken.Cancel();     // "stop NOW" — queued items are abandoned
```

**Completion drains. Cancellation drops.** You usually want both: complete the source, let each stage
drain, and keep cancellation as the deadline if draining takes too long.

### ⚠️ Bug 1 — a worker completing the next channel

```csharp
async Task WorkerAsync()
{
    await foreach (var order in ch1.Reader.ReadAllAsync())
        await ch2.Writer.WriteAsync(Price(order));

    ch2.Writer.Complete();     // ❌ this worker is not the stage
}
```

Worker 1 finishes marginally early and closes `ch2`. Workers 2–4 are still holding items:

```
  worker completes the channel: delivered 37/40, 3 worker(s) hit ChannelClosedException
  stage owner completes it    : delivered 40/40, 0 worker(s) hit ChannelClosedException
```

**Three orders lost.** And those exceptions are thrown inside unobserved worker tasks, so nothing is
logged — the pipeline reports a clean shutdown. It only happens **under load, at shutdown**, the two
conditions least covered by tests.

```csharp
var workers = Enumerable.Range(0, 4).Select(_ => WorkerAsync()).ToArray();

await Task.WhenAll(workers);   // ALL of them — and faults surface here instead of vanishing
ch2.Writer.Complete();         // only now is it true that no more items are coming
```

> 🌟 **`Complete()` is a statement about the whole STAGE, not about one worker.** Only whoever owns the
> stage may call it.

---

## 6. ⚠️ Bug 2 — one bad item kills the worker

```csharp
await foreach (var order in reader.ReadAllAsync(token))
{
    await ProcessAsync(order);     // 💥 throws on one malformed order
}
```

The exception unwinds the `await foreach`, and that loop **is the worker's entire life**. The worker does
not skip the item — it **dies**:

```
hour 0:  4 workers  →  100% throughput
hour 1:  3 workers  →   75%
hour 2:  2 workers  →   50%
hour 3:  1 worker   →   25%
hour 4:  0 workers  →  the queue fills and the service is dead
```

Degrading in steps, every step invisible, because an unobserved faulted `Task` logs nothing.

Measured — 200 orders, 4 workers, 1 in 10 malformed:

```
  try outside the loop  : processed  37/200, 0/4 workers alive,  0 dead-lettered
  try inside the loop   : processed 181/200, 4/4 workers alive, 19 dead-lettered
```

**The placement of the `try` is the entire fix.** Outside the loop it still catches the exception — and
the worker still dies.

```csharp
await foreach (var order in reader.ReadAllAsync(token))
{
    try
    {
        await ProcessAsync(order);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Order {Id} failed", order.Id);
        await deadLetter.Writer.WriteAsync(order, token);   // park it, keep going
    }
}
```

> 🌟 **A stage must survive a bad item. One poison message should cost you one message — not the worker,
> and not the pipeline.**

A **poison message** fails *every* time (malformed, or referencing something deleted). Retrying it
forever is an infinite loop; it belongs in a **dead-letter queue** — a place a human looks at.

---

## 7. ⚠️ Bug 3 — parallelism destroys ordering

```
one worker:    A → B → C          order preserved
two workers:   A ─┐
               B ─┴→  B, A, C     whoever finishes first
```

Usually fine. Sometimes fatal — `OrderCancelled` processed before `OrderCreated` is a real bug.
Measured, 12 orders × 6 events each on 4 workers:

```
  one queue, N workers  : 12 of 12 orders saw their events out of sequence
  partitioned by key    :  0 of 12 orders saw their events out of sequence
```

**Every single key was reordered.** The fix is to route every event for a key to the *same* worker:

```csharp
var lane = StableHash(orderEvent.OrderId) % laneCount;
await lanes[lane].Writer.WriteAsync(orderEvent);
```

**Ordering holds within a key; parallelism holds across keys.** You keep nearly all the throughput — you
only lose it when one key is disproportionately busy, which is a **hot partition**: the same problem and
the same word as a [Cosmos partition key](../../Databases/Cosmos/PartitionKeys/PartitionKeys.md). 🎯

⚠️ Use a **stable** hash. `string.GetHashCode()` is randomized per process in .NET, so the same id would
route differently after a restart and the guarantee quietly evaporates.
[`PipelineFaults.cs`](PipelineFaults.cs) uses FNV-1a.

### The other two ways out

| Strategy | How | When |
|---|---|---|
| **Preserve order** | partition by key | in-process, one consumer group |
| **Detect disorder** | version/sequence number, reject stale | distributed — the robust answer |
| **Stop caring** | commutative operations (`set status` not `decrement`) | when you can design for it |

> 🌟 **Three ways out of an ordering problem: preserve order, detect disorder, or stop caring.**
> Partitioning is cheapest; versioning is most robust.

*(Not to be confused with a **routing slip** — an itinerary carried inside the message listing the steps
it should visit. That is for dynamic workflows and sagas, not for ordering.)*

---

## 8. Delivery guarantees

| | Means | Cost |
|---|---|---|
| **At-most-once** | acknowledge, then process | messages **lost** on a crash |
| **At-least-once** | process, then acknowledge | messages **duplicated** on a crash |
| Exactly-once | — | ❌ does not exist across a network |

Real systems pick **at-least-once** and make the consumer **idempotent** — which is
[§3.1's idempotency keys](../../Apis/Http/Fundamentals/HttpFundamentals.md) arriving for the second
time. That is not a coincidence: it is *the* standard answer, and you now have it in two contexts.

---

## 9. The production layer

Hand-rolled `Channel<T>` pipelines ship perfectly well. Once the wiring gets complex — fan-out, fan-in,
per-block parallelism, linked completion — the proven library is **TPL Dataflow**
(`System.Threading.Tasks.Dataflow`, from Microsoft):

```csharp
var validate = new TransformBlock<Order, Order>(Validate,
    new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1, BoundedCapacity = 100 });

var price = new TransformBlock<Order, PricedOrder>(PriceAsync,
    new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 4, BoundedCapacity = 100 });

var store = new ActionBlock<PricedOrder>(StoreAsync,
    new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1, BoundedCapacity = 100 });

validate.LinkTo(price, new DataflowLinkOptions { PropagateCompletion = true });
price.LinkTo(store, new DataflowLinkOptions { PropagateCompletion = true });
```

Read what that bought:

| Hand-rolled | Dataflow |
|---|---|
| the completion cascade of §5, easy to get wrong | `PropagateCompletion = true` |
| bounded channels wired by hand | `BoundedCapacity` |
| N worker loops per stage | `MaxDegreeOfParallelism` |
| loop plumbing | declarative wiring |

And when a stage must cross a process boundary, the channel becomes **Azure Service Bus / Event Hub /
Kafka** — same concepts, same failure modes, plus the network. That is Month 4.

---

## 10. WHO decides — the architect's view

- **Bound every queue.** The capacity is a design decision: how much burst you absorb before pushing back
- **Publish queue depth as a metric.** It is the cheapest bottleneck detector in existence — and the
  natural bridge to [§5.1 observability](../../Observability)
- **Decide the failure policy per stage:** retry, dead-letter, or drop. "It'll never throw" is not a policy
- **Decide whether ordering matters** *before* adding workers — retrofitting partitioning is painful
- **At-least-once plus idempotent consumers** is the default. Design for duplicates, not against them
- **Keep stages single-purpose.** A stage doing two things cannot be scaled for either

> 🌟 **An in-process pipeline and a distributed queue have the same failure modes. Learn them here,
> where you can attach a debugger.**

---

## 11. Demo output

```
60 orders arriving every 8ms through validate(1ms) -> price(20ms) -> store(5ms):
  price x1 :   31.5 orders/sec  peak queues [validate 1, price 30, store 1]
  price x2 :   59.6 orders/sec  peak queues [validate 1, price  1, store 2]
  price x4 :   60.8 orders/sec  peak queues [validate 1, price  1, store 3]
  price x8 :   60.2 orders/sec  peak queues [validate 1, price  1, store 2]

A slow final stage (25ms), and what the queues in front of it do:
  bounded(8) : peak queues [validate 8, price 8, store 8]
  unbounded  : peak queues [validate 1, price 1, store 30]

200 orders, 4 workers, 1 in 10 malformed:
  try outside the loop  : processed  37/200, 0/4 workers alive,  0 dead-lettered
  try inside the loop   : processed 181/200, 4/4 workers alive, 19 dead-lettered

40 items, 4 workers, who calls Complete() on the next channel:
  worker completes the channel: delivered 37/40, 3 worker(s) hit ChannelClosedException
  stage owner completes it    : delivered 40/40, 0 worker(s) hit ChannelClosedException

12 orders x 6 events each, 4 workers:
  one queue, N workers  : 12 of 12 orders saw their events out of sequence
  partitioned by key    :  0 of 12 orders saw their events out of sequence
```

---

## 12. Warm-up questions

1. `validate 1ms → price 20ms → store 5ms`, one worker each. Latency? Throughput? Which stage decides
   each?
2. The database slows to 500 ms/order and every channel is unbounded. Describe the next twenty minutes.
3. Why does a `try` **around** the `await foreach` not save the worker?
4. Four workers, and one calls `Complete()` on the next channel. What do the other three see?
5. You scale a stage to 4 workers and `OrderCancelled` starts arriving before `OrderCreated`. Name
   three fixes and say which you would pick in-process.
6. Why is `string.GetHashCode()` the wrong hash for routing by key?
7. Which queue do you put on the dashboard, and what does it tell you?

---

## 13. The three to remember

1. 🌟 **Throughput is the slowest stage; latency is the sum of all of them — and the queue that fills
   points at the bottleneck**
2. 🌟 **Bounded channels turn "out of memory" into "slower"** — pressure that reaches the front door is
   pressure you can act on
3. 🌟 **A stage must survive a bad item, and only the stage owner may call `Complete()`**
