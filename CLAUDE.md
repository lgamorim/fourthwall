# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

Fourthwall is the **story creation tool** for a text-based, write-your-own-adventure RPG: a locally hosted Blazor app where creators design, visualize, and validate branching stories as a directed graph of scenes (Choice / Linear / Ending), each scene carrying narrative text and an image.

The authoritative design is [docs/design/0001-architecture-and-roadmap.md](docs/design/0001-architecture-and-roadmap.md). Read it before doing any design or implementation work — it defines the domain model, validation rules (§4.2), the key architectural decisions D1–D7 (§5.3), and the phased roadmap (§6). The game runtime and combat authoring are explicit non-goals of this repo's current roadmap.

## Current state

Pre-`0.1.0`: the design doc exists; **no solution or code has been scaffolded yet**. Phase 1 (foundation and domain) starts with repository scaffolding — `.slnx` at the root, `Directory.Build.props`, `.editorconfig`, projects per the layout in design doc §9.

## Commands

Once the solution is scaffolded, the standard loop is:

```
dotnet build                                   # zero warnings required (TreatWarningsAsErrors)
dotnet test                                    # run after every change
dotnet test test/Fourthwall.Domain.UnitTests   # single project
dotnet test --filter "FullyQualifiedName~TestName"   # single test
dotnet format --verify-no-changes              # style check, enforced in CI
dotnet pack                                    # runs in CI from day one
```

Integration tests (real SQLite) live in `test/*.IntegrationTests/` projects, separate from unit tests.

## Non-obvious constraints

Binding rules live in `.claude/rules/` (architecture, coding standards, design principles, testing, workflow) and are loaded automatically — follow them. Points that most shape day-to-day work here:

- **Strict TDD**: failing test before any production code, `dotnet test` before declaring anything done.
- **Workflow**: all work happens on `feature/M<number>-<desc>` branches; milestone plans need user approval before execution; never open a PR or commit to `master` without being asked.
- **Only two first-party dependencies**: Graph1x (all graph structure/analysis — never hand-roll graph code) and sqlbound + Dapper over SQLite (no EF Core). Both are adapters in Infrastructure; their types must never leak past that layer.
- **sqlbound's SQLite provider verifies columns only** — integration tests are the compensating control for anything its compile-time verification can't catch.
- **A story on disk is a folder** (`story.db` + `assets/` images referenced by relative path); editor-only tables are namespaced `editor_*`.
