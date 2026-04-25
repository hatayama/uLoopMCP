---
name: uloop-control-play-mode
description: "Control Unity Editor play mode (play/stop/pause). Use when you need to: (1) Start play mode to test game behavior, (2) Stop play mode to return to edit mode, (3) Pause play mode for frame-by-frame inspection. Executes via `uloop control-play-mode` CLI invocation; returns the resulting IsPlaying / IsPaused state as JSON."
---

# uloop control-play-mode

Control Unity Editor play mode (play/stop/pause).

## Usage

```bash
uloop control-play-mode [options]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--action` | string | `Play` | Action to perform: `Play`, `Stop`, `Pause` |

## Global Options

| Option | Description |
|--------|-------------|
| `--project-path <path>` | Optional. Use only when the target Unity project is not the current directory. |

## Examples

```bash
# Start play mode
uloop control-play-mode --action Play

# Stop play mode
uloop control-play-mode --action Stop

# Pause play mode
uloop control-play-mode --action Pause
```

## Output

Returns JSON with the current play mode state:
- `IsPlaying`: Whether Unity is currently in play mode
- `IsPaused`: Whether play mode is paused
- `Message`: Description of the action performed

## Notes

- Play action starts the game in the Unity Editor (also resumes from pause)
- Stop action exits play mode and returns to edit mode
- Pause action pauses the game while remaining in play mode
- Useful for automated testing workflows

### Asynchronous PlayMode entry

The command returns as soon as the action is dispatched. PlayMode entry itself is asynchronous — Unity transitions on the next editor frame, not before this command returns. The `IsPlaying` value in the response reflects the state *at the moment the response was built* and may still be `false` for a `Play` request even though the request was accepted.

Before invoking PlayMode-dependent commands (`uloop simulate-mouse-input`, `uloop simulate-mouse-ui`, `uloop simulate-keyboard`, `uloop record-input`, `uloop replay-input`), wait for `IsPlaying: true` by re-issuing `uloop control-play-mode --action Play` (it is idempotent) until the response shows `IsPlaying: true`, or insert a brief delay. Those PlayMode-dependent skills also self-check and return a "PlayMode is not active" error if invoked too early — treat that error as "retry after a short wait", not as a permanent failure.
