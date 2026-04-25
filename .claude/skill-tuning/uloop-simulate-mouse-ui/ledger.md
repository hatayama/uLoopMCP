# Failure pattern ledger — uloop-simulate-mouse-ui

| Pattern | First seen | Iter fixed | Notes |
|---------|------------|------------|-------|
| SMU1: description's How was vague (mentioned EventSystem coordinates but not the synthetic-pointer-event mechanism) | Iter 0 | Iter 0 | Replaced with explicit "fires synthetic UI pointer events (PointerDown / Drag / PointerUp / PointerClick) … does not touch Mouse.current" — reinforces the boundary with simulate-mouse-input. |
| SMU2: NO `## Output` section at all (worst-case sub-pattern) | Iter 0 | Iter 0 | Same severity as SK and SMI2. Added 8 fields from `SimulateMouseUiResponse.cs` plus the documented Click-vs-Drag empty-space behavior rule. |

## Carryover (informational)
- Same "no `## Output` at all" sub-pattern as SK and SMI. Three skills in a row needed this fix.
- The split-drag rules (DragStart → DragMove → DragEnd, with DragEnd mandatory) are unique to this skill and worth surfacing in any cross-cutting documentation.
- The boundary distinction (this skill = synthetic UI pointer events; simulate-mouse-input = Mouse.current state injection) is the most consequential thing for tool selection and is now explicit in both descriptions.
