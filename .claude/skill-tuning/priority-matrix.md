# Skill-tuning rollout priority matrix

Output of the post-pilot horizontal-rollout assessment (see `uloop-compile/iter-04.md` and `uloop-launch/iter-01.md` for the pilot ROI snapshot used as calibration).

## Scoring axes

Each remaining skill is scored on three axes (0–3 each, higher = higher priority for tuning):

1. **Description compliance gap** (CLAUDE.md "What → When → How"):
   - 3 = "How" entirely absent (no implementation hint, no return-shape, no "via X")
   - 1 = partially compliant (How present but terse, e.g. "Returns ...")
   - 0 = fully compliant
2. **Hidden complexity** (SKILL.md size as proxy for implicit specification surface):
   - 3 = > 100 lines
   - 2 = 70–99 lines
   - 1 = 50–69 lines
   - 0 = < 50 lines
3. **Downstream-caller risk** (likelihood another skill / agent workflow chains off of it):
   - 3 = central building block (e.g. `execute-dynamic-code`, `run-tests`, `get-logs`)
   - 2 = mid-graph (PlayMode controls, scene query)
   - 1 = leaf-ish (per-window utility)
   - 0 = standalone (demo / sample)

## Ranking (descending)

| Rank | Skill | Desc | Complex | Caller | Total | Notes |
|------|-------|------|---------|--------|-------|-------|
| 1 | `uloop-execute-dynamic-code` | 3 | 1 | 3 | **7** | Highest leverage. Used as backstop by simulate-* skills; long description with NOT-for clauses. Tuning likely surfaces non-trivial gaps |
| 2 | `simulate-mouse-demo` | 3 | 3 | 0 | **6** | Largest body (114 lines), description lacks "(1)(2)(3)" enumeration. Standalone but the body itself is the failure surface |
| 2 | `uloop-control-play-mode` | 3 | 1 | 2 | **6** | "How" missing; PlayMode start/stop has timing semantics callers must respect |
| 4 | `uloop-screenshot` | 1 | 3 | 1 | **5** | 112 lines — largest non-demo body. Past skill-tuning effort already mentioned in `uloop-compile/ledger.md` references but no per-skill artifacts exist |
| 4 | `uloop-run-tests` | 1 | 1 | 3 | **5** | Heavily used in CI loops; freeze-prevention rules in CLAUDE.md indicate non-trivial constraints on caller behavior |
| 4 | `uloop-get-logs` | 1 | 1 | 3 | **5** | Debug entry point; filtering / regex semantics are exposed |
| 4 | `uloop-find-game-objects` | 1 | 2 | 2 | **5** | Selector grammar (name/regex/path/component/tag/layer) is the main risk surface |
| 8 | `uloop-execute-menu-item` | 3 | 0 | 1 | 4 | "How" missing; menu-path discovery is implicit |
| 8 | `uloop-clear-console` | 3 | 0 | 1 | 4 | Trivial but description lacks How |
| 8 | `find-orphaned-meta` | 3 | 0 | 1 | 4 | Description currently lacks "via uloop CLI" suffix |
| 8 | `uloop-hello-world` | 3 | 1 | 0 | 4 | Sample-only; tune last (or never) |
| 8 | `uloop-get-hierarchy` | 1 | 1 | 2 | 4 | Caller-facing tree shape |
| 8 | `uloop-simulate-keyboard` | 1 | 2 | 1 | 4 | Hold-key semantics implicit |
| 14 | `uloop-simulate-mouse-ui` | 0 | 2 | 1 | 3 | Description fully compliant; complexity in body |
| 14 | `uloop-simulate-mouse-input` | 0 | 2 | 1 | 3 | Description fully compliant; complexity in body |
| 14 | `uloop-record-input` | 1 | 1 | 1 | 3 | Modest gap |
| 14 | `uloop-replay-input` | 1 | 1 | 1 | 3 | Modest gap |
| 18 | `uloop-focus-window` | 0 | 0 | 1 | 1 | Already compliant; defer indefinitely |

## Recommended next batch (top 3)

Take the next three (rank 1, 2-tied for slot 2) and run the pilot's full Iter 0 → Iter N flow on each:

1. **`uloop-execute-dynamic-code`** — pilot ROI suggests 4 iters likely; the "NOT for file I/O or script authoring" exclusion clause is exactly the kind of dual-use boundary that hold-out tests catch
2. **`simulate-mouse-demo`** — body-heavy, demo-style; expect a description rewrite + body trim, not pure additive patches
3. **`uloop-control-play-mode`** — small body, quick iteration; PlayMode timing edges may surface a `uloop-launch`-style blocking-semantics gap

## Recommended deferral

Ranks 14–18 (totals ≤ 3) ship as-is. Re-evaluate only if a real downstream caller surfaces an issue traceable to one of them. This matches mizchi's "do not pre-emptively iterate on already-compliant skills" guidance.

## Calibration from the pilot

| Pilot data point | Calibration value |
|------------------|-------------------|
| `uloop-compile`: 5 iters (Iter 0–4), 10 dispatches, 2 SKILL.md edits | A "score 4–5" skill costs ~1 working session |
| `uloop-launch`: 2 iters (Iter 0 + 1), 1 dispatch, 1 SKILL.md edit | A cross-skill carryover gap costs ~1/3 of that |
| Both surfaced 1 self-containment gap each that pre-pilot self-review missed | Self-review alone cannot substitute for closed-book subagent dispatch |

## Out of scope for this matrix

Execution of the recommended next batch. Each batch entry should be opened as its own task with its own commit boundary. The matrix is the artifact; rollout decisions are downstream.
