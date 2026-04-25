# Iteration 1 — closed-book verify (uloop-simulate-keyboard)

## Subagent
- ID: a74acff88fbdbc310
- Mode: closed-book (inlined SKILL.md only)
- Tokens: 13510, duration: 11556ms

## Scenario A (Sprint Shift+W for 3s)
- Plan: `KeyDown LeftShift` → `KeyDown W` → wait 3s (used `sleep 3` as a fill-in) → `KeyUp W` → `KeyUp LeftShift`, with screenshot step inserted per workflow
- C1 PASS, C2 PASS, C3 PASS

### Discretionary fill-ins
1. `sleep 3` between KeyDown and KeyUp — skill body had no documented wait mechanism for multi-key holds (`--duration` is explicitly Press-only). Subagent inferred `sleep` from general shell knowledge.

### Out-of-source reaches
1. Same as above — `sleep 3` mechanism is shell knowledge, not in inlined text.

## Decision
- Critical PASS but two real gaps:
  - SK2: no `## Output` section → subagent could not name response fields
  - SK_wait: the wait primitive was implicit, agents fill in
- Iter 2 will add `## Output` and document the wait primitive.
