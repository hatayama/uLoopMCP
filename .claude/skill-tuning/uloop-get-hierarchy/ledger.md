# Failure pattern ledger — uloop-get-hierarchy

| Pattern | First seen | Iter fixed | Notes |
|---------|------------|------------|-------|
| GH1: description lacked "How" suffix | Iter 0 | Iter 0 | Initial Iter 0 patch said "returns the scene's GameObject tree as nested JSON" — this was WRONG (see GH4 below). Iter 2 corrected. |
| GH2: `## Output` was one-line "Returns JSON with hierarchical structure" | Iter 0 | Iter 2 | Iter 1 subagent did not fabricate but had no usable shape info. Iter 2 patched. |
| GH3: `--use-components-lut auto` rationale unstated | Iter 0 | deferred | Not surfaced in Iter 1/2 |
| GH4: **CORRECTNESS BUG** — both description and `## Output` claimed inline JSON; the actual response is `{ message, hierarchyFilePath }` and the hierarchy itself is written to a file on disk | Iter 2 (discovered when reading `GetHierarchyResponse.cs`) | Iter 2 | High severity. Without this fix, agents would parse a file path as if it were the hierarchy object. Patch reworded both description and `## Output` to make the file-path indirection explicit. |

## Carryover (informational)
- GH4 is the first **substantive misdirection** found in any tuned skill — earlier ledger entries were "missing info" / "stub". Lesson: open the `<X>Response.cs` for every skill, even when the body looks plausible.
