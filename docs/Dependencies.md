# Dependencies — what we use and why

Every package here has to earn its place. This file records **why** each one was added, so nobody
(including future-you) has to guess whether a dependency is still needed.

> 🧠 A dependency is a **long-term commitment**: it's code you don't control, that can break, go
> unmaintained, or carry a CVE. Writing down the reason makes it possible to *remove* one later.

Last verified: **2026-08-08** · target framework **net10.0**

---

## Runtime dependencies

### `src/BackendArchitect`

| Package | Version | Why it's here |
|---|---|---|
| **Polly** | 8.7.0 | The .NET standard for **resilience**: retries with backoff + jitter, circuit breakers, timeouts, fallbacks, and pipeline composition. Used by §6.2 to show the real library alongside our hand-rolled `CircuitBreaker`/`RetryPolicy`, so the study repo teaches both *how it works* and *what you'd actually ship*. |

**That's the only runtime dependency in the whole repo**, and deliberately so: every other example
(indexing, transactions, isolation, partition keys, RU costing, HTTP semantics, protobuf compatibility,
GraphQL fetching) is modelled with plain BCL types. Simulating a concept in ~60 lines teaches more than
wiring up a real client, runs offline, and executes in milliseconds.

### `practice/BackendArchitect.Practice`
No packages. Exercises are solved with the BCL only — the point is to write the mechanism yourself.

---

## Test dependencies

Both `tests/BackendArchitect.Tests` and `practice/BackendArchitect.Practice.Tests`:

| Package | Version | Why it's here |
|---|---|---|
| **xunit** | 2.9.2 | The test framework — `[Fact]`, `[Theory]`, `Assert`. Chosen over NUnit/MSTest for its lightweight, convention-light style and first-class `[Theory]` data-driven tests, which we lean on a lot. |
| **xunit.runner.visualstudio** | 2.8.2 | The VSTest adapter that lets `dotnet test`, Visual Studio and Rider **discover and run** xunit tests. Without it the tests exist but nothing finds them. |
| **Microsoft.NET.Test.Sdk** | 17.11.1 | The MSBuild/VSTest plumbing that makes a project a *test* project (test host, `dotnet test` integration). Required by every .NET test project regardless of framework. |

Those three are the standard trio — they always travel together.

`<Using Include="Xunit" />` is set in both test `.csproj` files so `using Xunit;` isn't needed in every
file. Note that `ImplicitUsings` does **not** include Xunit; this had to be added explicitly.

---

## Deliberately NOT taken

Worth recording, because "why *isn't* X here?" is asked as often as "why is Y here?".

| Package | Why we don't use it |
|---|---|
| **FluentAssertions** | Nicer syntax, but v8+ changed to a paid licence for commercial use. Plain `Assert` has no such risk and keeps the repo copy-pasteable. |
| **Moq / NSubstitute** | Our examples inject plain delegates (`Func<DateTimeOffset>`, `Func<bool>`) instead of interfaces, so there's nothing to mock. A hand-written fake like `FlakyPaymentGateway` is clearer than a mock setup. |
| **Microsoft.Data.SqlClient / EF Core** | The SQL topics teach *concepts* (indexing, isolation, normalisation) via models; a real database would make the repo slow, stateful and offline-hostile. The runnable SQL lives in `indexing-playground.sql` for you to execute against your own server. |
| **Microsoft.Azure.Cosmos** | Same reasoning — the Cosmos topics model partitioning and RU cost so they run in milliseconds with no Azure account or bill. |
| **BenchmarkDotNet** | Will be added in **§6.1 Profiling & benchmarking**, where measuring *is* the subject. Not before. |
| **OpenTelemetry** | Coming in **Month 3 (§5.1 Observability)**. |

---

## Policy

**Before adding a package, ask:**
1. Can the BCL do this in a reasonable amount of code? (For teaching, hand-rolling is often *better*.)
2. Is it actively maintained, widely adopted, and licensed compatibly?
3. What does it pull in transitively?
4. **Would I be able to justify it in a code review in six months?** If not, don't add it.

**When you do add one:** record it in this file with a one-line reason, in the same commit.

## Current status (checked 2026-08-08)

**Vulnerabilities: none** across all four projects. ✅

**Updates available** (test tooling only — no runtime impact):

| Package | Current | Latest | Notes |
|---|---|---|---|
| xunit | 2.9.2 | 2.9.3 | patch — safe to take any time |
| Microsoft.NET.Test.Sdk | 17.11.1 | 18.8.1 | **major** — verify `dotnet test` still discovers everything |
| xunit.runner.visualstudio | 2.8.2 | 3.1.5 | **major** — v3 targets the xunit v3 ecosystem; pairing a v3 runner with xunit v2 needs checking |

Polly is current. Deliberately **not** upgrading the two majors yet: the whole value of this repo is that
its tests run and pass, and a test-runner migration is a chore with no learning payoff mid-topic. Revisit
between months, and take the xunit patch whenever.

## Handy commands

```bash
dotnet list BackendArchitect.slnx package                 # what's referenced
dotnet list BackendArchitect.slnx package --outdated      # what's behind
dotnet list BackendArchitect.slnx package --vulnerable    # known CVEs
dotnet list BackendArchitect.slnx package --include-transitive
```

Run the `--vulnerable` check periodically — it's the cheapest security habit there is.
