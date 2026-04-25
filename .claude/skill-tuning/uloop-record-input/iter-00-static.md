# Iter 0 — static description ↔ body audit (uloop-record-input)

## Source of truth
`Packages/src/Editor/Api/McpTools/RecordInput/Skill/SKILL.md`
mirrored to `.claude/skills/uloop-record-input/SKILL.md`.
Response shape: `Packages/src/Editor/Api/McpTools/RecordInput/RecordInputResponse.cs`.

## Pre-Iter-0 gaps

| ID | Pattern | Where seen | Iter 0 fix |
|----|---------|------------|------------|
| RI1 | description lacked "How" suffix per CLAUDE.md What→When→How rule | frontmatter | Appended: "Routes through uloop CLI to Unity, which captures Input System device-state diffs frame-by-frame in PlayMode and serializes them to JSON when stopped; requires PlayMode and the New Input System." |
| RI2 | `## Output` was missing the `Action` field that `RecordInputResponse.cs` declares (`Action` is always populated) — would have led closed-book agents to either omit it from response handling or assume it's not surfaced | `## Output` | Added `Action` (`Start`/`Stop` echo) and tightened nullability annotations to match `*?` declarations on `OutputPath`, `TotalFrames`, `DurationSeconds`. Added explicit "these are the only six fields" disclaimer plus "no `RecordingId`/`StartTimestamp`/`KeysCaptured`" forestaller. |

## Body-vs-description gaps remaining
- None material: body's deterministic-replay table, prerequisites, and parameter list all align with description. The deterministic-replay guidance is robust — keep as-is.

## Cross-skill carryover
- Same response-shape-stub family as EM/CC/GH/GL/FG/SK. The body had a partial Output already; the gap was a single missing field (`Action`).

## Decision
- Iter 0 patches landed. Proceed to Iter 1 closed-book verification.
