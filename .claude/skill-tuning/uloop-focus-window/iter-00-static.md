# Iter 0 — static description ↔ body audit (uloop-focus-window)

## Source of truth
`Packages/src/Cli~/src/skills/skill-definitions/cli-only/uloop-focus-window/Skill/SKILL.md`
mirrored to `.claude/skills/uloop-focus-window/SKILL.md`.

CLI-only skill (no Response.cs). Implementation: `Packages/src/Cli~/src/commands/focus-window.ts`.

## Pre-Iter-0 gaps

| ID | Pattern | Where seen | Iter 0 fix |
|----|---------|------------|------------|
| FW1 | description lacked "How" suffix per CLAUDE.md What→When→How rule | frontmatter | Appended: "Routes through the uloop CLI which calls OS-level focus APIs (osascript on macOS, PowerShell on Windows) via the launch-unity library; bypasses the Unity TCP server so it works even while Unity is compiling or in domain reload." |
| FW2 | response-shape stub: `## Output` was the one-liner "Returns JSON confirming the window was focused." Same family as EM/CC/GH/GL/FG. Closed-book agents would either invent fields (PID, windowHandle, platform) or admit ignorance. | `## Output` | Expanded to enumerate the only two real fields (`Success`, `Message`) extracted from `focus-window.ts:31-77`, with examples of the actual `Message` strings the implementation emits, and an explicit "no PID/window-handle/platform field" disclaimer to forestall fabrication. |

## Body-vs-description gaps remaining
- None material: description's three "Use when" cases all have body coverage; description does not name parameters or fields the body is missing.

## Cross-skill carryover
- Same response-shape-stub family as Iter 0 of EM/CC/GH/GL/FG/SK (batch 2 + 3). Standard fix applied.

## Decision
- Iter 0 patches landed. Proceed to Iter 1 closed-book verification — expect zero fabricated fields after the explicit two-field declaration.
