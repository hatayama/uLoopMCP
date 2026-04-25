# Iteration 1 — closed-book median dispatch (uloop-control-play-mode)

## Result

| Scenario | Outcome | Accuracy | tool_uses | duration | retries |
|----------|---------|----------|-----------|----------|---------|
| Median: "Play モードに入ってから simulate-mouse-ui を呼びたい" | ○ | 100% (5/5 critical) | 0 | 16.4s | 0 |

Subagent: `abcfe3cb7e300da93`. closed-book mode (SKILL.md inlined, all other sources forbidden).

## Newly surfaced gaps

1. **CPM2 confirmed.** Subagent flagged "cannot determine from skill alone whether `Play` blocks until PlayMode entry completes, or whether the response can return with `IsPlaying: false`". This was the exact gap predicted in Iter 0. The subagent correctly refused to fabricate sequencing semantics and instead flagged the unclear point — proof that closed-book reading would mislead any downstream caller that sequences PlayMode-dependent commands without an explicit wait.
2. **No mention that `Play` is idempotent.** Subagent could not tell whether re-issuing `--action Play` while already in PlayMode is safe or an error. (low severity — covered by existing `Notes` "Play action starts the game ... (also resumes from pause)" but ambiguous.)

Gap #1 is the substantive one. Gap #2 is acceptable as-is.

## Iter 2 plan

- Add an `### Asynchronous PlayMode entry` subsection under `## Notes` that:
  - States the response returns immediately and `IsPlaying` reflects state at response-build time
  - Tells callers to poll `--action Play` (idempotent) until `IsPlaying: true` before invoking PlayMode-dependent commands
  - Lists the affected commands: `simulate-mouse-input`, `simulate-mouse-ui`, `simulate-keyboard`, `record-input`, `replay-input`
  - Notes those PlayMode-dependent skills self-check and return a recoverable "PlayMode is not active" error
- Resolves CPM2 and indirectly resolves the `simulate-mouse-demo` Iter 1 unclear point (same family).
