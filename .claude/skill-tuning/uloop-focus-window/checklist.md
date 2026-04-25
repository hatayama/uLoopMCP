# Checklist — uloop-focus-window

## Scenario A
- [critical] Issues `uloop focus-window` (no args needed)
- [critical] Reports response using ONLY documented fields (`Success`, `Message`)
- Does not invent PID, window-handle, platform, or process-list fields

## Scenario B
- [critical] Calls `uloop focus-window` BEFORE the capture step
- Notes that focus-window works while Unity is busy

## Scenario H (hold-out)
- [critical] Predicts `Success: false` with `Message` such as "No running Unity process found for this project" — NOT an exception or a missing key
- Surfaces the documented Message string content, not a paraphrase invented from scratch
- Does not invent retry/launch logic in the response shape itself
