# Checklist — uloop-replay-input

## Scenario A
- [critical] Issues `uloop replay-input --action Start` (no `--input-path` is fine; auto-detects latest)
- [critical] Reports response using ONLY documented fields (`Success`, `Message`, `Action`, `InputPath`, `CurrentFrame`, `TotalFrames`, `Progress`, `IsReplaying`)
- Notes PlayMode + New Input System prerequisites

## Scenario B
- [critical] Uses `--loop true` on the Start command
- Specifies `--input-path scripts/intro-demo.json`
- Does not invent loop-counter fields in the response

## Scenario H (hold-out)
- [critical] Issues `uloop replay-input --action Status` to query progress
- [critical] Reports `CurrentFrame`, `TotalFrames`, and `Progress` from the response
- Does not invent `ElapsedSeconds`, `RemainingSeconds`, or per-frame inspection fields
