# Failure-pattern ledger — simulate-mouse-demo

Per-target ledger. No skill-local patches landed; all substantive gaps were resolved at the source skill (`uloop-control-play-mode`).

| Pattern | First seen | Iter | Notes |
|---------|-----------|------|-------|
| "Wait a moment after Play" lacks concrete protocol | Iter 1 #1 | Iter 1 | Resolved indirectly by `uloop-control-play-mode` Iter 2 patch (`### Asynchronous PlayMode entry`). No skill-local patch needed. |
| Coordinate-discovery fallback unspecified | Iter 1 #2 | Iter 1 | Deferred (80-point ship). Low-severity edge case; downstream callers handle locally. |

## Cross-skill carryover (informational only)

This skill demonstrates the **cross-skill resolution pattern**: when a caller skill flags an unclear point that originates in a callee skill, the patch belongs at the callee, not the caller. Recorded here for future tuning iterations to follow the same pattern instead of duplicating wait-protocol guidance in every PlayMode-aware skill.
