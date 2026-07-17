# Workflow

- Work is organized in two tiers: a **phase** is a versioned slice of the roadmap (design doc §6); a **milestone (M-number)** is a PR-sized unit of work inside a phase. M-numbers are globally sequential across the project and are assigned during phase planning — the design doc names phases, not M-numbers.
- For each milestone, draft a plan first and present it to the user; execution starts only after the user approves the plan.
- Never open a pull request automatically — always confirm with the user first.
- Make minimal, focused changes; do not refactor unrelated code in the same change.
- One logical change per commit, with an imperative-mood message explaining *why*.
- When unsure between two designs, present both with trade-offs and ask before implementing.
- Never commit or push directly to the default branch (`master`/`main`). Milestones are used to track the progress of the project. All work happens on a `feature/M<number>-<desc>`-prefixed branch.
- The branch lands on the default branch only through a reviewed pull request. Enforce this with branch protection (require a PR, disallow direct pushes, require the squash-merge strategy) so it can't be bypassed by accident.
- Merge by squash so each feature arrives as a single logical commit on the default branch, consistent with the "one logical change per commit" rule above; the squash commit message keeps the imperative, *why*-focused form.
- Versioning follows semantic versioning: each phase gets its own minor version (Phase 1 → `0.1.x`, Phase 2 → `0.2.x`, …;).
- When a phase completes, tag it on the default branch with an annotated tag (e.g., `git tag -a v0.1.0 -m "..."`) and push the tag to GitHub for reference.
- `PackageVersion` carries a prerelease suffix (`X.Y.0-preview.N`) during a phase's active development. Closing the phase drops the suffix to the clean `X.Y.0` in the same commit that gets tagged, so the tag always matches the package version it marks exactly. The next phase's first commit starts the new prerelease line (`X.(Y+1).0-preview.1`).
- Each phase has one matching GitHub milestone (titled `Phase N — <Name> (0.Y.x)`), not one per M-number; every M-number's PR in that phase is associated with the phase's milestone on creation, and the milestone is closed when the phase's final PR merges. The milestone's description lists each composing M-number with its own description as a bullet, so the phase-level summary and the per-milestone detail both stay visible in one place.
- Update this file when a new convention or correction is established.
