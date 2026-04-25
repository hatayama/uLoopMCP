# Iteration 0 — static audit (uloop-get-logs)

## Gaps found

| ID | Gap | Severity |
|----|-----|----------|
| GL1 | description lacked CLI invocation hint | medium |
| GL2 | `## Output` is a one-line stub: "Returns JSON array of log entries with message, type, and optional stack trace." No field names, no schema, no log-type enum values, no timestamp/file/line indication | high (self-containment hole; closed-book reader cannot inspect a log entry's fields without guessing) |
| GL3 | `--max-count` interaction with `--log-type Error` filter ordering not stated (does max-count cap pre-filter or post-filter?) | low |

## Iter 0 fix landed

GL1 only. Appended to description: "Executes via `uloop get-logs` CLI invocation; returns a JSON array of log entries (filtered by log type, text search, or regex)."

GL2 deferred to Iter 1 verification — the subagent self-report on "response shape documented?" will demonstrate the gap concretely. If confirmed, Iter 2 patch will expand the `## Output` section with explicit field listing.

GL3 deferred (80-point ship).
