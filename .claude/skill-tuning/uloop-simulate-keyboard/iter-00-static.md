# Iteration 0 — static audit (uloop-simulate-keyboard)

## Gaps found

| ID | Gap | Severity |
|----|-----|----------|
| SK1 | description had no How suffix | medium |
| SK2 | No `## Output` section at all — completely missing response shape | high (worse than the response-shape-stub family — there is not even a stub) |
| SK3 | `--key` description says "Key name matching Input System Key enum (e.g. `W`, `Space`, `LeftShift`, `A`, `Enter`). Case-insensitive." — does not enumerate the full enum or link to it. May be acceptable since common keys are listed, but unclear what happens on unknown key. | low |
| SK4 | "All held keys are automatically released when PlayMode exits" — good, but no mention of what happens to held keys across scene reload or domain reload (likely lost) | low |

## Iter 0 fix landed

SK1 only. Appended: "Executes via `uloop simulate-keyboard` CLI invocation; injects into Unity Input System (`Keyboard.current`), so the project must use the new Input System and be in PlayMode."

SK2 deferred to Iter 1 verification; this one is most likely to trigger discretionary fill-ins from the subagent because there is literally no `## Output` to read. Iter 2 will read `SimulateKeyboardResponse.cs` and add the section.
SK3/SK4 deferred unless Iter 1 surfaces them.
