# Iteration 1 — closed-book median dispatch (uloop-run-tests)

## Result

| Scenario | Outcome | Accuracy | tool_uses | duration | retries |
|----------|---------|----------|-----------|----------|---------|
| Median: "EditMode と PlayMode の両方のテストを走らせて、失敗があれば詳細を見たい" | ○ | 100% (6/6 critical) | 0 | 10.1s | 0 |

Subagent: `aa286135ab598d3f2`. closed-book mode.

## Newly surfaced gaps

1. **`XmlPath` presence on success unclear.** Subagent flagged that the JSON field's behavior on `Success: true` is undocumented (always present? null? empty string?). Real downstream-caller risk: a caller iterating over results to read XML could trip over an empty path.
2. **EditMode → PlayMode ordering dependency unstated.** Subagent assumed independent ordering. Acceptable assumption, but undocumented.
3. **`--filter-value` interaction with `--filter-type all` unstated.** Acceptable; default behavior is obvious from the parameter table.
4. **Timeout / freeze behavior absent.** Single-flight constraint is now in description (Iter 0 fix), but skill does not say what to do if Unity freezes mid-run. Consistent with `uloop-launch`/`uloop-compile` recovery pattern that lives in CLAUDE.md.

Single-flight (RT1) is a **pass** — the description-level placement was correctly read by the subagent (quoted: "single-flight only — never run multiple `uloop run-tests` in parallel"). RT1 patch verified.

## Iter 2 plan

- Patch only gap #1: clarify `XmlPath` semantics in `## Output`. Empty string when no XML saved (typically on success), populated when tests failed.
- Add `CompletedAt` field that the response actually includes but the skill never mentioned (extracted from RunTestsResponse.cs).
- Gap #2/#3/#4 deferred (80-point ship; same family as cross-skill recovery patterns documented elsewhere).

## Convergence (after Iter 0 fix)

| Criterion | Status |
|-----------|--------|
| Single-flight constraint reaches the reader | ✓ |
| All `[critical]` items satisfied | ✓ |
| New unclear points = 1 substantive | △ → patched in Iter 2 |
