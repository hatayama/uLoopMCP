# Iteration 0 — static audit (uloop-screenshot)

## Gaps found

| ID | Gap | Severity |
|----|-----|----------|
| SS1 | description had only 3 When items + no How suffix; missed the `--annotate-elements` use case central to `simulate-mouse-ui` workflows | high |
| SS2 | PlayMode requirement for `--capture-mode rendering` is in parameter description only, not in `## Notes` or precondition section | medium (downstream-caller risk: a caller could try `--capture-mode rendering` in EditMode) |
| SS3 | `## Notes` says "Use `uloop focus-window` first if needed" but does not say what "if needed" means | low |

## Iter 0 fix landed

SS1 only. Rewrote description to add a 4th When item (`--annotate-elements` for `simulate-mouse-ui` workflows) and append "Executes via `uloop screenshot` CLI invocation; writes PNG files under `.uloop/outputs/Screenshots/` (or a custom directory) and returns paths plus optional annotated-element metadata as JSON."

SS2 deferred to Iter 1 verification — closed-book subagent test will surface whether a downstream caller can confidently choose `--capture-mode` without leaking the EditMode-vs-PlayMode constraint into "Discretionary fill-ins". SS3 deferred (80-point ship).
