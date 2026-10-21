---
name: uloop-record-video
toolName: record-video
description: "Record the Game View or any Editor window to .mp4/.webm and get back what you can read without watching it: a sheet of up to 32 consecutive frames around the trigger (or around the biggest change when there is no trigger), a timeline sheet, and per-frame change numbers. Use for flicker, stutter, one-frame glitches, fades, cut-ins, transitions, effect order, or anything one screenshot cannot show. One shot: --seconds N --trigger '<uloop command>'."
---

# Task

Record the Game View during Play Mode, or any Editor window with `--window-name`, and read what happened from PNG sheets and numbers instead of the video.

## Workflow

1. A Game View recording needs Play Mode (`uloop control-play-mode --action Play`); `--window-name` does not.
2. `uloop record-video --seconds 3 --trigger "simulate-keyboard --action Press --key Space"` (the trigger is a uloop command without the leading `uloop`): focuses Unity, starts, runs the trigger, marks the moment it returns, waits, stops, and prints one JSON.
3. Read the PNG at `BurstSheetPath`: up to 32 consecutive frames, 4 columns, top-left first. `BurstFrames` gives each tile's `EncodedFrame` and `Seconds`; `BurstAnchor: "mark"` centers it on the trigger, `"largest-change"` on the biggest change.
4. Read the PNG at `TimelineSheetPath`: the whole recording at even spacing, 5 columns.
5. Read `FrameChange` (below).
6. To look closer, with ffmpeg installed (works with Unity closed): `uloop record-video --video <OutputPath> --sheet-from <s> --sheet-to <s>`, or `--frame-at <s>` for one full-size frame. Pass the `Seconds` the sheets report.

## Choosing the trigger

- Input: `simulate-keyboard`, `simulate-mouse-input`, `simulate-mouse-ui`, `replay-input`.
- Code reaching a line (an AI branch, a coroutine stage): `enable-pause-point --file <f> --line <n> --mode trace --await`. Trace does not pause Play Mode; other modes are rejected. For long waits raise `--seconds` and `--max-duration-seconds`.

## Reading the numbers

- `FramesChangedVsPrevious` counts frames where 2+ grid cells (1/64 of the width) changed by `CellChangedThreshold` or more. Smaller areas do not register; look at the burst sheet.
- Judge flicker by `FramesChangedVsPreviousMatchingTwoBack` and `TopAlternatingCells[].Count` (rectangles in video pixels, top-left origin), not `FramesChangedVsPrevious` alone: smooth motion keeps that high after a fix. Measured (FramesChangedVsPrevious / MatchingTwoBack / Count): a one-frame blink 62 / 30 / 30; the same element fixed to a 1 s pulse 58 / 3 / 4.

## Before claiming a fix

Record the broken state first and check the symptom shows in the sheets or numbers, then record the fix. If the broken state does not show it, report the fix as unverified.

## Tool Reference

```bash
uloop record-video --seconds <1-600> [--trigger "<uloop command>"] [start options]
uloop record-video --action start [--frame-rate <fps>] [--max-duration-seconds <sec>] [--resolution-scale <0.1-1.0>] [--quality <low|medium|high>] [--window-name <name>] [--match-mode <exact|prefix|contains>] [--output-path <file>]
uloop record-video --action mark|status|stop
uloop record-video --video <file> --frame-at <s> | --sheet-from <s> --sheet-to <s>
```

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--action` | enum | `start` | `start` - begin recording, `stop` - finalize the file, `status` - report progress, `mark` - tell the running recording that the moment you want to see is now; the burst sheet is built around it |
| `--frame-rate` | integer | `30` | Output video fps. Valid range 1–60. Frames are paced by wall-clock time; when the Editor renders slower than this, the previous frame is repeated. Used by `start` only. |
| `--max-duration-seconds` | integer | `60` | Auto-stop safety limit in seconds. Valid range 1–600. Used by `start` only. |
| `--resolution-scale` | number | `1.0` | Resolution scale (0.1 to 1.0) applied to the Game View size before encoding. `0.5` cuts file size and encoding cost to about a quarter. Used by `start` only. |
| `--quality` | enum | `medium` | Encoder bitrate preset: `low`, `medium`, or `high`. Used by `start` only. |
| `--window-name` | string | empty | Editor window title to record instead of the Game View (for example Scene, Inspector, Console). Empty records the Game View and requires Play Mode. A window recording does not require Play Mode, brings the tab to the front, repaints it every frame, and keeps running when Play Mode stops. Used by `start` only. |
| `--match-mode` | enum | `exact` | Window title matching for `--window-name`: `exact`, `prefix`, or `contains` (case-insensitive). Used by `start` only. |
| `--output-path` | string | empty | Output file path. Empty uses `.uloop/outputs/Videos/gameview_<yyyyMMdd_HHmmss_fff>.mp4` (`.webm` on Linux). Extension must be `.mp4` (H.264) or `.webm` (VP8). Linux rejects `.mp4`. Used by `start` only. |
| `--seconds` | integer | - | CLI-only. Record one clip in a single command: start, the trigger, a mark, wait this long, stop. 1–600, at most `--max-duration-seconds` (default 60); raise both for a longer clip. |
| `--trigger` | string | - | CLI-only. uloop command run after start, then marked. Implies `--seconds 3`. |
| `--video` | string | - | CLI-only. Recorded video to cut frames from (`OutputPath`). Needs ffmpeg; does not contact Unity. |
| `--frame-at` | number | - | CLI-only. Write the full-size frame at these seconds beside the video. |
| `--sheet-from` | number | - | CLI-only. Start of a range to write as a sheet of up to 40 frames. Requires `--sheet-to`. |
| `--sheet-to` | number | - | CLI-only. End of the `--sheet-from` range. |

### Actions

| Action | Behavior | Typical use |
|--------|----------|-------------|
| `start` | Validates, opens the encoder, returns. One recording at a time; a second `start` fails until `stop`. | Manual capture across several commands |
| `mark` | Anchors the burst sheet on now. | After the action, before `stop` |
| `status` | Reports live counters. When idle, reports the most recent auto-stopped recording. | Check progress or why a recording ended |
| `stop` | Finalizes the file and returns the sheets and numbers. When nothing is recording, returns the last auto-stopped recording once. | End capture |

Do not run `uloop compile` while recording: it auto-stops the recording. Use exactly `OutputPath`; the folder holds older recordings.

Output fields, error messages, extraction details, and worked flicker numbers: [references/output-and-troubleshooting.md](references/output-and-troubleshooting.md).
