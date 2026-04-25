# Iteration 1 — closed-book median dispatch (uloop-get-logs)

## Result

| Scenario | Outcome | Accuracy | tool_uses | duration | retries |
|----------|---------|----------|-----------|----------|---------|
| Median: "コンパイル後に NullReferenceException が出たログだけ stack trace 付きで取得したい" | ○ | 100% (5/5 critical) | 0 | 12.5s | 0 |

Subagent: `ab0620240b09a0238`. closed-book mode.

## Newly surfaced gaps

1. **GL2 confirmed in full force.** Subagent could not state any per-entry field name with confidence ("`message`? `text`? `logMessage`?"). The "`type` enum values" question went unanswered. The single-line `## Output` ("Returns JSON array of log entries with message, type, and optional stack trace") is exactly the failure mode the closed-book test was designed to catch — descriptive prose without field-list specificity.
2. **`--include-stack-trace true` syntax unclear.** Subagent guessed at the boolean syntax (flag-only vs `=true`). Low severity; same convention applies across all `uloop` skills, but is not stated here.
3. **Pre-filter vs post-filter semantics for `--max-count` unstated** (= GL3, deferred at Iter 0).

Gap #1 is the substantive one and exactly matches the predicted Iter 0 hypothesis.

## Iter 2 plan

- Expand `## Output` with explicit field list: `TotalCount`, `DisplayedCount`, `LogType`, `MaxCount`, `SearchText`, `IncludeStackTrace`, `Logs[]: {Type, Message, StackTrace}`. Populated from `GetLogsResponse.cs`.
- Same semantic theme as EDC Iter 2: "make response shape explicit".
