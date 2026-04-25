# Iter 0 — static description ↔ body audit (uloop-simulate-mouse-ui)

## Source of truth
`Packages/src/Editor/Api/McpTools/SimulateMouseUi/Skill/SKILL.md`
mirrored to `.claude/skills/uloop-simulate-mouse-ui/SKILL.md`.
Response shape: `Packages/src/Editor/Api/McpTools/SimulateMouseUi/SimulateMouseUiResponse.cs`.

## Pre-Iter-0 gaps

| ID | Pattern | Where seen | Iter 0 fix |
|----|---------|------------|------------|
| SMU1 | description's "via EventSystem screen coordinates" hinted at the mechanism but did not state that synthetic UI pointer events are fired (PointerDown/Drag/PointerUp/PointerClick) and explicitly that Mouse.current is NOT touched. This distinction matters because agents could otherwise reach for simulate-mouse-ui to drive game-logic that reads Mouse.current. | frontmatter | Replaced with: "Routes through uloop CLI to Unity which fires synthetic UI pointer events (PointerDown / Drag / PointerUp / PointerClick) at the given screen coordinates through the active EventSystem and GraphicRaycaster — does not touch Mouse.current." Reinforces the boundary with simulate-mouse-input. |
| SMU2 | NO `## Output` section at all — same worst-case sub-pattern as SK and SMI2 | bottom of body | Added full `## Output` from `SimulateMouseUiResponse.cs`: `Success`, `Message`, `Action`, `HitGameObjectName` (nullable), `PositionX`, `PositionY`, `EndPositionX` (nullable), `EndPositionY` (nullable). Plus the documented "Click/LongPress on empty space → Success=true with HitGameObjectName=null" vs "Drag on empty space → Success=false" rule, lifted from the existing Coordinate-System section. |

## Body-vs-description gaps remaining
- None material: Workflow (1-6 steps), Tool Reference (full param table + actions table + split-drag rules), Coordinate System, Examples, and Prerequisites are all comprehensive.

## Cross-skill carryover
- Same "no `## Output` at all" sub-pattern as SK / SMI. Standard fix re-applied.
- This is a complement skill to simulate-mouse-input — the tool selection table in both bodies is the canonical decision point and is preserved.

## Decision
- Iter 0 patches landed. Proceed to Iter 1 closed-book verification.
