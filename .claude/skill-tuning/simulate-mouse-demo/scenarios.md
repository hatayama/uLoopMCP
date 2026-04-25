# Scenarios — simulate-mouse-demo

| ID | Type | Prompt |
|----|------|--------|
| M | median | "demo シナリオを最初から最後まで実行" |

closed-book mode: subagent receives only the SKILL.md text. The skill is a chained workflow (screenshot → simulate-mouse-ui repeated), so the median exercises the whole sequence. SC.1 (self-containment audit) verifies the subagent can reach the end of the demo without consulting any other skill body.
