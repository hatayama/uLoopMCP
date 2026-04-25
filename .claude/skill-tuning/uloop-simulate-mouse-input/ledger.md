# Failure pattern ledger — uloop-simulate-mouse-input

| Pattern | First seen | Iter fixed | Notes |
|---------|------------|------------|-------|
| SMI1: description's How clause | Iter 0 | Iter 0 (no patch needed) | Existing wording "Injects … directly into Mouse.current" already satisfies the How requirement. Marked as compliant. |
| SMI2: NO `## Output` section at all (worst-case sub-pattern) | Iter 0 | Iter 0 | Same severity as SK from batch 3. Added full `## Output` from `SimulateMouseInputResponse.cs`: `Success`, `Message`, `Action`, `Button` (nullable), `PositionX` (nullable float), `PositionY` (nullable float). Disclaimer covers the most likely fabrications: delta/scroll fields. |

## Carryover (informational)
- Same "no `## Output` at all" sub-pattern as SK. Both fixed identically by extracting fields from the `*Response.cs`.
- Critical lesson reaffirmed: missing `## Output` is the worst case in the response-shape-stub family, more likely to produce fabrications than a one-line stub.
