# Iteration 0 — static audit (uloop-get-hierarchy)

## Gaps found

| ID | Gap | Severity |
|----|-----|----------|
| GH1 | description lacked How suffix | medium |
| GH2 | `## Output` is one line ("Returns JSON with hierarchical structure of GameObjects and their components") — no field names, no nesting shape | high (response shape stub family — same as GL2/FG2/EM2; this one is likely the largest because hierarchy + components + LUT) |
| GH3 | `--use-components-lut` (`auto`/`true`/`false`) — body does not explain what LUT means or when `auto` chooses true vs false | low–medium |
| GH4 | `--include-paths` semantics (`false` by default) — what does "full path" add when there is already nested parent-child structure? | low |

## Iter 0 fix landed

GH1 only. Appended: "Executes via `uloop get-hierarchy` CLI invocation; returns the scene's GameObject tree as nested JSON (with optional component metadata and depth limiting)."

GH2 deferred to Iter 1 verification; if subagent invents fields, expand `## Output` in Iter 2 by reading `GetHierarchyResponse.cs`.
GH3/GH4 deferred — only patch if Iter 1 surfaces guesses.
