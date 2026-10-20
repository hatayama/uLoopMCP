---
name: uloop-game-view-mode
toolName: game-view-mode
description: "Show which view the Game tab is showing, or switch it between the Game view and the Device Simulator, rotating the simulated device to match the Game view's orientation. Use before Play when the UI must be checked in a device frame."
---

# uloop game-view-mode

Show which view the Game tab is showing, or switch it between the Game view and the Device Simulator, rotating the simulated device to match the Game view's orientation.

## Usage

```bash
uloop game-view-mode [--to <GameView|Simulator>]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--to` | string | unset | `Simulator` switches the current Game view tab to the Device Simulator in place and rotates the device to match the Game view (landscape or portrait) when Player Settings allow that orientation; `GameView` switches back. Omit to report the current view without changing it. |

## Output

Returns JSON containing:

- `Success`: Whether the request completed. `false` only when the Device Simulator is not available in this Editor.
- `PreviousView`: View before the request: `GameView`, `Simulator`, `None` (no Play Mode view open), or `Other` (a third-party Play Mode view).
- `View`: View after the request, same values.
- `Changed`: Whether the tab was swapped or a view was opened.
- `SimulatorOrientation`: Orientation the Simulator renders (`Portrait`, `PortraitUpsideDown`, `LandscapeLeft`, `LandscapeRight`). Present only when the current view is the Simulator.
- `Message`: Human-readable status.
- `Warning`: Present when the device was not rotated or the Simulator renders a different orientation than requested.

## Examples

```bash
# Report which view is showing
uloop game-view-mode

# Play in the Device Simulator, rotated like the Game view
uloop game-view-mode --to Simulator
uloop control-play-mode --action Play

# Back to the plain Game view
uloop game-view-mode --to GameView
```

## Notes

- The tab is replaced in place, exactly like the Game / Simulator dropdown in the Game view toolbar. No second window is opened, and the device selected in the Simulator is kept.
- Already showing the requested view: nothing changes and `Changed` is `false`.
- No Play Mode view open: a new window of the requested kind is opened; the Simulator keeps its own rotation because there is no Game view shape to copy.
- The orientation comes from the Game view's rendering resolution (wider than tall is landscape; square counts as portrait). A Free Aspect tab uses the tab's own shape, so set `set-game-view-size` first when the shape must be exact.
- When Player Settings allow no orientation on the wanted side (for example a portrait-only project and a landscape Game view), the device is not rotated and `Warning` says so. Fix the Default Orientation or the allowed auto-rotation flags in Player Settings if landscape is really wanted.
- Works in Edit Mode, Play Mode, and while paused; the play state is never changed. Start or stop playing with `control-play-mode` separately.
- Editors without the Device Simulator (Unity 2020 and older without the package) return `Success: false` and leave the view untouched.
