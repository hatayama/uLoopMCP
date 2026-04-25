# Failure pattern ledger — uloop-record-input

| Pattern | First seen | Iter fixed | Notes |
|---------|------------|------------|-------|
| RI1: description lacked "How" suffix | Iter 0 | Iter 0 | Appended Input-System-device-state-diff How explanation. |
| RI2: `## Output` missing `Action` field | Iter 0 | Iter 0 | `RecordInputResponse.cs` declares `Action` as always-populated; the original `## Output` documented only the conditional Stop-only fields and skipped the always-on `Action` echo. Added it plus tightened nullability. |

## Carryover (informational)
- Same response-shape-stub family as EM/CC/GH/GL/FG/SK. This skill was only mildly affected (one missing field, not a stub).
- The deterministic-replay design table is the most valuable part of this skill body and should be preserved verbatim — multiple downstream skills (replay-input) reference it.
