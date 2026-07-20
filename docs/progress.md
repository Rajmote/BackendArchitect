# Progress log

Driven by the **6-month plan** in [`../../BackendAndArchitectRoadmap.md`](../../BackendAndArchitectRoadmap.md).

## Month 1 — Databases
| Topic | Notes | Code | Status |
|---|---|---|---|
| Indexing (B-trees, clustered vs non-clustered, composite, covering) | [01-Indexing.md](../src/BackendArchitect/Month01-Databases/01-Indexing.md) | `IndexIntuition.cs` | ✅ done |
| Reading a query plan (seek vs scan, function-on-column, logical reads) | [02-QueryPlans.md](../src/BackendArchitect/Month01-Databases/02-QueryPlans.md) | `indexing-playground.sql` | ✅ done |
| Transactions & ACID, isolation levels | — | — | ⏳ next |
| Data modeling (normalization vs denormalization, keys) | — | — | ☐ |
| Cosmos DB (partition keys, RU/s, indexing policy, consistency) | — | — | ☐ |

## Later months
- Month 2 — APIs, HTTP & resilience ☐
- Month 3 — Concurrency & observability ☐
- Month 4 — Distributed systems & messaging ☐
- Month 5 — Domain-Driven Design ☐
- Month 6 — System design & architecture judgment ☐

## Log
| Date | What I learned / built | Next |
|---|---|---|
| 2026-07-20 | Scaffolded BackendArchitect solution; indexing + query-plan lessons with runnable seek-vs-scan demo + SQL playground + tests | Transactions & isolation levels |
