# Failure-pattern ledger — uloop-execute-dynamic-code

Per-target ledger (no cross-skill pattern merging). Iter that introduced each entry shown in `Iter` column.

| Pattern | First seen | Iter | Notes |
|---------|-----------|------|-------|
| Response shape undocumented (no `## Output` section) | EDC1 family | Iter 1 | Subagent had to invent the response contract from training data. Closed at Iter 2 by adding the full field list. |
| Workflow step "adjust code and retry" lacks termination | EDC2 | Iter 0 | Deferred. Low-severity workflow-completeness item; no downstream-caller risk. |

## Cross-skill carryover (informational only)

The "response shape undocumented" pattern is a recurring family observed in: `uloop-execute-dynamic-code` (this), `uloop-launch` (different shape — startup probe summary). Each occurrence is patched per-skill rather than via shared template.
