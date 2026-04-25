# Iter 0 — static description ↔ body audit (uloop-simulate-mouse-input)

## Source of truth
`Packages/src/Editor/Api/McpTools/SimulateMouseInput/Skill/SKILL.md`
mirrored to `.claude/skills/uloop-simulate-mouse-input/SKILL.md`.
Response shape: `Packages/src/Editor/Api/McpTools/SimulateMouseInput/SimulateMouseInputResponse.cs`.

## Pre-Iter-0 gaps

| ID | Pattern | Where seen | Iter 0 fix |
|----|---------|------------|------------|
| SMI1 | description had implicit "How" via "Injects … directly into Mouse.current" but the existing wording was sufficient | frontmatter | No change. The wording "Injects button clicks, mouse delta, and scroll wheel directly into Mouse.current" already satisfies the How requirement (it names the technical mechanism). |
| SMI2 | NO `## Output` section at all — same severity as the SK case in batch 3. Closed-book agents would have to either invent fields or admit ignorance, since the only signal would be "nothing is documented". | bottom of body | Added full `## Output` enumerating the six real fields from `SimulateMouseInputResponse.cs`: `Success`, `Message`, `Action`, `Button` (nullable), `PositionX` (nullable float), `PositionY` (nullable float). Plus explicit "no `DeltaX`/`DeltaY`/`ScrollX`/`ScrollY`/`Duration`/hit-element field" forestaller and a verify-via-screenshot reminder. |

## Body-vs-description gaps remaining
- None material: When-to-use table, parameters, actions, examples are comprehensive; description's New-Input-System caveat is reflected in body prerequisites.

## Cross-skill carryover
- Same "no `## Output` at all" worst-case sub-pattern as SK from batch 3. Standard fix re-applied.
- The Action enum (`Click`, `LongPress`, `MoveDelta`, `SmoothDelta`, `Scroll`) is critical to surface accurately so closed-book agents don't invent enum values.

## Decision
- Iter 0 patches landed. Proceed to Iter 1 closed-book verification.
