# Failure pattern ledger — uloop-execute-menu-item

| Pattern | First seen | Iter fixed | Notes |
|---------|------------|------------|-------|
| EM1: description lacked "How" suffix per CLAUDE.md guideline | Iter 0 | Iter 0 | Appended `Executes via ... routes through EditorApplication.ExecuteMenuItem (or reflection fallback)` |
| EM2: response shape stub — `## Output` was one-line "Returns JSON with execution result" | Iter 0 | Iter 2 | Iter 1 closed-book did NOT fabricate fields (correctly admitted unknown shape), but still surfaced as a discretionary fill-in. Iter 2 patched `## Output` with full field list extracted from `ExecuteMenuItemResponse.cs`. Same family as GL2/FG2/CC2/GH2/SK2. |
| EM3: `--use-reflection-fallback` rationale unstated | Iter 0 | deferred | Not surfaced in Iter 1/2 closed-book runs; leaving for now |

## Carryover (informational)
- Cross-skill response-shape-stub family: encountered in EDC, GetLogs, FindGameObjects, ClearConsole, ExecuteMenuItem, GetHierarchy, SimulateKeyboard. Standard Iter 2 fix: read `<X>Response.cs` and inline the field list.
