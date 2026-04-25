# Iteration 0 — static audit (uloop-control-play-mode)

## Gaps found

| ID | Gap | Severity |
|----|-----|----------|
| CPM1 | description lacked "How" suffix | high |
| CPM2 | body says "Useful for automated testing workflows" but does not say whether `Play` blocks until PlayMode is entered, or returns immediately | medium (downstream-caller risk; same family as `uloop-launch` blocking-semantics gap) |

## Iter 0 fix landed

CPM1 only. Appended to description: "Executes via `uloop control-play-mode` CLI invocation; returns the resulting IsPlaying / IsPaused state as JSON."

CPM2 deferred to Iter 1 verification. The closed-book subagent test will surface whether a downstream caller can confidently sequence subsequent `uloop` calls after `control-play-mode --action Play` without an explicit wait. If they cannot, an Iter 2 patch will add a one-line clarification.
