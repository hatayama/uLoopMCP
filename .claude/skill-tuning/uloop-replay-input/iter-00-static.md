# Iter 0 — static description ↔ body audit (uloop-replay-input)

## Source of truth
`Packages/src/Editor/Api/McpTools/ReplayInput/Skill/SKILL.md`
mirrored to `.claude/skills/uloop-replay-input/SKILL.md`.
Response shape: `Packages/src/Editor/Api/McpTools/ReplayInput/ReplayInputResponse.cs`.

## Pre-Iter-0 gaps

| ID | Pattern | Where seen | Iter 0 fix |
|----|---------|------------|------------|
| RP1 | description lacked "How" suffix per CLAUDE.md What→When→How rule | frontmatter | Appended: "Routes through uloop CLI to Unity, which deserializes the JSON recording and pushes the captured Input System device states back into Mouse.current / Keyboard.current frame-by-frame in PlayMode; requires PlayMode and the New Input System." |
| RP2 | `## Output` was missing the `Action` field that `ReplayInputResponse.cs` declares (`Action` is always populated for `Start`/`Stop`/`Status`) | `## Output` | Added `Action` echo, tightened nullability to match `*?` declarations on `InputPath`, `CurrentFrame`, `TotalFrames`, `Progress`, `IsReplaying`. Added "no `LoopCount`/`ElapsedSeconds`/`OverlayVisible`" forestaller. |

## Body-vs-description gaps remaining
- None material: body's deterministic-replay cross-reference to `record-input` is appropriate; parameters and prerequisites all align.

## Cross-skill carryover
- Same single-field-gap pattern as RI2. The pair record-input/replay-input share their response-class shape pattern (Action always present, file-related fields nullable per phase).

## Decision
- Iter 0 patches landed. Proceed to Iter 1 closed-book verification.
