# Iteration 0 — static audit (uloop-execute-dynamic-code)

## Gaps found

| ID | Gap | Severity |
|----|-----|----------|
| EDC1 | description lacked "How" suffix (no CLI invocation hint, no execution model) | high |
| EDC2 | "If execution fails, adjust code and retry" in workflow has no retry-bound or termination condition | low (workflow-completeness, deferred) |
| EDC3 | shell quoting differs zsh vs PowerShell — covered in Parameters section, no description preview | low (acceptable; the body covers it explicitly) |

## Iter 0 fix landed

EDC1 only. Appended to description: "Executes via `uloop execute-dynamic-code` CLI invocation; compiles snippet with Roslyn and runs synchronously inside the Unity Editor process."

EDC2 and EDC3 deferred — both are 80-point ship items.
