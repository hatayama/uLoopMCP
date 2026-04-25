# Iteration 1 — closed-book verify (uloop-simulate-mouse-ui)

## Subagent
- ID: a8bba7c0663278af9
- Mode: closed-book (inlined SKILL.md only)
- Tokens: 16375, duration: 14532ms

## Scenarios run
- A (median): Click ButtonStart at SimX=400, SimY=300
- B (edge): Slow drag (200,400) → (600,400) for visual inspection
- H (hold-out): Split drag with two screenshot pauses (300,300) → (500,300) → (700,300)

## Results

| Scenario | C1 cmd | C2 docfields-only | C3 no-invention | C4 split-drag-sequence |
|----------|--------|-------------------|-----------------|-------------------------|
| A | PASS | PASS | PASS | — |
| B | PASS | PASS | PASS | — |
| H | PASS | PASS | PASS | PASS |

- A: clean `Click --x 400 --y 300`, surfaced `HitGameObjectName` to verify ButtonStart was the topmost element under the pointer.
- B: correctly chose `--drag-speed 200` citing the body's "200 is slow enough to watch" guidance verbatim.
- H: ran the full DragStart → screenshot → DragMove → screenshot → DragEnd sequence in correct order; explicitly noted that "DragStart must precede DragMove or DragEnd; DragEnd must be called to release the drag state" — lifted from the documented Split Drag Rules.

## Discretionary fill-ins
- 0 substantive

## Out-of-source reaches
- 0

## Convergence
- New unclear: 0 (was 2 in pre-Iter-0 audit: SMU1 vague description How, SMU2 missing Output entirely)
- Out-of-source reaches: 0
- All critical PASS including C4 (split-drag sequence + DragEnd-mandatory)
- No Iter 2 patches needed — converged at Iter 1.

SMU converged.
