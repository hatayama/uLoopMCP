# Iteration 1 — closed-book median dispatch (uloop-find-game-objects)

## Result

| Scenario | Outcome | Accuracy | tool_uses | duration | retries |
|----------|---------|----------|-----------|----------|---------|
| Median: "Player という名前の GameObject に Rigidbody が付いているか確認したい" | ○ | 100% (5/5 critical) | 0 | 10.5s | 0 |

Subagent: `a68093dcb5d217e81`. closed-book mode.

## Newly surfaced gaps

1. **FG2 confirmed.** Subagent could not state any field name in the per-GameObject record. Same family as GL2.
2. **`--required-components` semantics ambiguous.** Subagent flagged uncertainty: is it a filter (only return GameObjects with that component), or an output projection (include those components in the response)? Both readings are plausible from the parameter description "Required components". Real downstream-caller risk.
3. **Fall-through behavior on multi-result for non-Selected modes** (= FG3, deferred at Iter 0). Subagent assumed inline JSON; correct.

## Iter 2 plan

- Expand `## Output` with full field list extracted from `FindGameObjectsResponse.cs` + `ComponentInfo.cs`:
  - `results[]: {name, path, isActive, tag, layer, components[]}`
  - `components[]: {type, fullTypeName, properties[]}`
  - `properties[]: {name, type, value}` (only with `--include-inherited-properties`)
  - `totalFound`, `errorMessage`, `processingErrors[]`
  - File-export sub-section for multi-Selected: `resultsFilePath`, `message`
- Gap #2 (`--required-components` semantics) is a parameter-table issue, not Output. Defer to Iter 3 if a future verification still surfaces it; the expanded Output (which doesn't separately list "matched components") implicitly clarifies this is a filter.
