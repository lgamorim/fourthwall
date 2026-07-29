# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

Fourthwall is the **story creation tool** for a text-based, write-your-own-adventure RPG: a locally hosted Blazor app where creators design, visualize, and validate branching stories as a directed graph of scenes (Choice / Linear / Ending), each scene carrying narrative text and an image.

The authoritative design is [docs/design/0001-architecture-and-roadmap.md](docs/design/0001-architecture-and-roadmap.md). Read it before doing any design or implementation work — it defines the domain model, validation rules (§4.2), the key architectural decisions D1–D7 (§5.3), and the phased roadmap (§6). The game runtime and combat authoring are explicit non-goals of this repo's current roadmap.

## Current state

`0.2.0` — **Phase 2 (Persistence) complete** (milestones M1–M10; tags `v0.1.0`, `v0.2.0`). The solution is scaffolded per design doc §9 (`Fourthwall.slnx` and `Directory.Build.props` at the root, Domain/Application/Infrastructure/Web projects under `src/`, unit and integration tests under `test/`). The domain model, validation engine, Graph1x-backed reachability, and SQLite persistence (repository, migrations, asset store) are implemented and tested — a story saves and reopens with full fidelity. Next up: **Phase 3 — Web Shell and Form-Based Editing** (`0.3.x`), starting the Blazor UI. See the README's Status section and design doc §6 for detail.

## Commands

The standard loop is:

```
dotnet build                                   # zero warnings required (TreatWarningsAsErrors)
dotnet test                                    # run after every change
dotnet test test/Fourthwall.Domain.UnitTests   # single project
dotnet test --filter "FullyQualifiedName~TestName"   # single test
dotnet format --verify-no-changes              # style check, enforced in CI
dotnet pack                                    # runs in CI (deliberate deviation — see below)
```

Integration tests (real SQLite) live in `test/*.IntegrationTests/` projects, separate from unit tests.

## Conventions
@.claude/rules/core/coding-standards.md
@.claude/rules/core/design-principles.md
@.claude/rules/core/architecture.md
@.claude/rules/core/testing-philosophy.md
@.claude/rules/core/workflow-core.md
@.claude/rules/overlays/workflow-team.md
@.claude/rules/archetype/application.md
@.claude/rules/overlays/workflow-agent-review.md

These are copied from the shared [claude-rules](https://github.com/lgamorim/claude-rules) repository via its `tools/sync.ps1`, composed as `application-solo -Workflow team -Add workflow-agent-review`. Because that combination matches no profile, the modules are imported directly rather than through a profile manifest. Re-audit for drift from the claude-rules checkout with the **same flags**, plus `-Check` — it cannot infer how the set was composed:

```powershell
./tools/sync.ps1 -Target <path-to>\fourthwall -Profile application-solo -Workflow team -Add workflow-agent-review -Check
```

## Architecture (dependencies always point inward)

Clean architecture: Domain ← Application ← Infrastructure/Web.

- **Domain** — pure story model (`Story`, `Scene`, `Choice`, `SceneKind`) and its hard invariants. No external dependencies.
- **Application** — use cases and the abstractions the outer layers implement (`IStoryGraph`, `IStoryRepository`, `IStoryValidator`). Depends only on Domain.
- **Infrastructure** — adapters at the edges: Graph1x behind `IStoryGraph`, sqlbound (source-gen typed SQL) + Dapper behind the repository interfaces. SQLite is the chosen database: all relational persistence and SQL-file migrations target it, and no other provider is supported. Graph1x and sqlbound types never leak past this layer.
- **Web** — Blazor (locally hosted, interactive server rendering) UI. Depends on Application; Infrastructure is wired in only via DI at the composition root.

## Workflow: phases, milestones, versioning

- Work is organized in two tiers: a **phase** is a versioned slice of the roadmap (design doc §6); a **milestone (M-number)** is a PR-sized unit of work inside a phase. M-numbers are globally sequential across the project and are assigned during phase planning — the design doc names phases, not M-numbers.
- For each milestone, draft a plan first and present it to the user; execution starts only after the user approves the plan.
- All work happens on a `feature/M<number>-<desc>` branch — one branch and one squash-merged PR per milestone.
- Versioning follows semantic versioning: each phase gets its own minor version (Phase 1 → `0.1.x`, Phase 2 → `0.2.x`, …).
- When a phase completes, tag it on the default branch with an annotated tag (e.g., `git tag -a v0.1.0 -m "..."`) and push the tag to GitHub for reference.
- `PackageVersion` carries a prerelease suffix (`X.Y.0-preview.N`) during a phase's active development. Closing the phase drops the suffix to the clean `X.Y.0` in the same commit that gets tagged, so the tag always matches the package version it marks exactly. The next phase's first commit starts the new prerelease line (`X.(Y+1).0-preview.1`).
- Each phase has one matching GitHub milestone (titled `Phase N — <Name> (0.Y.x)`), not one per M-number; every M-number's PR in that phase is associated with the phase's milestone on creation, and the milestone is closed when the phase's final PR merges. The milestone's description lists each composing M-number with its own description as a bullet, so the phase-level summary and the per-milestone detail both stay visible in one place.

## Reviews follow `overlays/workflow-agent-review.md`

A separate review agent reads each PR fresh, with no implementer context, and leaves inline comments that cite the specific rule module a finding violates rather than raising bare style preferences. It never pushes, merges, or resolves its own comments. The implementer addresses feedback with follow-up commits on the same branch; disagreements go to the maintainer to adjudicate, not back-and-forth between agents. The overlay's own text says the implementer "opens the PR" — that's about role separation from the reviewer, not a license to skip confirmation: the implementer still confirms with the maintainer before opening any PR, per `overlays/workflow-team.md`, which takes precedence here.

## Deliberate deviations from the imported rules

- **`dotnet pack` runs in CI**, although `archetype/application.md` says the deliverable of an application is the running app, not a package. The pack step is load-bearing here: the phase-versioning discipline ties each annotated tag to the exact `PackageVersion` it marks, and packing continuously keeps that machinery honest. Do not remove the Pack step from CI.
- **XML docs are enforced, not relaxed.** `GenerateDocumentationFile` is on and `CS1591` is *not* suppressed (unlike the archetype's relaxation, which this repo deliberately exceeds); missing docs on public members fail the build, with suppressions scoped to non-API surfaces (Web components, tests) via `.editorconfig`.
- **This repo's `.editorconfig` is richer than the claude-rules reference** (it adds an intersection symbol group so `private static readonly` fields stay PascalCase, with notes). Keep it; do not overwrite it with the reference copy.

## Non-obvious constraints

- **Strict TDD**: failing test before any production code, `dotnet test` before declaring anything done.
- **Never open a PR or commit to `master` without being asked**; milestone plans need user approval before execution.
- **Only two first-party dependencies**: Graph1x (all graph structure/analysis — never hand-roll graph code) and sqlbound + Dapper over SQLite (no EF Core). Both are adapters in Infrastructure; their types must never leak past that layer.
- **sqlbound's SQLite provider verifies columns only** — integration tests are the compensating control for anything its compile-time verification can't catch.
- **A story on disk is a folder** (`story.db` + `assets/` images referenced by relative path); editor-only tables are namespaced `editor_*`.
- **`dotnet format` is the naming gate, not the build**: `EnforceCodeStyleInBuild` does not surface `IDE1006` naming violations as build errors in this SDK, so a green build alone does not prove naming compliance.
