# Failure pattern ledger — uloop-replay-input

| Pattern | First seen | Iter fixed | Notes |
|---------|------------|------------|-------|
| RP1: description lacked "How" suffix | Iter 0 | Iter 0 | Appended Input-System-state-injection How explanation. |
| RP2: `## Output` missing `Action` field | Iter 0 | Iter 0 | Same shape gap as RI2. Fixed by enumerating all 8 fields and adding nullability annotations matching `ReplayInputResponse.cs`. |

## Carryover (informational)
- Pair-skill with `record-input`. Both share the missing-`Action` gap and were fixed identically.
- Body's cross-reference to `record-input` for determinism guidance is correct and worth preserving — avoids duplication.
