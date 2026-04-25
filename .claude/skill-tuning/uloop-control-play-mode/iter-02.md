# Iteration 2 — patch + closed-book verification (uloop-control-play-mode)

## Patch landed

One semantic theme ("make async PlayMode entry explicit"). Added `### Asynchronous PlayMode entry` subsection under `## Notes` covering:

- The command returns immediately; PlayMode transitions on the next editor frame
- `IsPlaying` in the response reflects state at response-build time and may still be `false`
- Wait protocol: re-issue `--action Play` (idempotent) until `IsPlaying: true`, or insert a brief delay
- Lists affected PlayMode-dependent commands by name: `simulate-mouse-input`, `simulate-mouse-ui`, `simulate-keyboard`, `record-input`, `replay-input`
- "PlayMode is not active" error from those commands is recoverable, not terminal

Resolves Iter 1 gap CPM2 and indirectly resolves the `simulate-mouse-demo` Iter 1 unclear point ("wait a moment after Play").

## Verification

| Scenario | Outcome | Subagent | Sequencing doc'd? | Idempotency doc'd? |
|----------|---------|----------|------------------|-------------------|
| "Play モードに入ってから simulate-mouse-ui を呼びたい" | ○ | `a3a127b1096973958` | yes | yes |

closed-book mode. Same scenario as Iter 1, fresh subagent. Both predicted gaps now answered "yes" — patch verified.

## Residual gaps (Iter 2 self-report)

1. "Brief delay" duration unspecified (subagent assumed 1–2s).
2. Polling `--action Play` has no max-retry / timeout.
3. "PlayMode is not active" error format (JSON field vs exit code) not shown.

All three are low-severity 80-point ship items. The downstream-caller risk is bounded — retry logic naturally terminates when `IsPlaying: true` and Unity's PlayMode entry is bounded by editor responsiveness, not skill semantics.

## Convergence

| Criterion | Status |
|-----------|--------|
| New unclear points = 0 substantive | ✓ |
| All `[critical]` items satisfied | ✓ |
| Predicted Iter 1 gaps closed | ✓ |
| Cross-skill resolution for `simulate-mouse-demo` Iter 1 #1 | ✓ |
| Self-containment audit (SC.1) | ✓ |

Converged at Iter 2.
