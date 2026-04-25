# Iteration 0 — static audit (uloop-find-game-objects)

## Gaps found

| ID | Gap | Severity |
|----|-----|----------|
| FG1 | description lacked CLI invocation hint and the file-export semantics for multi-Selected | high |
| FG2 | `## Output` says "Returns JSON with matching GameObjects" — no field names, no schema | high (same family as GL2: response shape unspecified) |
| FG3 | `Selected` mode behavior when scene is unloaded or no scene is open is unstated | low |

## Iter 0 fix landed

FG1 only. Appended to description: "Executes via `uloop find-game-objects` CLI invocation; returns matching GameObjects with hierarchy paths and components as JSON (or writes to a file when multiple GameObjects are selected)."

FG2 deferred to Iter 1 verification. If confirmed, Iter 2 will expand `## Output`.

FG3 deferred (80-point ship).
