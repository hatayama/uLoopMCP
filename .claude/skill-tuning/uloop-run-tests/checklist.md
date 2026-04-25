# Checklist — uloop-run-tests

## Scenario M

- `[critical]` Issue `uloop run-tests` calls **sequentially** (never in parallel)
- `[critical]` Recognize the single-flight constraint from the description
- `[critical]` After a failure, identify `XmlPath` as the field to read for detail
- `[critical]` Recognize that `XmlPath` may be empty on success
- `[critical]` State the response shape (Success/TestCount/PassedCount/FailedCount/SkippedCount/XmlPath/CompletedAt)
- `[critical]` Self-report any unclear points or discretionary fill-ins
