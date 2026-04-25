# Checklist — uloop-record-input

## Scenario A
- [critical] Issues `uloop record-input --action Start` then `uloop record-input --action Stop` (sequence)
- Optionally narrows scope with `--keys "W,A,S,D"` for the start command
- [critical] Reports response using ONLY documented fields (`Success`, `Message`, `Action`, `OutputPath`, `TotalFrames`, `DurationSeconds`)
- Notes that PlayMode + New Input System are prerequisites

## Scenario B
- [critical] Uses `--output-path scripts/regression-bug-42.json` on the `Stop` call (not `Start`)
- Reports `OutputPath` from the Stop response

## Scenario H (hold-out)
- [critical] Calls `uloop record-input --action Stop` to flush any captured frames
- Reads `Success` and `Message` from the response, does NOT invent a "PlayMode left" status field
- Acknowledges the recorded frame count from `TotalFrames` (if Stop succeeds) without inventing replay-side fields
