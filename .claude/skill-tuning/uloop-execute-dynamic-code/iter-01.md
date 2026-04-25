# Iteration 1 — closed-book median dispatch (uloop-execute-dynamic-code)

## Result

| Scenario | Outcome | Accuracy | tool_uses | duration | retries |
|----------|---------|----------|-----------|----------|---------|
| Median: "Cube を赤くする C# を組み立てて実行" | ○ | 100% (5/5 critical) | 0 | 18.6s | 0 |

Subagent: `a385d4c466e964efe`. closed-book mode (SKILL.md inlined, all other sources forbidden).

## Newly surfaced gaps

1. **`## Output` section absent.** The subagent declared in `Discretionary fill-ins` that it inferred the response shape (assumed null/empty for void return). The skill never documents Success / Result / Logs / CompilationErrors / ErrorMessage / UpdatedCode. This is a large self-containment hole — every caller has to guess.
2. **PlayMode vs EditMode execution context unstated.** The subagent flagged this as an unclear point. The implementation supports both, but the skill is silent.
3. **Unity API specifics (GameObject.Find, MeshRenderer.material) inferred from training data.** This one is acceptable — the skill cannot enumerate every Unity API; the Code Examples reference files exist for that.

Discretionary fill-in #3 is *not* a gap. #1 and #2 are.

## Iter 2 plan

- Add `## Output` section listing the JSON fields (resolves #1)
- Add a one-liner in the same section stating "Both EditMode and PlayMode are supported" (resolves #2)
- One semantic theme = "make the response contract explicit"
