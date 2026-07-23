# Progress log

Structure and priorities live in [`../../BackendAndArchitectRoadmap.md`](../../BackendAndArchitectRoadmap.md)
(Technology → Main topic → Sub topic → Example). The 6-month plan drives weekly focus.

## Current focus: Technology 2 — Databases

| Sub topic | Notes | Code / script | Status |
|---|---|---|---|
| 2.1.1 Indexing | [Indexing.md](../src/BackendArchitect/Databases/SQL/Indexing/Indexing.md) | `IndexIntuition.cs`, `IndexingDemo.cs` | ✅ |
| 2.1.2 Query plans | [QueryPlans.md](../src/BackendArchitect/Databases/SQL/QueryPlans/QueryPlans.md) | `indexing-playground.sql` | ✅ |
| 2.1.3 Transactions & ACID | [Transactions.md](../src/BackendArchitect/Databases/SQL/Transactions/Transactions.md) | `Bank.cs`, `TransactionsDemo.cs` | ✅ |
| 2.1.4 Isolation levels | [IsolationLevels.md](../src/BackendArchitect/Databases/SQL/IsolationLevels/IsolationLevels.md) | `TicketBooth.cs`, `IsolationLevelsDemo.cs` | ✅ |
| 2.1.5 Data modeling | — | — | ⏳ next |
| 2.3 Cosmos DB | — | — | ☐ |

## Technologies (see roadmap tree for sub topics)
- 1. Foundations ☐ · **2. Databases ⏳** · 3. APIs & HTTP ☐ · 4. Concurrency & Async ☐
- 5. Observability & Security ☐ · 6. Performance & Reliability ☐ · 7. Cloud & Infra ☐
- 8. Distributed Systems ☐ · 9. Architecture & Design ☐ · 10. Multiplier Skills ☐

## Log
| Date | What I learned / built | Next |
|---|---|---|
| 2026-07-20 | Scaffolded BackendArchitect; reorganized into Technology→MainTopic→SubTopic tree; indexing + query-plan examples (runnable seek-vs-scan demo, SQL lab, 4 tests) | Transactions & isolation levels |
| 2026-07-22 | Transactions & ACID done (Bank model demonstrating all 4 ACID letters: atomicity/consistency/isolation-lock/durability-snapshot; AcidDemo + 10 tests). Removed all "SureCore" references from repo & roadmap. Added docs/BookList.md. Started Isolation Levels (Who/What/3 anomalies taught; note is WIP) | Finish Isolation Levels: the 4 levels, When/Where/How, runnable demo + tests |
