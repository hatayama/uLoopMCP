# Iteration 1 — baseline

## Changes (diff from previous)
- (none — Iter 1 measures the baseline. Iter 0 applied only the description "How" suffix.)
- Pattern applied: (n/a)

## Execution results (per scenario)

| Scenario | Success/Failure | Accuracy | steps (`tool_uses`) | duration | retries | Weak phase |
|----------|-----------------|----------|---------------------|----------|---------|------------|
| A (median) | ○ | 100% (4/4) | 0 | 13.2s | 0 | — |
| B (edge: force + wait) | ○ | 100% (5/5) | 0 | 15.7s | 0 | — |
| C (edge: lock recovery) | ○ | 100% (5/5) | 0 | 19.4s | 0 | — |

Subagent IDs (for audit): A = `abdbd66d796fbe875`, B = `a791772f137be102f`, C = `aeafb4da00d1fc558`. All `general-purpose` subagent_type.

## Structured reflection (newly surfaced this time)

No `unclear points` from any subagent. `tool_uses` is 0 across all three (the SKILL.md was fully inlined into the contract — no reference descent needed). All `[critical]` items hit ○ on first attempt, no retries.

### qualitative interpretation of `tool_uses`

All three scenarios returned `tool_uses=0`. There is no skew across scenarios → the skill body is self-contained for the in-iteration scenario set. Per the upstream skill's section on `tool_uses` interpretation, this rules out the "decision-tree-index-leaning with low self-containment" failure mode for these scenarios.

## Discretionary fill-ins (newly surfaced this time)

These are not failures but they surface implicit specification — places where two different subagents could plausibly diverge:

- **A**: `WarningCount > 0` semantics not specified. Subagent assumed "report the number, do not block the user". Different subagents may treat warnings as blocking.
- **B**: `Success: false` recovery path not specified. Subagent inferred "fall back to `uloop fix`" by cross-referencing the Troubleshooting section. The connection is not explicit at the top-level workflow.
- **B**: `ErrorCount=0` / `WarningCount=0` interpreted as "compile clean" without explicit guidance. Likely safe, but not committed at the doc level.
- **C**: After `uloop fix`, should the retry use `--force-recompile` or not? Subagent inferred "no flags" because Troubleshooting writes plain `uloop compile` after `uloop fix`. Not explicit.
- **C**: Subagent attributed "Unity is busy" only to stale locks because that is the only cause documented in Troubleshooting. The skill does not acknowledge that "busy" can also mean Unity is mid-Domain-Reload, mid-import, or simply not running. **This is the precursor signal to the hold-out H gap.**

## Ledger updates

- (none added in Iter 1 — `unclear points` count is 0; per mizchi's `Issue / Cause / General Fix Rule` framing, ledger entries come from unclear points, not discretionary fill-ins)

## Convergence-criteria status

Per the upstream skill: convergence requires 2 consecutive iterations with `new unclear points = 0` AND metric variation within ±10/15% AND hold-out scenario doesn't drop ≥15pt.

- This is iter 1: 0 unclear points → 1 of 2 consecutive clears
- Need iter 2 to confirm stability with **fresh** subagents (no fix applied) before triggering hold-out H

## Next iteration plan

**Iter 2 = stability re-dispatch (no SKILL.md change)**: re-run scenarios A/B/C with three fresh `general-purpose` subagents. Same prompts, same checklist. The purpose is to verify that the 0-unclear-points result is reproducible across subagent rolls (not a coincidence of one favorable sample), which is the upstream skill's stated reason for the "2 consecutive" rule.

If Iter 2 also returns 0 unclear points and metric variation within thresholds → run hold-out H (this is where the body's Unity-not-running gap is expected to surface, per Iter 0 carry-over G3).

If Iter 2 surfaces NEW unclear points → this is an iteration-rate signal: the discretionary fill-ins above were actually unstable, and we need to address the most reproducible one before the next dispatch.
