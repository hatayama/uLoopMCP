# Record Video Returns Frame Evidence an Agent Can Read

Date: 2026-10-21

## Decision

Every stopped `uloop record-video` recording returns, beside the video file, evidence an agent
can read without watching the video:

- a burst sheet: up to 32 consecutive frames around the moment of interest, anchored on a mark when one
  was made, otherwise on the frame with the largest change; omitted when fewer than two frames
  were captured, or when nothing changed and nothing was marked, because one frame or an
  unanchored still clip shows nothing a screenshot does not;
- a timeline sheet: tiles evenly spaced across the whole recording, at most 40; written whenever
  at least one frame was captured;
- frame change numbers: per-frame measurements in a `frames.json` sidecar and a summary in the
  response (changed frames, frames matching the one two before, and the cells where that
  alternation happened most).

The frames are measured while recording, on CPU copies of the frames already captured for the
encoder, so the evidence is complete the moment the recording stops.

A recording around one action is one command: `--seconds N --trigger "<uloop command>"` focuses
Unity, starts, runs the trigger, marks the moment the trigger returns, waits, and stops. The mark
is what anchors the burst sheet on the action instead of on whatever changed most.

There are two kinds of trigger:

- input: `simulate-keyboard`, `simulate-mouse-input`, `simulate-mouse-ui`, `replay-input`;
- code reaching a line: `enable-pause-point --mode trace --await`. Trace mode does not pause Play
  Mode, so the frames after the hit keep moving; pausing modes are rejected as triggers.

ffmpeg is an optional dependency used only to cut frames out of a finished recording afterwards
(`--video` with `--frame-at` or `--sheet-from`/`--sheet-to`). That path never contacts Unity, so it
works while the Editor is closed. Times are read on the capture clock that the responses report and
mapped to video frames through the sidecar's samples, because the video's own clock (frame number /
frame rate) drifts from it by a frame or more.

## Context

An agent cannot watch a video. Before this decision `record-video` returned only a file path, and
the skill suggested extracting stills with `ffmpeg -vf fps=1`, which samples one frame a second and
misses exactly what a recording is for: flicker, one-frame glitches, stutter, and effect order. An
agent asked to confirm such a fix either took a screenshot, which is one frame, or reported the
video path and could not judge it.

Turning the recording into images and numbers at stop time makes the evidence the default output
rather than an extra step an agent has to know about. Anchoring on a mark matters because the
largest change in a recording is often not the action under test (a scene transition, a moving
background).

## Measurements

- Cost per captured frame (1280×720, 30 fps, 10 s, Debug code optimization): 0.546 to 0.592 ms on
  average across three runs, with occasional spikes of 0.9 to 2.7 ms from main-thread scheduling
  and 0 to 1 gen-0 collections per run. That is under 2% of a 33 ms frame.
- Mark against effect: with a key press as trigger the mark landed at 0.195 s, with a trace pause
  point at 3.806 s; in both the effect was first visible two frames before the mark, inside the 16
  frames the burst keeps before it.
- Flicker numbers: a one-frame blink gave 62 / 30 / 30 (FramesChangedVsPrevious /
  FramesChangedVsPreviousMatchingTwoBack / top alternating cell count); the same element fixed to a
  1 s pulse gave 58 / 3 / 4. The changed-frame count alone does not tell the two apart; the
  alternation counts do.

## Considered Options

- Leave extraction to the agent with ffmpeg: rejected as the default. It needs ffmpeg installed,
  the agent has to choose frames without knowing where the change is, and fixed-rate sampling
  misses one-frame events. ffmpeg stays for looking closer after the evidence points somewhere.
- Analyse the video file after it is finalized: rejected. It needs a decoder in the Editor or the
  CLI, and the frames are already in memory while recording.
- Return verdicts ("flicker detected"): rejected. Thresholds that decide for every game would be
  wrong for many; the response carries measurements, and the skill explains how to read them.

## Consequences

- An agent reads the sheets with its image reader and the numbers from JSON; the skill leads with
  the one-shot and how to read both.
- Every recording pays the per-frame measurement cost above, and leaves two PNGs and a JSON beside
  the video; they are pruned with the video.
- Only recordings made by this version can be re-cut, because extraction needs the sidecar.
- An area smaller than one grid cell (1/64 of the frame width at most resolutions) does not register in the numbers;
  the burst sheet is the evidence for small elements.

## Revisit When

- (a) A configuration seen in real use costs more than 1.0 ms per captured frame on average.
- (b) Agents can read video files directly.
- (c) A trigger seen in real use produces its effect outside the 16 frames before and after the
  mark.
