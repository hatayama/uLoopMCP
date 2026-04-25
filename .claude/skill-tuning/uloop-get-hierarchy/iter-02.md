# Iteration 2 — correctness fix + response-shape patch + verify (uloop-get-hierarchy)

## Patches landed
1. **Description rewrite** (was: "returns the scene's GameObject tree as nested JSON"). Now: "the actual hierarchy data is written to a JSON file on disk and the response returns the file path (not the tree inline) — open the file to read the structure."
2. **`## Output` rewrite** to document `message` and `hierarchyFilePath` and state explicitly that the hierarchy itself is in the file at that path.

Source-of-truth target: `Packages/src/Editor/Api/McpTools/GetHierarchy/Skill/SKILL.md`. Mirrored to `.claude/skills/uloop-get-hierarchy/SKILL.md`.

## Verification subagent
- ID: a0910fe6079f53f45
- Mode: closed-book on post-patch SKILL.md
- Tokens: 13130, duration: 13626ms

## Verification result (Scenario A: "show entire hierarchy with components")
- Plan: `uloop get-hierarchy` (defaults), THEN open `hierarchyFilePath` via Read tool to surface tree
- C1 PASS, C2 PASS (correctly recognized file-path indirection), C3 PASS
- Discretionary fill-ins: none
- Out-of-source reaches: none

## Convergence
- New unclear: 0
- Fields fabricated: 0
- Substantive correctness bug GH4 (file-path-vs-inline) eliminated
- Subagent independently planned the file-read follow-up step from the body alone — strong signal that the "open the file" guidance is unambiguous

GH converged.
