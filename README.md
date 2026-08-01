# fourthwall

A text-based RPG in the tradition of write-your-own-adventure gamebooks: every choice branches, every branch has consequences, and the whole story is a directed graph of scenes — rooms and doors, decisions and outcomes, one path leading to another.

This repository currently hosts the **story creation tool**: a visual editor where creators design, visualize, and validate branching stories before a player ever sees them.

## What the tool does

- **Design** — author scenes (narrative text plus an image each) and wire choices between them on an interactive graph canvas. Scenes are Choice (2+ paths out), Linear (one follow-up), or Ending (death, victory, or otherwise — the story stops here).
- **Visualize** — the story *is* a graph: pan, zoom, drag nodes, see every branch and every loop at a glance.
- **Validate** — structural checks run live: exactly one start scene, no unreachable scenes, no accidental dead ends, at least one reachable ending, inescapable loops flagged.

A finished story is a self-contained folder — a SQLite database plus its image assets — ready for the future game runtime (with its card- and dice-based combat) to load.

## Tech stack

- **.NET 10 / C#** end to end; the UI is **Blazor**, locally hosted.
- **[Graph1x](https://github.com/lgamorim/graph1x)** powers everything graph: structure, traversal, reachability, cycle analysis.
- **[sqlbound](https://github.com/lgamorim/sqlbound)** + **Dapper** over **SQLite** for compile-time-verified, typed data access and SQL-file migrations.
- Clean architecture: Domain ← Application ← Infrastructure/Web, dependencies always pointing inward.

## Documentation

- [Architecture and Roadmap](docs/design/0001-architecture-and-roadmap.md) — vision, domain model, key decisions, and the phased plan (`0.1.x` → `1.0.0`).

## Status

`0.3.0` — **Phase 3 (Web Shell and Form-Based Editing) complete.** A complete story can now be authored end to end in the browser, before any canvas exists. Run `dotnet run --project src/Fourthwall.Web` and you can:

- **create or open a story folder**, and return to it from a list of recent ones,
- **write its scenes** — narrative text, kind (Choice / Linear / Ending), and an ending's outcome,
- **connect them** — wire choices with their own labels and targets, reorder them, and give linear scenes a follow-up,
- **attach an image** to a scene and see it previewed,
- **validate the story** and click straight through to whichever scene a rule complains about.

Underneath, from the earlier phases: the pure story **domain model** with its invariants, the **validation engine** covering the design's structural rules plus asset integrity, **reachability analysis** backed by Graph1x behind an `IStoryGraph` abstraction, and **persistence** — a self-contained story folder of a SQLite `story.db` plus content-hashed images, saving and reopening with full fidelity.

Next is the **interactive canvas** (`0.4.x`): scenes as draggable nodes and choices as labelled edges, with the forms becoming the detail view rather than the workflow. See the roadmap's [Phases and Milestones](docs/design/0001-architecture-and-roadmap.md#6-phases-and-milestones) for what lands when.

## License

[Apache-2.0](LICENSE)
