# Backend Engineer → Software Architect — Roadmap & Tracker

> Tick boxes as you go. World-class = **Depth × Breadth × Judgment × Communication**.
> Backend engineering is the foundation; architecture is built on top of it.

**How to use this:** work top-to-bottom within a phase, but let the **6-month plan** (bottom) drive
your *weekly* focus. Re-read this monthly and check what moved.

---

## Track A — Backend Engineer

### Phase 1 · Foundations (never "done")
- [ ] Data structures & algorithms + Big-O (arrays, hash maps, trees, graphs, sorting, recursion)
- [ ] How the machine works: memory, CPU, stack/heap, OS processes & threads
- [ ] C# to real depth: generics, LINQ, `async/await`, spans/memory, records, nullable ref types
- [ ] Clean Code + SOLID applied without thinking
- [ ] Design patterns (✅ in progress) + when NOT to use them
- [ ] Refactoring as a habit (small, safe, test-backed steps)
- [ ] Testing: unit + integration, TDD on at least one real feature
- [ ] Git deeply (rebase, bisect, reflog) + CI/CD (✅ done for the OOAD repo)

### Phase 2 · Core backend
- [ ] SQL deep: joins, **indexing**, **transactions/ACID**, isolation levels, **read a query plan**
- [ ] Data modeling: normalization vs denormalization, choosing keys
- [ ] NoSQL families (document / key-value / wide-column) and *when to use which*
- [ ] **Cosmos DB**: partition keys, RU/s, indexing policy, consistency levels (you use it at work)
- [ ] API design: REST maturity, **gRPC**, GraphQL basics; versioning & contracts
- [ ] HTTP deeply: methods, status codes, caching headers, content negotiation
- [ ] Concurrency & async: race conditions, locks, immutability, `Task`/`Channel`/`IAsyncEnumerable`
- [ ] Message queues & background jobs; **idempotency**
- [ ] AuthN/Z: OAuth2 / OIDC, JWT, sessions, RBAC (you touch Keycloak at work)
- [ ] Observability: structured logging, metrics, **distributed tracing** (OpenTelemetry)
- [ ] Security: OWASP Top 10, input validation, secrets, encryption basics

### Phase 3 · Performance & reliability
- [ ] Profiling & benchmarking (measure before optimizing) — BenchmarkDotNet
- [ ] Networking: TCP/IP, TLS, DNS, load balancing
- [ ] Resilience: retries, timeouts, **circuit breakers**, backpressure, graceful degradation
- [ ] Containers: Docker; Kubernetes basics
- [ ] One cloud deeply: **Azure** (App/Container Apps, Event Hub, Cosmos, Key Vault, monitoring)

---

## Track B — Software Architect (build on A)

### Phase 4 · Design at scale
- [ ] Distributed systems: **CAP**, consistency models, replication, partitioning, consensus
- [ ] The 8 fallacies of distributed computing
- [ ] System design practice ("design X") — do this weekly
- [ ] Architecture styles: monolith → **modular monolith** → microservices, event-driven, serverless
- [ ] **CQRS** and **event sourcing** (trade-offs, not cargo-cult)
- [ ] **Domain-Driven Design**: bounded contexts, aggregates, ubiquitous language
- [ ] Messaging & streaming: Kafka / **Azure Event Hub** (you already work with the ACL → Event Hub flow)

### Phase 5 · Architectural judgment
- [ ] Quality attributes & trade-offs (scalability, availability, security, cost, maintainability)
- [ ] Documenting architecture: **C4 model**, **ADRs** (✅ used at work), UML/sequence (✅ you read these now)
- [ ] Anti-patterns & evolutionary architecture / fitness functions
- [ ] **Conway's Law** / Team Topologies

### Phase 6 · Multiplier skills (senior → world-class)
- [ ] Writing: design docs, ADRs, crisp diagrams — **influence without authority**
- [ ] Business/product thinking: speak in ROI, risk, and trade-offs
- [ ] Mentoring, technical strategy, estimation, risk management

---

## The canon (read + take notes)
**Craft**
- [ ] The Pragmatic Programmer
- [ ] A Philosophy of Software Design — Ousterhout
- [ ] Refactoring — Fowler
**Patterns / Enterprise**
- [ ] Head First Design Patterns (+ GoF as reference)
- [ ] Patterns of Enterprise Application Architecture — Fowler
- [ ] Enterprise Integration Patterns — Hohpe
**Systems (essential)**
- [ ] ⭐ Designing Data-Intensive Applications — Kleppmann
- [ ] System Design Interview (Vol 1 & 2) — Alex Xu
- [ ] Release It! — Nygard
**Architecture**
- [ ] Fundamentals of Software Architecture — Ford & Richards
- [ ] Software Architecture: The Hard Parts — Ford & Richards
- [ ] The Software Architect Elevator — Hohpe
- [ ] Building Microservices — Newman
**DDD**
- [ ] Learning Domain-Driven Design — Khononov
- [ ] Implementing DDD — Vernon
**Delivery/culture**
- [ ] Accelerate · The DevOps Handbook

---

## Prioritized 6-month plan (tailored to .NET / Azure)

Assume ~6–8 focused hours/week. Each month: one theme, a **book**, a **build**, and an **at-work** application.

### Month 1 — Databases (the highest-leverage backend skill)
- [ ] SQL: indexing, transactions/isolation, **read query plans**; data modeling
- [ ] Cosmos DB: partition-key design, RU/s, indexing policy, consistency levels
- [ ] **Build:** take a slow query / a Cosmos container and make it fast; document why
- [ ] **At work:** study how your production system models data in Cosmos (ACL vs Output DB)
- [ ] **Read:** DDIA ch. 1–4

### Month 2 — APIs, HTTP & resilience
- [ ] HTTP deep + REST/gRPC design + versioning/contracts
- [ ] Resilience: retries, timeouts, circuit breakers, **idempotency**
- [ ] **Build:** an API with a resilient client (Polly) + idempotent write
- [ ] **At work:** trace the ACL → Event Hub → Output flow end-to-end; write an **ADR** on one decision
- [ ] **Read:** Release It! (stability patterns)

### Month 3 — Concurrency & observability
- [ ] async/await internals, `Channel`, parallelism, race conditions
- [ ] OpenTelemetry: tracing + metrics + structured logs
- [ ] **Build:** a concurrent pipeline (producer/consumer) with full telemetry
- [ ] **At work:** add/inspect a distributed trace across two of your services
- [ ] **Read:** DDIA ch. 5–9 (replication, partitioning, transactions)

### Month 4 — Distributed systems & messaging
- [ ] CAP, consistency, the fallacies; event-driven basics
- [ ] Event Hub / Kafka; **CQRS** and **event sourcing** (trade-offs)
- [ ] **Build:** a small **event-sourced** service (append events, rebuild state)
- [ ] **At work:** map your system's event contracts (protobuf) and consumers
- [ ] **Read:** Building Microservices

### Month 5 — Domain-Driven Design
- [ ] Bounded contexts, aggregates, ubiquitous language, context mapping
- [ ] **Build:** model one non-trivial domain properly (aggregates + invariants)
- [ ] **At work:** identify a bounded context in your system and diagram it (C4 container level)
- [ ] **Read:** Learning DDD — Khononov

### Month 6 — System design & architecture judgment (capstone)
- [ ] Quality attributes & trade-offs; C4 diagrams; ADRs; anti-patterns
- [ ] **System design practice** weekly (URL shortener → rate limiter → news feed → payment system)
- [ ] **Capstone build:** design *and partially build* a scalable service; write the ADRs + C4 diagrams
- [ ] **Read:** Fundamentals of Software Architecture + System Design Interview (practice sets)

---

## Ongoing habits (every week / month)
- [ ] **Write & teach** — a blog post / ADR / doc per topic (teaching = mastery; you're already doing this with your study repos)
- [ ] **System design** — one problem per week
- [ ] **DSA** — a little, regularly (keep the muscle)
- [ ] **Read code** — one great open-source project per month
- [ ] **Reviews** — get reviewed, review others
- [ ] **Post-mortems** — write one whenever something breaks

---

## Progress log
| Date | What I learned / built | Next |
|---|---|---|
|  |  |  |

---

> **North star:** go deep on fundamentals, build and *break* real systems at scale, and relentlessly
> write/teach what you learn. **Judgment — the architect's real skill — is the residue of doing all three
> over years.**
