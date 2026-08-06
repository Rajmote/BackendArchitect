# Exercise 01 — Idempotency keys

**Topic:** §3.1 HTTP fundamentals · **Difficulty:** ⭐⭐ · **Estimated:** 45–60 min

---

## Scenario

You're building the payments service for a food-delivery app. The mobile client calls
`POST /payments` to charge a customer.

Mobile networks are unreliable, so the client **retries on timeout**. But a timeout is an
**unknown** — the charge may have already succeeded and only the *response* got lost. Retrying blindly
double-charges people, and support will hear about it.

Your job: make the handler **safe to retry**, using an **idempotency key** supplied by the client.

## Requirements

Implement `IdempotentPaymentHandler` in
[`BackendArchitect.Practice/Exercise01/IdempotentPaymentHandler.cs`](BackendArchitect.Practice/Exercise01/IdempotentPaymentHandler.cs).

1. **First call with a key** → perform the charge, store the result against the key, return it.
2. **Repeat call with the same key** → **do not charge again**; return the **stored original response**,
   flagged as a replay.
3. **A different key** → a different logical operation → charge again.
4. **Reject an invalid request** — a null/empty key, or an amount of zero or less — **without** charging
   and **without** storing anything. (A rejected request must not "poison" the key.)
5. **Same key, different amount** → the client is misusing the key (it identifies *one* operation).
   Reject it rather than silently returning the old result — this is a real Stripe behaviour, and
   silently returning the wrong amount would be worse than an error.
6. **Thread safety** — two retries can arrive **simultaneously** on different threads. The customer must
   still be charged exactly once.

## Acceptance criteria

- [ ] 3 given tests pass
- [ ] You've added tests of your own for requirements **4, 5 and 6**
- [ ] `ChargesExecuted` reports how many times money actually moved
- [ ] No `async`/await needed — keep it synchronous and simple
- [ ] It compiles with **zero warnings** (the repo treats warnings as errors)

## Hints

- The store is just a dictionary keyed by the idempotency key — but see requirement 6.
- Think carefully about **what you store** and **when** you store it. Order matters when a failure can
  occur between charging and recording.
- Requirement 5 needs you to keep something extra alongside the stored result.

## What I'll review

1. **Correctness** — does it satisfy all six requirements?
2. **The tests you wrote** — did you test the *interesting* cases or just the happy path?
3. **Design** — naming, the shape of the result type, where responsibilities sit
4. **Idiomatic C#** — nullability, immutability, and correct use of the concurrency primitives

## ⚠️ Don't peek

`src/BackendArchitect/Apis/Http/Fundamentals/IdempotentPaymentApi.cs` contains a reference version.
**Make your own attempt first** — it's simpler than what this exercise asks for anyway (it doesn't
handle requirements 4, 5 or 6).
