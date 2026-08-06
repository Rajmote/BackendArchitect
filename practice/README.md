# Practice — coding exercises

The third stage of each topic: **Theory → Quiz → Coding.**

Theory proves you can *follow* it. The quiz proves you can *recall* it. **Writing the code proves you
can *produce* it** — which is the only one that survives six months.

## How a session works

1. **You get a brief** — `ExerciseNN-Topic.md` in this folder: the scenario, the requirements, and
   acceptance criteria.
2. **You get 2–3 starter tests** in `BackendArchitect.Practice.Tests/` showing the expected shape.
   **You write the rest of the tests yourself** — deciding what's worth testing is part of the exercise.
3. **You implement** in `BackendArchitect.Practice/`, replacing the stubs.
4. **I review**: correctness first, then design, then idiomatic C#. When I find a problem I **explain it
   and give you a hint first** — you get one attempt to fix it. Ask and I'll apply the fix.

## Rules of the game

- Stubs **compile** but throw `NotImplementedException`, so a pending exercise is a **test failure, not a
  build failure**.
- This project **is part of CI**. While an exercise is in progress the suite is **red** — that's the
  point: **red is your to-do list, green means done.**
- Don't peek at `src/BackendArchitect/` for the equivalent reference implementation until you've made
  your own attempt. Struggling first is what makes the feedback stick.

## Running just the practice tests

```bash
dotnet test practice/BackendArchitect.Practice.Tests
```

Everything (reference examples + practice):
```bash
dotnet test BackendArchitect.slnx -c Release
```

## Exercises

| # | Topic | Brief | Status |
|---|---|---|---|
| 01 | §3.1 HTTP — idempotency keys | [Exercise01-IdempotencyKeys.md](Exercise01-IdempotencyKeys.md) | ⏳ not started |
