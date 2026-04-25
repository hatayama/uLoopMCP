# Iteration 0 — static audit (simulate-mouse-demo)

## Gaps found

| ID | Gap | Severity |
|----|-----|----------|
| SMD1 | description had no enumerated "(1)(2)(3)" When clauses (plain prose) | high |
| SMD2 | description lacked "How" suffix | high |
| SMD3 | body already structured as `## What / ## When / ## How` (well-formed) | n/a |

## Iter 0 fix landed

SMD1 and SMD2 in one edit: rewrote description to use enumerated When clauses and added "Executes via chained `uloop screenshot` and `uloop simulate-mouse-ui` CLI calls; requires SimulateMouseDemoScene loaded in PlayMode." as the How.
