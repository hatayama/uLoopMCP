---
name: uloop-replay-input
description: "Replay recorded input during PlayMode with frame-precise injection. Use when you need to: (1) Reproduce recorded gameplay exactly, (2) Run E2E tests from recorded input, (3) Generate demo videos with consistent input. Routes through uloop CLI to Unity, which deserializes the JSON recording and pushes the captured Input System device states back into Mouse.current / Keyboard.current frame-by-frame in PlayMode; requires PlayMode and the New Input System."
---

# uloop replay-input

Replay recorded keyboard and mouse input during PlayMode. Loads a JSON recording and injects input frame-by-frame via Input System with zero CLI overhead. Supports looping and progress monitoring.

## Usage

```bash
# Start replay (auto-detect latest recording)
uloop replay-input --action Start

# Start replay with specific file
uloop replay-input --action Start --input-path scripts/my-play.json

# Start replay with looping
uloop replay-input --action Start --loop true

# Check replay progress
uloop replay-input --action Status

# Stop replay
uloop replay-input --action Stop
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--action` | enum | `Start` | `Start`, `Stop`, `Status` |
| `--input-path` | string | auto | JSON path. Auto-detects latest in `.uloop/outputs/InputRecordings/` |
| `--show-overlay` | boolean | `true` | Show replay progress overlay |
| `--loop` | boolean | `false` | Loop continuously |

## Deterministic Replay

Replay injects the exact same input frame-by-frame, but the game must also be deterministic to produce identical results. See the **"Design Guidelines for Deterministic Replay"** section in the `record-input` skill for the full list of patterns to avoid (`Time.deltaTime`, `Random.Range`, physics, etc.) and their deterministic alternatives.

## Output

Returns JSON with:
- `Success`: Whether the operation succeeded
- `Message`: Status message
- `Action`: Echoes which action was executed (`Start`, `Stop`, or `Status`)
- `InputPath`: Path to recording file (nullable string; populated on `Start` only)
- `CurrentFrame`: Current replay frame index (nullable int)
- `TotalFrames`: Total frames in the recording (nullable int)
- `Progress`: Replay progress (nullable float in 0.0 – 1.0)
- `IsReplaying`: Whether replay is currently active (nullable bool)

These are the only eight fields. There is no `LoopCount`, `ElapsedSeconds`, `OverlayVisible`, or per-frame inspection data in the response.
