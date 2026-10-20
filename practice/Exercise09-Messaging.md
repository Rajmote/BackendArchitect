# Exercise 09 — a log consumer that survives a bad day

**Topic:** [§8.2b Messaging — logs](../src/BackendArchitect/Distributed/Messaging/Logs/Logs.md)
**Difficulty:** ⭐⭐
**Status:** 🔴 in progress — stubs and starter tests are in place.

---

## Scenario

An `orders` topic with 3 partitions. You own the consumer for the `billing` group. It has to keep
running through everything a log throws at it: duplicates, a malformed record, a rebalance, and an
outage longer than retention.

A log gives you none of the safety nets a queue does. You build them.

---

## What you implement

```csharp
public sealed class BillingConsumer
{
    public BillingConsumer(OrderLog log, string group, int maxAttempts, Func<LogRecord, bool> charge);

    public ConsumeReport ConsumeAll(int partition);
    public long CommittedOffset(int partition);
    public void SeekToBeginning(int partition);
    public IReadOnlyList<LogRecord> DeadLetters { get; }
    public int ChargesApplied { get; }
}

public sealed record ConsumeReport(int Processed, int DeadLettered, int DuplicatesSuppressed, bool PartitionBlocked);
```

`OrderLog` is a minimal append-only log supplied with the stub — the practice project is standalone, so
it does not reference the reference implementation.

## Requirements

| # | Requirement |
|---|---|
| 1 | **Process, then commit** — a crash must redeliver, never lose |
| 2 | The consumer is **idempotent**: a redelivered record applies its side effect once (`DuplicatesSuppressed` counts the rest) |
| 3 | A transient failure retries up to `maxAttempts` with the record still uncommitted |
| 4 | A permanent failure goes to `DeadLetters` **and the offset is committed**, so the partition keeps moving |
| 5 | `PartitionBlocked` is true only when the consumer genuinely cannot advance |
| 6 | Restarting mid-partition resumes from the committed offset, not from 0 |

## Acceptance criteria

- ✅ Requirements 1–6 hold
- ✅ 🌟 **Verify the tests can fail** — swap the commit to *before* processing and confirm the
  "nothing is lost" test goes red
- ✅ One test proves a record whose retries are exhausted is dead-lettered **and** committed. Parking
  without committing fixes nothing
- ✅ Say in one sentence what you gave up by dead-lettering rather than stalling

## Hints (read only when stuck)

<details>
<summary>Requirement 2 — where does idempotency live?</summary>

The offset tells you *where you are*, not *what you've done*. After a crash the consumer resumes from
the last commit and legitimately re-reads records it already applied. Keep a set of applied record
identities (the `(partition, offset)` pair, or a business key) and check it before the side effect —
the same shape as Exercise 01's receipt store.
</details>

<details>
<summary>Requirements 4 and 5 — why committing is the point</summary>

Offsets are sequential, so you cannot commit offset 8 while 7 is unhandled: one number would claim both
are done. Parking the record in `DeadLetters` and *not* committing leaves the partition exactly as
stuck as before. The reference demo shows it: `retry forever` stops at offset 7 of 20, and
`dead-letter, then commit` reaches 20.
</details>

<details>
<summary>Requirement 3 — classify before you retry</summary>

Retry what might succeed; dead-letter what never will. A malformed payload does not improve on the
third attempt — it just costs three attempts and delays everything behind it in that partition.
`maxAttempts` is the safety net for failures you did not classify.
</details>
