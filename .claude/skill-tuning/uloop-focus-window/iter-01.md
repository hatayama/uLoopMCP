# Iteration 1 — closed-book verify (uloop-focus-window)

## Subagent
- ID: a8af19aa63a26fbeb
- Mode: closed-book (inlined SKILL.md only)
- Tokens: 14300, duration: 7287ms

## Scenarios run
- A (median): Focus before screenshot
- B (edge): Pre-step before `uloop capture-unity-window`
- H (hold-out): Unity not running for this project

## Results

| Scenario | C1 cmd | C2 docfields-only | C3 no-invention |
|----------|--------|-------------------|-----------------|
| A | PASS | PASS | PASS |
| B | PASS | PASS | PASS |
| H | PASS | PASS | PASS |

- H correctly predicted `Success: false` with the documented Message string `No running Unity process found for this project`, NOT a fabricated process-list/PID/exception field.
- Subagent surfaced ONLY the two documented fields (`Success`, `Message`) across all three scenarios.

## Discretionary fill-ins
- 0

## Out-of-source reaches
- 0

## Convergence
- New unclear: 0 (was 2 in pre-Iter-0 audit: FW1 description + FW2 stub Output)
- Out-of-source reaches: 0
- All critical PASS at Iter 1
- No Iter 2 patches needed — converged at Iter 1.

FW converged.
