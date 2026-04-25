# Iteration 0 — static audit (find-orphaned-meta)

## Gaps found

| ID | Gap | Severity |
|----|-----|----------|
| FO1 | description / body relationship — confirm this skill already follows What→When→How and self-contained body | (audit only) |

## Iter 0 finding

This skill was left untouched because the body is already self-contained (it explains what orphaned `.meta` files are, when they appear, how to detect and clean them) and the description follows What→When→How. No Iter 0 patch landed.

Iter 1 will still run closed-book to verify subagent does not invent guesses, but no description-side fix was needed.
