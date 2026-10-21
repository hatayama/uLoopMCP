# record-video: output, numbers, extraction, and troubleshooting

## Output

Every action returns JSON containing:

- `Success`: Whether the request completed.
- `Message`: Human-readable status.
- `Action`: Echoes the executed action.
- `IsRecording`: Whether a recording is active after this call.
- `OutputPath`: Absolute path of the video file. Open this path; do not search the directory. A window recording uses the `window_` name prefix instead of `gameview_`.
- `Width` / `Height`: Encoded resolution after `--resolution-scale` and even rounding (0 when none).
- `FrameRate`: Output fps (0 when none).
- `Quality`: Bitrate preset in use.
- `EncodedFrameCount`: Frames written to the encoder.
- `SkippedFrameCount`: Frame slots that could not be captured (Game View closed or resized, or encoder refused a frame). The video keeps its timeline; skipped slots are simply missing.
- `ElapsedSeconds`: Seconds since start (frozen after stop).
- `StoppedBy`: Why the recording ended — `"cli"`, `"max-duration"`, `"play-mode-exit"`, `"window-closed"`, `"assembly-reload"`, or `"editor-quit"`. Omitted while recording.

A stopped recording (from `stop`, a one-shot, or `status`/`stop` after an auto-stop) also returns:

- `AnalysisPath`: `<stem>_frames.json` beside the video, with every captured frame's measurements (`Samples`) and the sheets' frame lists. Extraction reads it.
- `BurstSheetPath` / `BurstSheetColumns` / `BurstAnchor` / `BurstFrames`: The burst sheet (`<stem>_burst.png`, 4 columns, top-left first), up to 32 consecutive frames around the anchor: 16 before it and 16 after (15 after a largest-change anchor, which is itself a tile). Fewer when the anchor is within 16 frames of the start or the recording stops before the frames after it are filled. `BurstAnchor` is `"mark"` when a mark was made (a later mark replaces an earlier one), otherwise `"largest-change"`. Omitted when fewer than two frames were captured, or nothing changed and there was no mark.
- `TimelineSheetPath` / `TimelineSheetColumns` / `TimelineFrames`: The timeline sheet (`<stem>_timeline.png`, 5 columns), tiles half a second apart; past 40 tiles every other one is dropped and the spacing doubles, so it always covers the whole recording. Written whenever at least one frame was captured.
- `MarkSeconds`: The seconds of every mark accepted.
- `FrameChange`: The numbers below.

Each entry of `BurstFrames` and `TimelineFrames` is `{CapturedIndex, EncodedFrame, Seconds}`. `Seconds` is the capture clock, the same clock as `MarkSeconds`. `EncodedFrame` is the frame number in the video. The two clocks can drift by a frame or more, so match frames by `EncodedFrame`.

A one-shot (`--seconds`/`--trigger`) adds `OneShot`: `Seconds`, `TriggerCommand`, `TriggerResult` (`Command`, `Completed`, `Response` holding the trigger's own JSON, `Error`, `Explanation`), `MarkAccepted`, `FocusWindowNote` when focusing Unity failed, and `Warning` when the mark was refused or a pause point paused Play Mode. A failed trigger or a cancelled one-shot still stops the recording, but exits 1 with nothing on stdout: the error envelope on stderr (`RECORD_VIDEO_TRIGGER_FAILED` or `RECORD_VIDEO_CANCELLED`) carries the stop response, `OneShot` included, in `Error.Details.Recording`. `RECORD_VIDEO_STOP_FAILED` means the stop itself failed; its `Error.Details.Recording` holds what the stop returned; check `--action status`.

## Reading the numbers

The analysis shrinks each frame to tiles of at most 320 px wide and splits it into a grid of up to 64 columns. A cell "changed" when its mean difference is `CellChangedThreshold` (8 of 255) or more.

- `CapturedFrameCount`: Frames measured.
- `MeanVsPrevious`: Mean difference against the previous frame, over the whole recording.
- `LargestChange`: The frame with the most changed cells against the previous one (`CapturedIndex`, `EncodedFrame`, `Seconds`, `ChangedCellsVsPrevious`). Omitted when no frame changed in 2+ cells.
- `FramesChangedVsPrevious`: Frames with 2+ changed cells against the previous frame.
- `FramesChangedVsPreviousMatchingTwoBack`: Of those, frames that match the frame two before (at most 1 changed cell): the A-B-A pattern of a one-frame blink.
- `TopAlternatingCells`: Up to 3 cells where A-B-A happened most, with `Count`, the cell's `Column`/`Row`, and its rectangle `TopLeftX`/`TopLeftY`/`Width`/`Height` in video pixels from the top-left corner. With `--resolution-scale` below 1 these differ from Game View coordinates.

These are measurements, not verdicts:

- Do not conclude "it still flickers" from `FramesChangedVsPrevious` alone. Smooth pulsing, scrolling, or anything always moving keeps it high after the flicker is fixed. A blink shows as high `FramesChangedVsPreviousMatchingTwoBack` and a high `Count`; smooth change keeps both low.
  - Measured, 63- and 61-frame recordings of the same element: a one-frame blink gave 62 / 30 / 30 (FramesChangedVsPrevious / MatchingTwoBack / top Count); after fixing it to a 1 s pulse, 58 / 3 / 4.
- When something else on screen keeps moving, whole frames never match two back, but the per-cell `Count` still shows the blinking cell.
- An area smaller than one cell (1/64 of the width) does not register. Read the burst sheet for small elements.
- Record the broken state first and check the symptom shows. If it does not, the numbers cannot confirm the fix; say so.

## Cutting frames out of a finished recording

```bash
uloop record-video --video <OutputPath> --frame-at <seconds>
uloop record-video --video <OutputPath> --sheet-from <seconds> --sheet-to <seconds>
```

- Needs `ffmpeg` on PATH. It never contacts Unity, so it works while the Editor is closed or busy. Without ffmpeg it fails with `FFMPEG_NOT_FOUND`; the sheets and numbers written at stop are still beside the video.
- Seconds are on the capture clock: pass `MarkSeconds` or a tile's `Seconds`. Each maps to the frame showing the last capture at or before it, clamped to the recording.
- `--frame-at` writes `<stem>_frame_<ms>.png` at full size and returns `FramePath`.
- `--sheet-from`/`--sheet-to` writes `<stem>_sheet_<fromMs>_<toMs>.png` and returns `SheetPath`, `Columns` (5), and `TileWidth` (320). Every frame of the range up to 40; a longer range keeps every n-th frame.
- Both return `Frames` as `{EncodedFrame, CapturedIndex, Seconds}`. A frame the encoder repeated to keep pace reports the capture it repeats.
- Only recordings with `<stem>_frames.json` beside them can be cut. Extraction takes no other options. `FFMPEG_FAILED` carries the end of ffmpeg's output in `Details.Stderr`; a time past the end of the video also ends there.

## Troubleshooting

- `Success: false` with `Message` "A recording is already in progress" → run `stop` first, then `start` again.
- `Success: false` with "Play Mode view RenderTexture is not available" → open the Game View tab (`uloop focus-window`) and make sure a camera renders.
- `SkippedFrameCount` growing while `EncodedFrameCount` stays flat → the Game View is closed, hidden, or resized. Restore it; recording resumes without restarting.
- `EncodedFrameCount` far below `ElapsedSeconds × FrameRate` with few skips → the Editor is unfocused and throttling draws; run `uloop focus-window` before the next recording. The one-shot does this for you.
- `Success: false` with "Window '...' not found" → pick the right title from the `Open windows:` list in the message, or use `--match-mode prefix`.
- `StoppedBy` is not `"cli"` → the recording ended on its own; the file is still valid up to that point.
- A compile auto-stops and finalizes the recording (when Unity's Script Changes While Playing is `Recompile After Finished Playing`, the compile is refused instead and the recording continues). Exiting Play Mode stops a Game View recording; a `--window-name` recording keeps running.

## Notes

- Odd Game View sizes are rounded down to even for H.264 (for example 1286×723 → 1286×722; the last pixel row/column is dropped).
- The recording is wall-clock paced, so Play Mode pause records a still image, not a gap.
- A window recording survives Play Mode stopping, but entering Play Mode with domain reload enabled (Unity's default) ends it with `StoppedBy: "assembly-reload"`.
- Default output is H.264 `.mp4` on macOS/Windows and VP8 `.webm` on Linux. An explicit `.webm` path uses VP8 on every host.
- Default recordings under `.uloop/outputs/Videos/` keep only the newest 20 files per extension; a custom `--output-path` is never pruned. The sheets and `frames.json` of a pruned video are removed with it.
