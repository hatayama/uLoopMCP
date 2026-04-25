# Failure-pattern ledger — uloop-find-game-objects

Per-target ledger.

| Pattern | First seen | Iter | Notes |
|---------|-----------|------|-------|
| Response shape stub instead of field list | FG2 | Iter 0 | Predicted, confirmed by Iter 1, closed at Iter 2 with full list extracted from `FindGameObjectsResponse.cs` + `ComponentInfo.cs`. |
| `--required-components` filter-vs-projection ambiguity | Iter 1 #2 | Iter 1 | Resolved implicitly by Iter 2's expanded `## Output`: since `components[]` always lists all components on each result, `--required-components` is unambiguously a filter. No separate patch needed. |
| File-export branch existed but multi-Selected vs single-Selected behavior was implicit | FG1 (description-level) | Iter 0 | Closed at Iter 0 by mentioning file-write fallback in description, expanded at Iter 2 in `## Output`. |

## Cross-skill carryover (informational only)

Same "response shape stub" family as `uloop-get-logs` and `uloop-execute-dynamic-code`. Confirmed pattern: when implementation has nested response classes (`FindGameObjectResult`, `ComponentInfo`, `ComponentPropertyInfo`), the skill must walk the nesting explicitly — referencing only the top-level "results" array is insufficient.
