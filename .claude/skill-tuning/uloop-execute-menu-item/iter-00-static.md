# Iteration 0 — static audit (uloop-execute-menu-item)

## Gaps found

| ID | Gap | Severity |
|----|-----|----------|
| EM1 | description lacked How suffix | medium |
| EM2 | `## Output` is one line ("Returns JSON with execution result") — no field names | high (response shape stub family — same as GL2/FG2) |
| EM3 | `--use-reflection-fallback` rationale unstated (when does the primary path fail?) | low |

## Iter 0 fix landed

EM1 only. Appended: "Executes via `uloop execute-menu-item` CLI invocation; routes through Unity's `EditorApplication.ExecuteMenuItem` (or a reflection fallback) and returns the execution result as JSON."

EM2 deferred to Iter 1 verification — same pattern as GL2/FG2. Iter 2 will expand `## Output`.
