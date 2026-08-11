# Exercise 02 — Circuit breaker

**Topic:** §6.2 Resilience · **Difficulty:** ⭐⭐⭐ · **Estimated:** 60–90 min

> A classic interview question, and a genuine piece of engineering: a state machine, thresholds, timing
> and thread safety in one small class.

---

## Scenario

Your checkout service calls a payment provider. When the provider gets sick, you must **stop calling it**
— both to keep your own threads free and to give it room to recover.

You're building the breaker that wraps those calls.

## Requirements

Implement `PaymentCircuitBreaker` in
[`BackendArchitect.Practice/Exercise02/PaymentCircuitBreaker.cs`](BackendArchitect.Practice/Exercise02/PaymentCircuitBreaker.cs).

1. **Closed** (start state) — calls pass through; outcomes are recorded.
2. **Trips to Open** when the **failure ratio** within the last `sampleSize` calls reaches
   `failureRatio` — **and** at least `sampleSize` calls have been observed.
   *(This is harder than counting consecutive failures: a service failing 60% of the time is broken even
   though it never fails twice in a row.)*
3. **Open** — every call is rejected **instantly**, without invoking the dependency.
4. **Half-Open** — once `breakDuration` has elapsed, allow **exactly one** probe through.
   A success closes the breaker (and resets the statistics); a failure re-opens it for a **full**
   `breakDuration` again.
5. **Only one probe.** If ten threads arrive while Half-Open, **one** calls the dependency and the other
   **nine are rejected** — you must not stampede a recovering service.
6. **Thread safety** — all of the above must hold under concurrent callers.
7. Expose `State`, `CallsAttempted` (calls that reached the dependency) and `CallsRejected`.

## Acceptance criteria

- [ ] The 3 given tests pass
- [ ] You've added tests for requirements **2, 4, 5 and 6**
- [ ] Time is **injected** — no `Thread.Sleep` and no `DateTime.Now` inside the class
- [ ] Zero warnings

## Hints

- **Requirement 2:** you need a rolling window of the last N outcomes. A `Queue<bool>` of fixed size is
  the simplest thing that works.
- **Requirement 4:** "resets the statistics" matters — otherwise the old failures immediately re-trip it.
- **Requirement 5** is the subtle one. Ask yourself: *after* one thread is let through as the probe, what
  must the state be for everyone else arriving a microsecond later? You may need a flag, or a state the
  three public ones don't cover.
- **Requirement 6:** the check and the state change must be one atomic step — the same check-then-act
  lesson as Exercise 01.
- Time injection: take a `Func<DateTimeOffset> now` in the constructor, as the reference breaker does.

## What I'll review
1. **Correctness** — all seven requirements, especially 2, 4 and 5
2. **Your tests** — did you test the *interesting* transitions, or only the happy path?
3. **Design** — state representation, where the locking sits, naming
4. **Thread safety** — is the critical section right, and is it as small as it can be?

## ⚠️ Don't peek
`src/BackendArchitect/Reliability/Resilience/CircuitBreaker.cs` is the reference from the lesson — but it
is **simpler than this exercise**: it counts *consecutive* failures and does **not** enforce the
single-probe rule (5) or thread safety (6). Try it yourself first; then comparing the two is genuinely
instructive.
