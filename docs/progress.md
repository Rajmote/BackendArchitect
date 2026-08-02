# Progress log

Structure and priorities live in [`BackendArchitectRoadmap.md`](BackendArchitectRoadmap.md)
(Technology → Main topic → Sub topic → Example). The 6-month plan drives weekly focus.

## ✅ Technology 2 — Databases: COMPLETE (Month 1 done — 44 tests green)
Next: **Month 2 — APIs, HTTP & resilience** (§3 and §6.2).

| Sub topic | Notes | Code / script | Status |
|---|---|---|---|
| 2.1.1 Indexing | [Indexing.md](../src/BackendArchitect/Databases/SQL/Indexing/Indexing.md) | `IndexIntuition.cs`, `IndexingDemo.cs` | ✅ |
| 2.1.2 Query plans | [QueryPlans.md](../src/BackendArchitect/Databases/SQL/QueryPlans/QueryPlans.md) | `indexing-playground.sql` | ✅ |
| 2.1.3 Transactions & ACID | [Transactions.md](../src/BackendArchitect/Databases/SQL/Transactions/Transactions.md) | `Bank.cs`, `TransactionsDemo.cs` | ✅ |
| 2.1.4 Isolation levels | [IsolationLevels.md](../src/BackendArchitect/Databases/SQL/IsolationLevels/IsolationLevels.md) | `TicketBooth.cs`, `IsolationLevelsDemo.cs` | ✅ |
| 2.1.5 Data modeling | [DataModeling.md](../src/BackendArchitect/Databases/SQL/DataModeling/DataModeling.md) | `FlatOrder.cs`, `Normalized.cs`, `DataModelingDemo.cs` | ✅ |
| 2.2 NoSQL concepts | [NoSqlConcepts.md](../src/BackendArchitect/Databases/NoSQL/Concepts/NoSqlConcepts.md) | `RelationalStore.cs`, `DocumentStore.cs`, `NoSqlConceptsDemo.cs` | ✅ |
| 2.3.0 Cosmos DB — fundamentals | [Fundamentals.md](../src/BackendArchitect/Databases/Cosmos/Fundamentals/Fundamentals.md) | — (conceptual) | ✅ |
| 2.3.1 Cosmos — partition keys & `id` | [PartitionKeys.md](../src/BackendArchitect/Databases/Cosmos/PartitionKeys/PartitionKeys.md) | `PartitionedContainer.cs`, `PartitionKeysDemo.cs` | ✅ |
| 2.3.2 Cosmos — RU/s | [RequestUnits.md](../src/BackendArchitect/Databases/Cosmos/RequestUnits/RequestUnits.md) | `RuCost.cs`, `ThroughputBudget.cs`, `RequestUnitsDemo.cs` | ✅ |
| 2.3.3 Cosmos — indexing policy | [IndexingPolicy.md](../src/BackendArchitect/Databases/Cosmos/IndexingPolicy/IndexingPolicy.md) | `IndexPolicy.cs`, `IndexingPolicyDemo.cs` | ✅ |
| 2.3.4 Cosmos — consistency levels | [ConsistencyLevels.md](../src/BackendArchitect/Databases/Cosmos/ConsistencyLevels/ConsistencyLevels.md) | `ReplicatedStore.cs`, `ConsistencyLevelsDemo.cs` | ✅ |

## Technologies (see roadmap tree for sub topics)
- 1. Foundations ☐ · **2. Databases ⏳** · 3. APIs & HTTP ☐ · 4. Concurrency & Async ☐
- 5. Observability & Security ☐ · 6. Performance & Reliability ☐ · 7. Cloud & Infra ☐
- 8. Distributed Systems ☐ · 9. Architecture & Design ☐ · 10. Multiplier Skills ☐

## Log
| Date | What I learned / built | Next |
|---|---|---|
| 2026-07-20 | Scaffolded BackendArchitect; reorganized into Technology→MainTopic→SubTopic tree; indexing + query-plan examples (runnable seek-vs-scan demo, SQL lab, 4 tests) | Transactions & isolation levels |
| 2026-08-02 | **Month 1 Databases COMPLETE.** Finished Cosmos: partition keys (spread + hot-partition demo), RU/s (cost model + 429 throttling), indexing policy (over/under-indexing U-shape), consistency levels (5 levels on a lagging replica). 44 tests green | **Month 2 — APIs, HTTP & resilience** (§3, §6.2) |
| 2026-07-25 | §2.2 NoSQL concepts done (4 families, no-joins→scale-out, denormalize-by-default + how to handle updates to denormalized copies; normalized-vs-document demo measuring reads 200→100 and writes 1→10, 19 tests). Roadmap renamed/moved to `docs/BackendArchitectRoadmap.md`. Started §2.3 Cosmos DB — fundamentals note done | Cosmos partition keys, then RU/s, indexing policy, consistency levels |
| 2026-07-22 | Transactions & ACID done (Bank model demonstrating all 4 ACID letters: atomicity/consistency/isolation-lock/durability-snapshot; AcidDemo + 10 tests). Removed all "SureCore" references from repo & roadmap. Added docs/BookList.md. Started Isolation Levels (Who/What/3 anomalies taught; note is WIP) | Finish Isolation Levels: the 4 levels, When/Where/How, runnable demo + tests |
