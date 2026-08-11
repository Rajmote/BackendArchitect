# BackendArchitect

A hands-on study repo for the **Backend Engineer → Software Architect** roadmap
(see [`docs/BackendArchitectRoadmap.md`](docs/BackendArchitectRoadmap.md)).

Every topic gets:
- **Notes** — a markdown file with UML / flow / sequence diagrams (Mermaid), in the topic folder.
- **Runnable code** — a small C# example that makes the idea concrete, wired into `Program.cs`.
- **Tests** — xUnit tests that prove the idea and lock the behaviour.
- **SQL / scripts** — where the topic is database- or infra-flavoured.

We follow the **6-month plan** ordering (Databases → APIs → Concurrency → Distributed → DDD → System
design), but the repo is organized by the roadmap's **Technology → Main topic → Sub topic → Example**
tree, not by month.

## Layout

```
BackendArchitect/
├── src/BackendArchitect/                       # runnable examples (console app)
│   └── <Technology>/<MainTopic>/<SubTopic>/    # e.g. Databases/SQL/Indexing/
│       ├── <SubTopic>.md                       #   explanation + Mermaid diagrams
│       ├── *.cs                                #   runnable example
│       └── *.sql                               #   scripts where relevant
├── tests/BackendArchitect.Tests/               # xUnit tests, mirrored path
│   └── <Technology>/<MainTopic>/
└── docs/progress.md                            # progress log
```

Currently filled: `Databases/SQL/Indexing/` and `Databases/SQL/QueryPlans/`.

## Build / test / run

```bash
dotnet build BackendArchitect.slnx -c Release
dotnet test  BackendArchitect.slnx -c Release --no-build
dotnet run   --project src/BackendArchitect -c Release --no-build
```

## Progress

Tracked in [`docs/progress.md`](docs/progress.md). Tick the roadmap boxes as topics land.

## Books

Curated reading list covering every roadmap topic: [`docs/BookList.md`](docs/BookList.md).

## Dependencies

Every NuGet package and the reason it's there: [`docs/Dependencies.md`](docs/Dependencies.md).
