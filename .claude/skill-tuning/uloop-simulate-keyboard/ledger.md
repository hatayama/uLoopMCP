# Failure pattern ledger — uloop-simulate-keyboard

| Pattern | First seen | Iter fixed | Notes |
|---------|------------|------------|-------|
| SK1: description lacked "How" suffix | Iter 0 | Iter 0 | Appended `Executes via uloop simulate-keyboard ... injects into Keyboard.current ... requires PlayMode and new Input System.` |
| SK2: no `## Output` section at all (worse than stub family) | Iter 0 | Iter 2 | Iter 1 subagent admitted unknown shape and committed to surfacing raw stdout. Iter 2 added full `## Output` from `SimulateKeyboardResponse.cs`: `Success`, `Message`, `Action`, `KeyName` (nullable). |
| SK_wait: how to hold multi-key combos for a fixed duration was undocumented (the example body showed KeyDown/screenshot/KeyUp without an explicit wait, leaving the wait mechanism implicit) | Iter 1 (subagent fill-in: `sleep 3`) | Iter 2 | Added explicit rule: prefer `--action Press --duration` for single keys; for multi-key holds, use `sleep N` between KeyDown and KeyUp. Iter 2 verification confirmed the subagent picked `sleep 3` from the new documented rule rather than guessing. |
| SK3: `--key` does not enumerate full Input System Key enum | Iter 0 | deferred | Common keys listed via examples; not surfaced in Iter 1/2 |
| SK4: held-key behavior across scene/domain reload undocumented | Iter 0 | deferred | Edge case; not surfaced |

## Carryover (informational)
- Same response-shape-stub family as EM/CC/GH/GL/FG, plus the additional "no `## Output` at all" sub-pattern (worst case in the family).
- SK_wait pattern: when the example shows a sequence but omits the timing primitive between commands, agents will fill in. Document the timing primitive explicitly.
