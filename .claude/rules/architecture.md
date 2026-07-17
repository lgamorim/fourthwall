# Architecture

Rules governing physical project and solution structure (as opposed to logical design, which lives in `design-principles.md`):

- Repo/solution layout: source under `src/<ProjectName>/`, unit tests under `test/<ProjectName>.UnitTests/` (singular `test`), integration tests requiring a real dependency (e.g. a database provider) under `test/<ProjectName>.IntegrationTests/`, benchmark projects (BenchmarkDotNet) under `bench/<ProjectName>/`, one solution file (`.slnx`) per repo or example, at its root, referencing every project beneath it.
- Centralize shared MSBuild properties (`TargetFramework`, `Nullable`, `TreatWarningsAsErrors`, etc.) in a `Directory.Build.props` at that same root instead of repeating them per `.csproj`.
- `dotnet pack` runs in CI from day one so packaging bugs surface early.

## Layering

Clean architecture; dependencies always point inward (Domain ← Application ← Infrastructure/Web):

- **Domain** — pure story model (`Story`, `Scene`, `Choice`, `SceneKind`) and its hard invariants. No external dependencies.
- **Application** — use cases and the abstractions the outer layers implement (`IStoryGraph`, `IStoryRepository`, `IStoryValidator`). Depends only on Domain.
- **Infrastructure** — adapters at the edges: Graph1x behind `IStoryGraph`, sqlbound (source-gen typed SQL) + Dapper behind the repository interfaces. SQLite is the chosen database: all relational persistence and SQL-file migrations target it, and no other provider is supported. Graph1x and sqlbound types never leak past this layer.
- **Web** — Blazor (locally hosted, interactive server rendering) UI. Depends on Application; Infrastructure is wired in only via DI at the composition root.