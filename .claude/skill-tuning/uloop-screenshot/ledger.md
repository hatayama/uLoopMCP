# Failure-pattern ledger — uloop-screenshot

Per-target ledger.

| Pattern | First seen | Iter | Notes |
|---------|-----------|------|-------|
| Description missed a high-value When item | SS1 | Iter 0 | Description listed 3 generic When items but omitted the `--annotate-elements` use case central to mouse workflows. Closed at Iter 0 by adding a 4th When item + How suffix. |
| PlayMode requirement scattered across parameter rows | SS2 | Iter 0 | Marked medium severity; Iter 1 verification confirmed the parameter-table placement is sufficient — subagent picked it up correctly. No patch needed. |

## Cross-skill carryover (informational only)

The "high-value When item missing from description" pattern echoes `simulate-mouse-demo` Iter 0. When tuning future skills, scan for the most-likely caller workflow and verify it appears in the When list — not just the abstract capability.
