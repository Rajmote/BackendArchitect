# BackendArchitect

A hands-on study repo for the **Backend Engineer → Software Architect** roadmap
(see [`../BackendAndArchitectRoadmap.md`](../BackendAndArchitectRoadmap.md)).

Every topic gets:
- **Notes** — a markdown file with UML / flow / sequence diagrams (Mermaid), in the topic folder.
- **Runnable code** — a small C# example that makes the idea concrete, wired into `Program.cs`.
- **Tests** — xUnit tests that prove the idea and lock the behaviour.
- **SQL / scripts** — where the topic is database- or infra-flavoured.

We follow the **6-month plan** ordering: Databases → APIs → Concurrency → Distributed → DDD → System design.

## Layout

```
BackendArchitect/
├── src/BackendArchitect/            # the runnable examples (console app)
│   └── Month01-Databases/           # one folder per month/topic
├── tests/BackendArchitect.Tests/    # xUnit tests
└── docs/progress.md                 # progress log
```

## Build / test / run

```bash
dotnet build BackendArchitect.slnx -c Release
dotnet test  BackendArchitect.slnx -c Release --no-build
dotnet run   --project src/BackendArchitect -c Release --no-build
```

## Progress

Tracked in [`docs/progress.md`](docs/progress.md). Tick the roadmap boxes as topics land.
