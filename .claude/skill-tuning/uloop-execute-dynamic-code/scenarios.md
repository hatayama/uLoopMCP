# Scenarios — uloop-execute-dynamic-code

| ID | Type | Prompt |
|----|------|--------|
| M | median | "Cube を赤くする C# を組み立てて実行" |

closed-book mode: subagent receives only the SKILL.md text inlined in the dispatch prompt. Reference files under `references/` are excluded from the verification scope (the skill's reference loader is not exercised at the paper-exercise level).

Single-scenario design: this skill's surface is one CLI invocation; multiple scenarios would all hit the same `## Output` and `## Code Rules` sections. PlayMode-vs-EditMode coverage is implicit in the median (subagent self-reports whether the mode is documented).
