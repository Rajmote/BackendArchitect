# Backend Engineer → Software Architect — Structured Roadmap & Tracker

> World-class = **Depth × Breadth × Judgment × Communication**.
> This file is the **map**. The **territory** (notes + runnable code) lives in this repo under
> [`src/`](../src/BackendArchitect) and [`docs/`](../docs), organized as
> **Technology → Main topic → Sub topic → Example** — one `.md` per example, next to its code.

## How to use this
- The **tree below** is the reference structure — the complete set of things to learn, grouped by technology.
- The **[6-month plan](#6-month-plan-tailored-to-net--azure)** at the bottom drives your *weekly* focus (databases-first).
- **Live progress + per-example links:** [`progress.md`](progress.md). **Books:** [`BookList.md`](BookList.md).
- We **fill examples as we reach them**: a sub topic gets its `.md` + code when we study it, not before.
- Legend: ✅ done · ⏳ in progress / next · ☐ not started.

## Repo conventions (per example)
Every studied sub topic produces, in `src/BackendArchitect/<Technology>/<MainTopic>/<SubTopic>/`:
- **`<SubTopic>.md`** — explanation with Mermaid UML + sequence + flow diagrams.
- **Runnable C#** — a small example wired into `Program.cs`.
- **xUnit tests** in the mirrored path under `tests/`.
- **Scripts** (`.sql`, etc.) where the topic calls for it.

Build / test / run from the repo root:
```bash
dotnet build BackendArchitect.slnx -c Release
dotnet test  BackendArchitect.slnx -c Release --no-build
dotnet run   --project src/BackendArchitect -c Release --no-build
```

---

# Technology tree

## 1. Foundations *(never "done" — keep sharp)*
- **1.1 CS fundamentals**
  - 1.1.1 Data structures & Big-O (arrays, hash maps, trees, graphs) ☐
  - 1.1.2 Sorting, searching, recursion ☐
  - 1.1.3 Machine model: memory, CPU, stack/heap, processes & threads ☐
- **1.2 C# language depth**
  - 1.2.1 Generics ☐ · 1.2.2 LINQ ☐ · 1.2.3 async/await ☐
  - 1.2.4 Span/Memory ☐ · 1.2.5 Records ☐ · 1.2.6 Nullable reference types ☐
- **1.3 Craft**
  - 1.3.1 Clean Code + SOLID ☐ · 1.3.2 Design patterns (✅ studied in DesignPatterns repo) · 1.3.3 When NOT to use a pattern ☐
  - 1.3.4 Refactoring in small safe steps ☐
- **1.4 Testing & delivery**
  - 1.4.1 Unit + integration tests ☐ · 1.4.2 TDD on a real feature ☐
  - 1.4.3 Git deep (rebase, bisect, reflog) ☐ · 1.4.4 CI/CD (✅ CI wired for this repo)

## 2. Databases 📁 [`src/…/Databases`](../src/BackendArchitect/Databases)
- **2.1 SQL** — ✅ **complete**
  - 2.1.1 **Indexing** — ✅ [notes](../src/BackendArchitect/Databases/SQL/Indexing/Indexing.md) · code `IndexIntuition.cs`
    - Examples: B-tree seek vs scan ✅ · clustered vs non-clustered ✅ · composite + covering / leftmost-prefix ✅
  - 2.1.2 **Reading a query plan** — ✅ [notes](../src/BackendArchitect/Databases/SQL/QueryPlans/QueryPlans.md) · [sql lab](../src/BackendArchitect/Databases/SQL/Indexing/indexing-playground.sql)
    - Examples: seek vs scan in the plan ✅ · function-on-column kills the seek ✅ · logical reads vs time ✅
  - 2.1.3 **Transactions & ACID** — ✅ [notes](../src/BackendArchitect/Databases/SQL/Transactions/Transactions.md) · code `Bank.cs`, `AcidDemo.cs`
    - Examples: atomicity/rollback ✅ · consistency ✅ · isolation (lock) ✅ · durability (snapshot) ✅
  - 2.1.4 **Isolation levels** — ✅ [notes](../src/BackendArchitect/Databases/SQL/IsolationLevels/IsolationLevels.md) · code `TicketBooth.cs`
    - Examples: dirty read ✅ · non-repeatable read ✅ · phantom ✅ · the 4 levels + oversell demo ✅ · snapshot/MVCC (noted) ✅
  - 2.1.5 **Data modeling** — ✅ [notes](../src/BackendArchitect/Databases/SQL/DataModeling/DataModeling.md) · code `FlatOrder.cs`, `Normalized.cs`
    - Examples: normalization/3NF ✅ · anomalies flat vs normalized ✅ · denormalization trade-off ✅ · keys (PK/FK) ✅
  - 2.1.6 **Joins & set operations** — ☐
- **2.2 NoSQL concepts** — ✅ [notes](../src/BackendArchitect/Databases/NoSQL/Concepts/NoSqlConcepts.md) · code `RelationalStore.cs`, `DocumentStore.cs`
  - 2.2.1 Document ✅ · 2.2.2 Key-value ✅ · 2.2.3 Wide-column ✅ · 2.2.4 When to use which ✅
  - Examples: normalized-vs-document read/write trade ✅ · denormalization & the update anomaly ✅ · schema-on-read ✅
- **2.3 Cosmos DB** *(used at work)* — ⏳ in progress
  - 2.3.0 **Fundamentals** — ✅ [notes](../src/BackendArchitect/Databases/Cosmos/Fundamentals/Fundamentals.md)
    - Examples: Core-vs-compatibility APIs ✅ · Account→Database→Container→Item ✅ · partition key + id = the address ✅ · schema-on-read / no cross-container joins ✅
  - 2.3.1 **Partition-key design & `id`** — ✅ [notes](../src/BackendArchitect/Databases/Cosmos/PartitionKeys/PartitionKeys.md) · code `PartitionedContainer.cs`
    - Examples: logical vs physical partitions ✅ · even spread vs hot partition (measured) ✅ · point read vs single- vs cross-partition cost ✅ · synthetic & hierarchical keys ✅
  - 2.3.2 **Request Units (RU/s)** — ✅ [notes](../src/BackendArchitect/Databases/Cosmos/RequestUnits/RequestUnits.md) · code `RuCost.cs`, `ThroughputBudget.cs`
    - Examples: 1 RU anchor & cost ratios ✅ · writes ~5x reads (index maintenance) ✅ · queries priced by work not results ✅ · 429 throttling + SDK retry ✅ · provisioned/autoscale/serverless ✅
  - 2.3.3 **Indexing policy** — ✅ [notes](../src/BackendArchitect/Databases/Cosmos/IndexingPolicy/IndexingPolicy.md) · code `IndexPolicy.cs`
    - Examples: inverted default (`/*` indexes everything) ✅ · over- vs under-indexing U-shape, measured ✅ · included/excluded paths & wildcards ✅ · composite indexes ✅ · mutable (unlike the partition key) ✅
  - 2.3.4 **Consistency levels** — ✅ [notes](../src/BackendArchitect/Databases/Cosmos/ConsistencyLevels/ConsistencyLevels.md) · code `ReplicatedStore.cs`
    - Examples: the 5 levels compared on one lagging replica ✅ · session token / read-your-own-writes ✅ · 2x read cost for Strong & Bounded ✅

## 3. APIs & HTTP 📁 [`src/…/Apis`](../src/BackendArchitect/Apis)
- **3.1 HTTP fundamentals** — ✅ [notes](../src/BackendArchitect/Apis/Http/Fundamentals/HttpFundamentals.md) · code `HttpSemantics.cs`, `IdempotentPaymentApi.cs`
  - Examples: safe vs idempotent ✅ · method choice (POST/PUT/PATCH/DELETE) ✅ · why never hide a delete behind GET ✅ · status codes are machine-readable ✅ · retry = transient status AND idempotent op ✅ · idempotency keys ✅
  - Still to cover: caching headers ☐ · content negotiation ☐
- **3.2 REST design & versioning** — ✅ [notes](../src/BackendArchitect/Apis/Rest/Design/RestDesign.md) · code `ResourceUrl.cs`, `ApiChange.cs`, `VersionedOrderApi.cs`
  - Examples: RPC→REST URL redesign ✅ · Richardson maturity (target L2) ✅ · 201+Location ✅ · breaking vs additive change classifier ✅ · expand→migrate→contract ✅ · 4 versioning strategies + N-1/Sunset ✅
- **3.3 gRPC** — protobuf ☐ · streaming ☐
- **3.4 GraphQL** — basics & trade-offs ☐

## 4. Concurrency & Async 📁 `.../Concurrency`
- **4.1 async/await internals** ☐ · **4.2 Task / Channel / IAsyncEnumerable** ☐
- **4.3 Race conditions & locks** ☐ · **4.4 Immutability** ☐ · **4.5 Producer/consumer pipelines** ☐

## 5. Observability & Security 📁 `.../Observability` · `.../Security`
- **5.1 Observability** — structured logging ☐ · metrics ☐ · distributed tracing (OpenTelemetry) ☐
- **5.2 AuthN/Z** — OAuth2/OIDC ☐ · JWT ☐ · sessions ☐ · RBAC (Keycloak at work) ☐
- **5.3 Security** — OWASP Top 10 ☐ · input validation ☐ · secrets ☐ · encryption basics ☐

## 6. Performance & Reliability 📁 `.../Reliability`
- **6.1 Profiling & benchmarking** — BenchmarkDotNet, measure first ☐
- **6.2 Resilience** — retries ☐ · timeouts ☐ · circuit breakers ☐ · backpressure ☐ · idempotency ☐
- **6.3 Networking** — TCP/IP ☐ · TLS ☐ · DNS ☐ · load balancing ☐

## 7. Cloud & Infrastructure 📁 `.../Cloud`
- **7.1 Containers** — Docker ☐ · Kubernetes basics ☐
- **7.2 Azure deeply** — App/Container Apps ☐ · Event Hub ☐ · Cosmos ☐ · Key Vault ☐ · monitoring ☐

## 8. Distributed Systems 📁 `.../Distributed`
- **8.1 Theory** — CAP ☐ · consistency models ☐ · replication ☐ · partitioning ☐ · consensus ☐ · the 8 fallacies ☐
- **8.2 Messaging & streaming** — Kafka / Azure Event Hub ☐
- **8.3 CQRS** ☐ · **8.4 Event sourcing** ☐ *(trade-offs, not cargo-cult)*

## 9. Architecture & Design 📁 `.../Architecture`
- **9.1 Styles** — monolith → modular monolith → microservices → event-driven → serverless ☐
- **9.2 Domain-Driven Design** — bounded contexts ☐ · aggregates ☐ · ubiquitous language ☐ · context mapping ☐
- **9.3 Quality attributes & trade-offs** — scalability/availability/security/cost/maintainability ☐
- **9.4 Documenting** — C4 model ☐ · ADRs (✅ used at work) · UML/sequence (✅)
- **9.5 Evolution** — anti-patterns ☐ · evolutionary architecture / fitness functions ☐ · Conway's Law / Team Topologies ☐

## 10. Multiplier Skills *(senior → world-class)*
- **10.1 Writing** — design docs, ADRs, crisp diagrams (influence without authority) ☐
- **10.2 Business/product thinking** — ROI, risk, trade-offs ☐
- **10.3 Leadership** — mentoring ☐ · technical strategy ☐ · estimation ☐ · risk management ☐

---

# 6-month plan *(tailored to .NET / Azure)*

~6–8 focused hours/week. Each month: one theme, a **book**, a **build**, an **at-work** application.
Maps onto the tree above.

### Month 1 — Databases (§2) — ✅ **COMPLETE**
- [x] SQL indexing (§2.1.1) · [x] reading query plans (§2.1.2)
- [x] transactions/isolation (§2.1.3–4) · [x] data modeling (§2.1.5)
- [x] NoSQL concepts (§2.2) · [x] Cosmos DB (§2.3 — fundamentals, partition keys, RU/s, indexing policy, consistency levels)
- **Build:** make a slow query / Cosmos container fast; document why · **Read:** DDIA ch. 1–4

### Month 2 — APIs, HTTP & resilience (§3, §6.2)
- [ ] HTTP + REST/gRPC design + versioning · [ ] retries/timeouts/circuit breakers/idempotency
- **Build:** API + resilient client (Polly) + idempotent write · **At work:** trace ACL → Event Hub → Output; write an ADR · **Read:** Release It!

### Month 3 — Concurrency & observability (§4, §5.1)
- [ ] async internals, Channel, parallelism, races · [ ] OpenTelemetry traces/metrics/logs
- **Build:** concurrent producer/consumer with full telemetry · **Read:** DDIA ch. 5–9

### Month 4 — Distributed systems & messaging (§8)
- [ ] CAP, consistency, fallacies, event-driven · [ ] Event Hub/Kafka, CQRS, event sourcing
- **Build:** small event-sourced service (append events, rebuild state) · **Read:** Building Microservices

### Month 5 — Domain-Driven Design (§9.2)
- [ ] bounded contexts, aggregates, ubiquitous language, context mapping
- **Build:** model one non-trivial domain (aggregates + invariants) · **At work:** diagram a bounded context from your work (C4) · **Read:** Learning DDD — Khononov

### Month 6 — System design & architecture judgment (§9) — *capstone*
- [ ] quality attributes & trade-offs, C4, ADRs, anti-patterns · [ ] weekly system-design practice
- **Capstone:** design + partially build a scalable service; write the ADRs + C4 · **Read:** Fundamentals of Software Architecture + System Design Interview

---

# The canon (read + take notes)
> 📚 **Full curated booklist covering every topic above:** [`BookList.md`](BookList.md) — mapped to
> §1–§10, with reading order, .NET picks, and which books ship runnable code. Quick canon below.

**Craft:** The Pragmatic Programmer ☐ · A Philosophy of Software Design (Ousterhout) ☐ · Refactoring (Fowler) ☐
**Patterns/Enterprise:** Head First Design Patterns ☐ · PoEAA (Fowler) ☐ · Enterprise Integration Patterns (Hohpe) ☐
**Systems:** ⭐ Designing Data-Intensive Applications (Kleppmann) ☐ · System Design Interview Vol 1–2 (Xu) ☐ · Release It! (Nygard) ☐
**Architecture:** Fundamentals of Software Architecture ☐ · The Hard Parts ☐ · The Software Architect Elevator ☐ · Building Microservices ☐
**DDD:** Learning DDD (Khononov) ☐ · Implementing DDD (Vernon) ☐
**Delivery/culture:** Accelerate ☐ · The DevOps Handbook ☐

# Ongoing habits
- [ ] **Write & teach** — a note/ADR/doc per topic (this repo *is* this habit)
- [ ] **System design** — one problem/week · [ ] **DSA** — a little, regularly
- [ ] **Read code** — one great OSS project/month · [ ] **Reviews** — get reviewed, review others · [ ] **Post-mortems** — one whenever something breaks

---

> **North star:** go deep on fundamentals, build and *break* real systems at scale, and relentlessly
> write/teach what you learn. **Judgment — the architect's real skill — is the residue of doing all
> three over years.**
