# Failure pattern ledger — uloop-clear-console

| Pattern | First seen | Iter fixed | Notes |
|---------|------------|------------|-------|
| CC1: description lacked "How" suffix | Iter 0 | Iter 0 | Appended `Executes via ... equivalent to clicking the Console window's Clear button.` |
| CC2: `## Output` was one-line "Returns JSON confirming…" — hid `ClearedCounts` breakdown | Iter 0 | Iter 2 | Iter 1 closed-book did not fabricate, but missed the value of `ClearedCounts.ErrorCount/WarningCount/LogCount`. Iter 2 patched `## Output` with full field list from `ClearConsoleResponse.cs`. |
| CC3: `--add-confirmation-message` text/location unspecified | Iter 0 | deferred | Not surfaced in Iter 1/2; leave |

## Carryover (informational)
- Same response-shape-stub family as EM/GL/FG/GH/SK.
