# Iteration 0 — static audit (uloop-clear-console)

## Gaps found

| ID | Gap | Severity |
|----|-----|----------|
| CC1 | description lacked How suffix | medium |
| CC2 | `## Output` is one line ("Returns JSON confirming the console was cleared") — no field names | low (low-stakes confirmation, but still response-shape stub family) |
| CC3 | `--add-confirmation-message` does not specify what the confirmation looks like (where it appears, what text) | low |

## Iter 0 fix landed

CC1 only. Appended: "Executes via `uloop clear-console` CLI invocation; equivalent to clicking the Console window's Clear button."

CC2 deferred — same response-shape-stub family as GL2/FG2/EM2; verify in Iter 1 whether subagent invents field names.
CC3 deferred — only matters if Iter 1 subagent guesses behavior.
