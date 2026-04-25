# Iteration 1 — closed-book verify (uloop-simulate-mouse-input)

## Subagent
- ID: a3863cf5e10d3ba77
- Mode: closed-book (inlined SKILL.md only)
- Tokens: 16104, duration: 19570ms

## Scenarios run
- A (median): Right-click at (400, 300) for block placement
- B (edge): SmoothDelta 300px right over 0.5s
- H (hold-out): Legacy Input Manager project, click at (400, 300)

## Results

| Scenario | C1 cmd | C2 docfields-only | C3 no-invention | C4 EDC-recommendation |
|----------|--------|-------------------|-----------------|------------------------|
| A | PASS | PASS | PASS | — |
| B | PASS | PASS | PASS | — |
| H | PASS (no call) | PASS (n/a) | PASS | PASS |

- A: correctly used `--button Right` for the right-click case. Surfaced `Action`/`Button`/`PositionX`/`PositionY` as the documented echo fields. Included the workflow-mandated PlayMode pre-check and post-screenshot.
- B: correctly chose `SmoothDelta` (NOT MoveDelta which is one-shot) with `--duration 0.5`. Explicitly noted it would NOT surface `DeltaX`/`DeltaY`/`Duration` because they are not in the documented response, and would rely on the screenshot for visual confirmation. Strong negative-evidence behavior.
- H: correctly refused to run `simulate-mouse-input` on a legacy project and recommended `execute-dynamic-code` per the skill body — verbatim from the documented decision rule, not invented.

## Discretionary fill-ins
- 0 substantive (only "PlayMode check needed" inferred from Workflow step 1, which is documented)

## Out-of-source reaches
- 0

## Convergence
- New unclear: 0 (was 2 in pre-Iter-0 audit: SMI1 description-OK, SMI2 missing Output entirely)
- Out-of-source reaches: 0
- All critical PASS including C4 (legacy-input recommendation)
- No Iter 2 patches needed — converged at Iter 1.

SMI converged.
