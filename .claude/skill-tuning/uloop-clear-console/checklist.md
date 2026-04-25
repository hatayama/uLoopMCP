# Checklist — uloop-clear-console

## Scenario A (Clear + report error count)
- [critical] Issues `uloop clear-console` (no flags)
- [critical] Reports error count via `ClearedCounts.ErrorCount` (documented path)
- Does not invent fields

## Scenario B (Clear with confirmation)
- [critical] Issues `uloop clear-console --add-confirmation-message`
- Reports `Success` and `Message`

## Scenario H (Clear then verify)
- Issues `uloop clear-console`, surfaces `ClearedLogCount`
- For verification, would call `uloop get-logs` (not invent a re-check field on clear-console's response)
