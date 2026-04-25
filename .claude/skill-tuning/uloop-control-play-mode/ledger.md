# Failure-pattern ledger — uloop-control-play-mode

Per-target ledger.

| Pattern | First seen | Iter | Notes |
|---------|-----------|------|-------|
| Async dispatch documented as if synchronous (or silent) | CPM2 | Iter 0 | Predicted in static audit; confirmed by Iter 1 closed-book subagent. Closed at Iter 2 with `### Asynchronous PlayMode entry` subsection. |
| `Play` idempotency not stated explicitly | Iter 1 #2 | Iter 1 | Closed at Iter 2 (the new wait protocol explicitly says "re-issue (it is idempotent)"). |
| "Brief delay" duration unspecified, no max-retry on poll | Iter 2 self-report | Iter 2 | Deferred. Low-severity 80-point ship items; downstream caller risk is bounded. |

## Cross-skill carryover (informational only)

The "blocking semantics gap" pattern (sync-looking interface that is actually async, or vice versa) is a recurring family across:
- `uloop-launch`: documentation said nothing about whether the command blocks; implementation does block via startup probe → Iter 1 patch made the blocking explicit.
- `uloop-control-play-mode` (this): command returns immediately but PlayMode entry is async → Iter 2 patch made the async-ness explicit.

Two opposite directions of the same family. When tuning future skills that issue commands to Unity, default to **stating the timing contract explicitly** in the `## Output` or `## Notes` section.
