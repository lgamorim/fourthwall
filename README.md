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

`0.1.0` — **Phase 1 (Foundation and Domain) complete.** Implemented and tested so far:

- the pure story **domain model** (`Story`, `Scene`, `Choice`, outcomes) with its invariants,
- the **validation engine** enforcing the structural rules of the design (single start, reachability, degree-by-kind, reachable ending, no inescapable loops),
- **reachability analysis** backed by Graph1x behind an `IStoryGraph` abstraction.

Persistence (self-contained SQLite story packages) and the Blazor editor UI — the graph canvas, inspector, and live validation panel described above — arrive in later phases. See the roadmap's [Phases and Milestones](docs/design/0001-architecture-and-roadmap.md#6-phases-and-milestones) for what lands when.

## License

[Apache-2.0](LICENSE)
