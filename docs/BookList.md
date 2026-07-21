# Book List — Backend Engineer → Software Architect

> A curated reading list covering **every topic** in the
> [roadmap](../../BackendAndArchitectRoadmap.md). Not all are free — chosen for **quality and coverage**,
> not price. Books are mapped to the roadmap's technology sections (§1–§10) so you always know *why*
> you're reading one.

## Legend
- ⭐ **Essential** — if you only read a few, read these.
- 📘 **Code-heavy** — real, runnable sample code (many with a GitHub repo you can clone/test).
- 🟣 **.NET / C#-specific** — matches your stack (.NET, C#).
- 🧠 **Conceptual** — light on code, heavy on judgment; read for *how to think*.
- 🆓 **Free (legally)** — official free eBook or free online.

---

## The essential shortlist (start here, in this order)
1. ⭐📘🟣 **SQL Performance Explained** — Markus Winand *(indexing & query plans — exactly what we're learning now; also free online at use-the-index-luke.com 🆓)*
2. ⭐🧠 **Designing Data-Intensive Applications** — Martin Kleppmann *(the systems bible; databases → distributed systems)*
3. ⭐📘🟣 **.NET Microservices: Architecture for Containerized .NET Apps** — Microsoft 🆓 *(with the runnable **eShop** reference app on GitHub)*
4. ⭐📘🟣 **Unit Testing: Principles, Practices, and Patterns** — Vladimir Khorikov *(C#; how to test well)*
5. ⭐🧠 **Fundamentals of Software Architecture** — Mark Richards & Neal Ford *(the architect mindset: trade-offs & styles)*
6. ⭐🧠 **The Pragmatic Programmer** — Hunt & Thomas *(timeless craft)*

---

## Mapped to the roadmap

### §1 Foundations
| Topic | Book | Tags |
|---|---|---|
| Algorithms & Big-O (gentle) | **Grokking Algorithms** — Aditya Bhargava | 📘 |
| Algorithms (reference) | **The Algorithm Design Manual** — Steven Skiena | 🧠 |
| How the machine works | **Computer Systems: A Programmer's Perspective** — Bryant & O'Hallaron | 🧠 |
| How the machine works (accessible) | **Code: The Hidden Language** — Charles Petzold | 🧠 |
| C# language depth | **C# in Depth** — Jon Skeet · **C# 13 and .NET 9** — Mark J. Price *(latest ed.)* | 📘🟣 |
| Clean code & craft | **Clean Code** — Robert C. Martin · **The Pragmatic Programmer** — Hunt & Thomas | 🧠 |
| Design patterns | **Head First Design Patterns** — Freeman & Robson · **Dive Into Design Patterns** — Alexander Shvets *(you did OOA&D already)* | 📘 |
| Refactoring | **Refactoring** — Martin Fowler | 📘 |
| Testing & TDD | **Unit Testing: Principles, Practices, and Patterns** — Khorikov 🟣 · **TDD by Example** — Kent Beck | ⭐📘 |
| Dependency injection | **Dependency Injection Principles, Practices, and Patterns** — van Deursen & Seemann | 📘🟣 |
| Git | **Pro Git** — Chacon & Straub | 🆓 |

### §2 Databases
| Topic | Book | Tags |
|---|---|---|
| **Indexing & query plans** *(now)* | **SQL Performance Explained** — Markus Winand | ⭐📘🆓 |
| SQL for SQL Server | **T-SQL Fundamentals** — Itzik Ben-Gan | 📘🟣 |
| SQL mistakes to avoid | **SQL Antipatterns** — Bill Karwin | 📘 |
| Storage, transactions, isolation | **Designing Data-Intensive Applications** (ch. 3, 7) — Kleppmann | ⭐🧠 |
| Data modeling | **Database Design for Mere Mortals** — Michael Hernandez | 🧠 |
| NoSQL families | **NoSQL Distilled** — Sadalage & Fowler | 🧠 |
| Cosmos DB | Microsoft Learn docs *(primary)* + **Azure Cosmos DB** guidance in *Azure for Architects* | 🟣 |

### §3 APIs & HTTP
| Topic | Book | Tags |
|---|---|---|
| REST design | **RESTful Web APIs** — Richardson & Amundsen | 🧠 |
| HTTP deep | **HTTP: The Definitive Guide** — Gourley & Totty *(older but solid)* | 🧠 |
| gRPC | **gRPC: Up and Running** — Indrasiri & Kuruppu | 📘 |

### §4 Concurrency & Async
| Topic | Book | Tags |
|---|---|---|
| async/await, Channels, parallelism | **Concurrency in C# Cookbook** — Stephen Cleary | ⭐📘🟣 |
| Memory & GC (deeper) | **Pro .NET Memory Management** — Konrad Kokosa | 📘🟣 |

### §5 Observability & Security
| Topic | Book | Tags |
|---|---|---|
| Observability / tracing | **Observability Engineering** — Majors, Fong-Jones, Miranda | 🧠 |
| App security (friendly) | **Alice and Bob Learn Application Security** — Tanya Janca | 🧠 |
| Web security (deep) | **The Web Application Hacker's Handbook** — Stuttard & Pinto | 🧠 |
| OAuth2 / OIDC | **OAuth 2 in Action** — Richer & Sanso | 📘 |

### §6 Performance & Reliability
| Topic | Book | Tags |
|---|---|---|
| Stability patterns (circuit breakers, timeouts) | **Release It!** — Michael Nygard | ⭐🧠 |
| .NET benchmarking | **Pro .NET Benchmarking** — Andrey Akinshin *(BenchmarkDotNet author)* | 📘🟣 |
| Systems performance (deep) | **Systems Performance** — Brendan Gregg | 🧠 |
| Networking | **Computer Networking: A Top-Down Approach** — Kurose & Ross | 🧠 |

### §7 Cloud & Infrastructure
| Topic | Book | Tags |
|---|---|---|
| Docker | **Docker Deep Dive** — Nigel Poulton | 📘 |
| Kubernetes | **The Kubernetes Book** — Nigel Poulton | 📘 |
| Azure architecture | **Azure for Architects** — Ritesh Modi | 🟣 |

### §8 Distributed Systems
| Topic | Book | Tags |
|---|---|---|
| Core theory | **Designing Data-Intensive Applications** — Kleppmann | ⭐🧠 |
| Accessible intro | **Understanding Distributed Systems** — Roberto Vitillo | 🧠 |
| Patterns | **Designing Distributed Systems** — Brendan Burns | 📘 |
| Messaging & integration | **Enterprise Integration Patterns** — Hohpe & Woolf | 🧠 |
| Kafka / streaming | **Kafka: The Definitive Guide** — Shapira et al | 📘 |

### §9 Architecture & Design (incl. DDD, CQRS, event sourcing)
| Topic | Book | Tags |
|---|---|---|
| Architecture fundamentals | **Fundamentals of Software Architecture** — Richards & Ford | ⭐🧠 |
| Hard trade-offs | **Software Architecture: The Hard Parts** — Ford, Richards et al | 🧠 |
| Clean architecture | **Clean Architecture** — Robert C. Martin | 🧠 |
| Enterprise patterns | **Patterns of Enterprise Application Architecture** — Fowler | 🧠 |
| Microservices (why & how) | **Building Microservices** — Sam Newman | ⭐🧠 |
| Microservice patterns *(with code)* | **Microservices Patterns** — Chris Richardson *(FTGO repo on GitHub)* | 📘 |
| Migrating to microservices | **Monolith to Microservices** — Sam Newman | 🧠 |
| .NET reference architecture | **.NET Microservices** — Microsoft *(eShop repo)* | ⭐📘🟣🆓 |
| DDD (the blue book) | **Domain-Driven Design** — Eric Evans | 🧠 |
| DDD in practice | **Implementing Domain-Driven Design** — Vaughn Vernon | 📘 |
| DDD (modern & accessible) | **Learning Domain-Driven Design** — Vlad Khononov | ⭐🧠 |

### §10 Multiplier skills (senior → world-class)
| Topic | Book | Tags |
|---|---|---|
| The architect's role & influence | **The Software Architect Elevator** — Gregor Hohpe | 🧠 |
| System design practice | **System Design Interview, Vol 1 & 2** — Alex Xu | 📘 |
| Delivery & DevOps (evidence-based) | **Accelerate** — Forsgren, Humble, Kim | 🧠 |
| DevOps culture | **The DevOps Handbook** — Kim et al · **The Phoenix Project** *(novel)* | 🧠 |
| Growing as a senior/staff engineer | **Staff Engineer** — Will Larson | 🧠 |

---

## Reading order (aligned to the 6-month plan)
| Month | Theme | Read |
|---|---|---|
| **1** | Databases | **SQL Performance Explained** + **DDIA** ch. 1–4 |
| **2** | APIs, HTTP & resilience | **Release It!** + **RESTful Web APIs** |
| **3** | Concurrency & observability | **Concurrency in C# Cookbook** + **DDIA** ch. 5–9 |
| **4** | Distributed systems & messaging | **Building Microservices** + **Enterprise Integration Patterns** |
| **5** | Domain-Driven Design | **Learning DDD** (Khononov) + **.NET Microservices** (eShop) |
| **6** | System design & architecture | **Fundamentals of Software Architecture** + **System Design Interview** |

> **Ongoing (any month):** *The Pragmatic Programmer*, *Clean Code*, *Unit Testing* (Khorikov) — craft
> books you dip into continuously, not read once.

## How to actually use this list
1. **One "read" + one "reference" at a time.** Read the month's main book; keep a code-heavy one open to try things.
2. **Type the examples.** Reading code teaches nothing; running and breaking it teaches everything — that's what your `BackendArchitect` repo is for.
3. **Teach it back.** After each topic, write the note (like ours) in your own words. If you can't explain it, you haven't learned it yet.
