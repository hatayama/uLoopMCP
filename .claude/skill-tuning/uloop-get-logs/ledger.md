# Failure-pattern ledger — uloop-get-logs

Per-target ledger.

| Pattern | First seen | Iter | Notes |
|---------|-----------|------|-------|
| Response shape stub ("Returns JSON array of log entries with...") instead of field list | GL2 | Iter 0 | Predicted in static audit; confirmed by Iter 1 closed-book subagent (could not name `Type`/`Message`/`StackTrace`). Closed at Iter 2 with full field list extracted from `GetLogsResponse.cs`. |

## Cross-skill carryover (informational only)

The "response shape stub" pattern recurs across `uloop-execute-dynamic-code`, `uloop-find-game-objects`, and this skill. All three were closed by extracting the response field list from the corresponding C# response class. Future skills with thin `## Output` sections should be audited the same way: open the `*Response.cs` file, port the public properties into a documented field list with explicit enum values where applicable.
