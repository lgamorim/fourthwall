# Fourthwall — Architecture and Roadmap

**Status:** Accepted · **Author:** Luís Amorim (with Claude) · **Date:** 2026-07-17

*Fourthwall* — the invisible boundary between a story and its audience. This tool exists so creators can build worlds that readers step through, one choice at a time.

## 1. Vision

Build the story creation tool for a text-based RPG in the tradition of write-your-own-adventure gamebooks: every choice branches, every branch has consequences, and the whole story is a directed graph of scenes. The tool is where that graph is born — a visual editor in which a creator designs scenes, wires choices between them, attaches an image to each scene, and validates that the story actually works before a player ever sees it.

Outcomes:

- A creator can design a complete branching story on an interactive canvas — scenes as nodes, choices as edges — without touching a text file or a database.
- Every story is continuously validated: one entry point, no unreachable scenes, no dead ends that aren't deliberate endings, at least one path to an ending.
- A finished story is a self-contained package (database + image assets) ready to be consumed by the future game runtime.
- The domain model leaves a clean extension point for combat mechanics (cards, dice) without carrying any of that weight today.

## 2. Landscape and Positioning

| Project | Platform | Status | Coverage |
|---|---|---|---|
| Twine | Web/Electron, JS | Active | Visual passage graph, HTML export, no typed model or validation |
| Ink / Inky | C# runtime, custom language | Active | Script-first authoring, no visual graph editing |
| Yarn Spinner | C#/Unity, custom language | Active | Dialogue trees for games, script-first |
| Articy:Draft | Windows desktop, commercial | Active | Full narrative design suite, heavyweight, proprietary format |

Where Fourthwall's tool differentiates:

1. **Graph-first, not script-first.** Ink and Yarn Spinner treat the branching structure as an emergent property of a script. Here the graph *is* the primary artifact: authored visually, stored relationally, validated structurally with real graph algorithms (reachability, cycle analysis) rather than lint rules over text.
2. **.NET-native end to end.** Domain, persistence, validation, and UI are all C#. Graph1x provides the graph engine; there is no JS narrative runtime or custom DSL to maintain.
3. **Typed relational persistence.** Stories live in SQLite with compile-time-verified SQL (sqlbound) instead of an opaque blob format. Stories are inspectable, queryable, and migratable like any other data.
4. **Built for one game, honestly.** This is not a general narrative middleware play. The scene model (choice / linear / ending, image per scene, combat extension point) matches exactly what the Fourthwall RPG needs — and nothing more.

## 3. Constraints and Non-Goals

- **.NET 10 / C# only.** One runtime, one language across every layer. The UI is Blazor, locally hosted — no Electron, no JS framework.
- **Graph1x owns the graph.** All scene interconnection — structure, traversal, reachability, cycle detection — goes through Graph1x (v1.0.1). No hand-rolled graph code.
- **sqlbound + Dapper own data access.** Typed, compile-time-verified SQL via sqlbound where queries are static; Dapper (via `SqlSession`) where they must be composed at runtime. No EF Core, no other ORM.
- **SQLite is the storage engine.** A story is a folder: one SQLite database plus an `assets/` directory of scene images referenced by relative path.
- **No other first-party repos.** Graph1x and sqlbound are the only lgamorim libraries this project consumes.
- **Non-goal: the game runtime.** Playing the story with real game rules (state, inventory, combat resolution) is a separate future project. The tool ships a read-only preview walkthrough, nothing more.
- **Non-goal: combat authoring.** Card and dice mechanics belong to the runtime's design space. Scenes carry an extension point (see D6) so encounters can attach later; this roadmap builds none of it.
- **Non-goal: collaboration and hosting.** Single creator, local machine. Blazor keeps the door open to hosted multi-user editing later, but nothing in this roadmap depends on it.
- **License:** Apache-2.0 (per repository `LICENSE`).

## 4. Domain Primer

### 4.1 The gamebook model

A story is a set of *scenes*. Each scene is a passage of narrative text with an optional image, and exactly one of three kinds:

- **Choice** — the scene ends by offering the reader two or more choices, each leading to another scene. This is the branching heart of the genre.
- **Linear** — the scene flows into exactly one follow-up scene; useful for pacing, staging, and joining branches back together.
- **Ending** — the scene terminates the story. An ending carries an outcome (death, victory, or another flavor); "death and the end of the game" is simply an ending whose outcome is death.

Every story has exactly one *start scene*. Choices are first-class: each has its own label text ("Open the door", "Draw your sword") independent of the scene it leads to, and an explicit order among its siblings.

### 4.2 The story graph

Scenes are nodes; choices (and linear follow-ups) are directed edges. The graph is a general directed graph, not a DAG — gamebooks legitimately loop (return to the crossroads, retry the riddle) — so cycles are permitted but analyzed. Structural validity is defined as:

1. Exactly one start scene exists.
2. Every scene is reachable from the start scene (no orphans).
3. Every Choice scene has ≥ 2 outgoing edges; every Linear scene exactly 1; every Ending scene 0.
4. At least one Ending is reachable from the start.
5. Every scene can still reach *some* ending — a cycle with no exit is an inescapable trap and is flagged (as a warning: a deliberate doom-loop may be intended, but never silently).
6. Every scene image reference resolves to an existing asset; every asset is referenced (orphan assets are warnings).

Rules 1–4 are errors that mark a story invalid; 5–6 produce warnings. All of it is computed with Graph1x primitives — BFS reachability from the start, reverse reachability from the ending set, degree checks per scene kind.

### 4.3 The story package

A story on disk is a folder: `story.db` (SQLite, schema-versioned via sqlbound migrations) plus `assets/` holding scene images. The database stores relative asset paths and metadata, never image bytes. The folder is the unit of distribution — copy it, zip it, hand it to the future runtime. Canvas layout (node positions) is stored in the same database, in tables the runtime is free to ignore.

## 5. Architecture

Clean architecture; dependencies point inward. Infrastructure is wired in only at the Web composition root.

```
┌─────────────────────────────────────────────────────────────┐
│  Fourthwall.Web            Blazor UI: canvas, forms,         │
│                            validation panel, preview          │
├─────────────────────────────────────────────────────────────┤
│  Fourthwall.Infrastructure Graph1x adapter · sqlbound/Dapper  │
│                            repositories · SQLite · migrations │
│                            · image asset store                │
├─────────────────────────────────────────────────────────────┤
│  Fourthwall.Application    Use cases · IStoryGraph            │
│                            IStoryRepository · IStoryValidator │
├─────────────────────────────────────────────────────────────┤
│  Fourthwall.Domain         Story · Scene · Choice · SceneKind │
│                            hard invariants · zero dependencies│
└─────────────────────────────────────────────────────────────┘
```

### 5.1 Domain

The pure story model: `Story`, `Scene`, `Choice`, `SceneKind`, ending outcomes, and the invariants that must never break regardless of storage or UI — a Choice always points at a scene, an Ending never has outgoing choices, choice labels are non-empty, a story has one start. Records where immutability fits, encapsulated mutation where the editor needs it. No package references at all: not Graph1x, not sqlbound.

### 5.2 Application

Use cases (create story, add/edit/remove scene, wire choice, attach image, validate, preview walk) expressed against abstractions the outer layers implement: `IStoryGraph` (structure and analysis queries), `IStoryRepository` (persistence), `IStoryValidator` (the §4.2 rule set), `IAssetStore` (image ingestion and resolution). Depends only on Domain. Validation results are a typed report — rule id, severity, offending scene ids — so the UI can navigate straight to problems.

### 5.3 Infrastructure — key decisions

**D1 — Blazor, locally hosted, interactive server rendering.** The tool runs as `dotnet run` on the creator's machine and opens in a browser. Blazor keeps the whole stack C#, and the web platform (SVG, pointer events) is the cheapest place to build a rich interactive graph canvas. Server interactivity means components talk to Application services directly — no API layer to design, version, or secure for a single-user local tool.

**D2 — Hand-rolled SVG canvas, minimal JS interop.** The canvas is Blazor components rendering SVG: nodes, edges, pan/zoom via a transform, drag via pointer events. A thin JS interop shim covers only what Blazor can't reach (pointer capture, wheel deltas, bounding boxes). No JS graph framework (JointJS, Cytoscape) — that would move the tool's core interaction model outside C# and fight D1. Benefit stated per the design rules: one less ecosystem, and the canvas state lives in the same C# component model as everything else.

**D3 — Graph1x behind `IStoryGraph`, general directed graph.** Infrastructure adapts Graph1x's directed graph (not the DAG variant — cycles are legal, §4.2) and its BFS/reachability/cycle algorithms to the `IStoryGraph` abstraction. Graph1x types never cross the Application boundary; the adapter translates scene ids in, analysis results out. Graph1x's DOT/Mermaid serialization is exposed as an export use case for free.

**D4 — sqlbound + Dapper over SQLite.** Static queries use `[SqlQuery]`/`[SqlExecute]` and get compile-time verification against the real schema; runtime-composed queries (search, filtered lists) go through `SqlSession`/Dapper. Schema evolution uses sqlbound's SQL-file migrations with checksums. Known limit accepted: sqlbound's SQLite provider verifies columns only (no parameter typing) — the integration test suite carries the weight the verifier can't (§7, §8).

**D5 — Images are files, the database stores paths.** Attaching an image copies it into the story's `assets/` folder (content-hashed filename to dodge collisions and staleness); the scene row stores the relative path plus display metadata. Keeps the database small and the story folder inspectable; the tool never edits images, only ingests and previews them.

**D6 — Combat extension point, nothing more.** Each scene has an optional, opaque extension slot (a discriminated tag plus a JSON payload column) reserved for future mechanics like combat encounters. The tool round-trips it untouched and validates nothing inside it. When combat authoring arrives, it extends this slot with its own schema and editors instead of reshaping the core scene tables.

**D7 — The story folder is the package format.** No separate export pipeline: the SQLite database plus `assets/` *is* what the future runtime loads. Layout tables are namespaced (`editor_*`) so the runtime can ignore them wholesale.

### 5.4 Web

Blazor components composed from three primary surfaces: the **canvas** (design), the **inspector** (scene/choice detail forms, image attach and preview), and the **validation panel** (live §4.2 report, click-to-navigate). A story-picker shell wraps them. DI at `Program.cs` is the only place Infrastructure types appear.

## 6. Phases and Milestones

A **phase** is a versioned slice of this roadmap (one minor version: Phase 1 → `0.1.x`, Phase 2 → `0.2.x`, …). A **milestone (M-number)** is a PR-sized unit of work inside a phase; M-numbers are globally sequential and assigned during each phase's planning, not here. Effort estimates are calibrated for agentic development with review (manual-solo equivalent in parentheses).

**Phase 1 — Foundation and Domain** (`0.1.x`; ~2–3 days agentic, ~2–3 weeks manual)

- Repository scaffolding: `.slnx`, `Directory.Build.props`, `.editorconfig`, `src`/`test` layout, GitHub Actions CI running build, test, format check, and `dotnet pack` from day one.
- Domain model — `Story`, `Scene`, `Choice`, `SceneKind`, ending outcomes — with invariants enforced in-type and TDD coverage of every edge case.
- Application abstractions (`IStoryGraph`, `IStoryRepository`, `IStoryValidator`, `IAssetStore`) and the core use cases against them.
- Graph1x adapter implementing `IStoryGraph`; validator implementing rules 1–5 of §4.2 over it.

*Exit: a story can be built and validated fully in memory; CI green with zero warnings; packing works.*

**Phase 2 — Persistence** (`0.2.x`; ~2–3 days agentic, ~2–3 weeks manual)

- SQLite schema and sqlbound migrations: stories, scenes, choices, extension slot, editor layout tables.
- Repository implementations via sqlbound typed queries (Dapper `SqlSession` where composition demands it).
- Story package handling: create/open a story folder, `IAssetStore` copying images into `assets/` with content-hashed names, asset-integrity validation (§4.2 rule 6).
- Integration test project exercising the real SQLite provider end to end.

*Exit: full round-trip — create, save, close, reopen a story with images — through the real database.*

**Phase 3 — Web Shell and Form-Based Editing** (`0.3.x`; ~3–4 days agentic, ~3–4 weeks manual)

- Blazor app with DI composition root; story picker (create/open/recent).
- Inspector surface: scene CRUD, choice wiring, kind/outcome editing, image attach with preview — all via forms and lists.
- Validation panel v1: run validation, list errors/warnings, navigate to the offending scene.

*Exit: a complete story can be authored end to end in the browser, before any canvas exists.*

**Phase 4 — Interactive Canvas** (`0.4.x`; ~4–6 days agentic, ~4–6 weeks manual)

- SVG canvas: render scenes as nodes (kind-styled, image thumbnail) and choices as labeled edges.
- Pan, zoom, and node drag with positions persisted to the editor layout tables; auto-layout (via Graph1x traversal ordering) for stories that predate saved positions.
- On-canvas authoring: create a scene, drag a choice edge between scenes, select-to-inspect (canvas and inspector stay in sync).
- Validation overlays: offending nodes/edges highlighted in place.

*Exit: the primary authoring loop happens on the canvas; forms become the detail view, not the workflow.*

**Phase 5 — Validation UX, Preview, and Polish** (`0.5.x`; ~2–3 days agentic, ~2–3 weeks manual)

- Live validation on every mutation with debounce; problem count always visible.
- Preview walkthrough: read the story scene by scene, clicking choices, with images — a reader's-eye check, no game rules.
- Graph exports (DOT/Mermaid via Graph1x), story statistics (scene/ending counts, longest path, branch factor).
- Hardening, docs, `README` walkthrough with screenshots; drop prerelease suffix and tag `v1.0.0` when the phase closes — the tool is feature-complete for story creation.

*Exit: design, visualize, validate — the full promise of §1 — demonstrable on a real story.*

**Post-v1.0** (ongoing)

- Combat encounter authoring on the D6 extension point; story simulation with game state; hosted multi-creator editing. Each is its own design doc.

Roll-up: roughly **2.5–4 weeks** of agentic development end to end, against an estimated **3.5–5 months** solo-manual.

## 7. Testing Strategy

- **TDD throughout:** red–green–refactor for all production code; no milestone closes with a failing test.
- **Unit (Domain/Application):** fast and deterministic — no I/O, no clock, no delays; invariants and validator rules covered per edge case (empty stories, self-loops, unreachable clusters, endings with outgoing edges).
- **Golden stories:** a small corpus of hand-built stories (valid, each-rule-violating, pathological cycles) snapshot-tested through the validator, doubling as regression anchors for the graph adapter.
- **Integration (Infrastructure):** repository and migration tests against real SQLite files — this suite is the compensating control for sqlbound's columns-only SQLite verification.
- **Component (Web):** bUnit for inspector and validation-panel behavior; canvas interaction logic factored into plain C# state classes so pan/zoom/drag math is unit-testable without a browser.

## 8. Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| sqlbound SQLite provider verifies columns only — type errors slip past compile time | High | Integration tests over real SQLite are mandatory per repository method (§7) |
| Hand-rolled SVG canvas underperforms or fights Blazor's render model on large stories | Medium | Phase 3 ships a fully usable form-based editor first; canvas state isolated in testable C# classes; virtualize rendering if node counts demand it |
| Graph1x abstraction leak — analysis needs outgrow `IStoryGraph` | Low–Med | Adapter owns all translation; extend the interface deliberately, never pass Graph1x types through |
| Scope creep toward the game runtime (state, combat, simulation) | Medium | §3 non-goals are contractual; D6 gives combat a landing zone without building it |
| sqlbound is at `1.0.0-rc.1` — breaking changes before stable | Low | Pin the RC; the API surface used (attributes, migrations, `SqlSession`) is the stable core of the library |

## 9. Repository Layout

```
fourthwall/
├─ Fourthwall.slnx
├─ Directory.Build.props            # TFM, Nullable, TreatWarningsAsErrors, style enforcement
├─ .editorconfig
├─ docs/
│  └─ design/
│     └─ 0001-architecture-and-roadmap.md
├─ src/
│  ├─ Fourthwall.Domain/            # Story, Scene, Choice, SceneKind — zero dependencies
│  ├─ Fourthwall.Application/       # use cases, IStoryGraph, IStoryRepository, IStoryValidator, IAssetStore
│  ├─ Fourthwall.Infrastructure/    # Graph1x adapter, sqlbound/Dapper repos, SQLite, migrations, asset store
│  └─ Fourthwall.Web/               # Blazor UI: canvas, inspector, validation panel, preview
└─ test/
   ├─ Fourthwall.Domain.UnitTests/
   ├─ Fourthwall.Application.UnitTests/
   ├─ Fourthwall.Infrastructure.UnitTests/
   ├─ Fourthwall.Infrastructure.IntegrationTests/   # real SQLite
   └─ Fourthwall.Web.UnitTests/                     # bUnit + canvas state classes
```

CI is GitHub Actions: restore, build (warnings as errors), `dotnet format --verify-no-changes`, unit + integration tests, and `dotnet pack` on every push, so packaging bugs surface early.

## 10. First Three Actions

1. **Plan Phase 1 milestones.** Break Phase 1 into M-numbered, PR-sized milestones (scaffolding; domain model; application abstractions + Graph1x adapter + validator), present the plan for approval per the project workflow.
2. **Scaffold the repository.** `.slnx`, `Directory.Build.props`, `.editorconfig`, empty projects per §9, CI pipeline green on an empty build — the walking skeleton every later milestone lands on.
3. **TDD the Domain model.** Failing tests first for `Story`/`Scene`/`Choice` invariants (§4.1), then the minimal model that passes; this pins the vocabulary every other layer builds on.
