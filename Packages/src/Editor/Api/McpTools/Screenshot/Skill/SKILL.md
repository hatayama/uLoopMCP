---
name: uloop-screenshot
description: "Take a screenshot of Unity Editor windows and save as PNG. Use when you need to: (1) Screenshot the Game View, Scene View, Console, Inspector, or other windows, (2) Capture current visual state for debugging or documentation, (3) Save what the Editor looks like as an image file."
---

# uloop screenshot

Take a screenshot of any Unity EditorWindow by name and save as PNG.

## Usage

```bash
uloop screenshot [--window-name <name>] [--resolution-scale <scale>] [--match-mode <mode>]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--window-name` | string | `Game` | Window name to capture (e.g., "Game", "Scene", "Console", "Inspector", "Project", "Hierarchy", or any EditorWindow title) |
| `--resolution-scale` | number | `1.0` | Resolution scale (0.1 to 1.0) |
| `--match-mode` | enum | `exact` | Window name matching mode: `exact`, `prefix`, or `contains`. All modes are case-insensitive. |

## Match Modes

| Mode | Description | Example |
|------|-------------|---------|
| `exact` | Window name must match exactly (case-insensitive) | "Project" matches "Project" only |
| `prefix` | Window name must start with the input | "Project" matches "Project" and "Project Settings" |
| `contains` | Window name must contain the input anywhere | "set" matches "Project Settings" |

## Window Name

The window name is the text displayed in the window's title bar (tab). The user (human) will tell you which window to capture. Common window names include:

- **Game**: Game View window
- **Scene**: Scene View window
- **Console**: Console window
- **Inspector**: Inspector window
- **Project**: Project browser window
- **Hierarchy**: Hierarchy window
- **Animation**: Animation window
- **Animator**: Animator window
- **Profiler**: Profiler window
- **Audio Mixer**: Audio Mixer window

You can also specify custom EditorWindow titles (e.g., "EditorWindow Capture Test").

## Global Options

| Option | Description |
|--------|-------------|
| `--project-path <path>` | Target a specific Unity project (mutually exclusive with `--port`). Path resolution follows the same rules as `cd` — absolute paths are used as-is, relative paths are resolved from cwd. |
| `-p, --port <port>` | Specify Unity TCP port directly (mutually exclusive with `--project-path`). |

## Examples

```bash
# Take a screenshot of Game View at full resolution
uloop screenshot

# Take a screenshot of Game View at half resolution
uloop screenshot --window-name Game --resolution-scale 0.5

# Take a screenshot of Scene View
uloop screenshot --window-name Scene

# Take a screenshot of Console window
uloop screenshot --window-name Console

# Take a screenshot of Inspector window
uloop screenshot --window-name Inspector

# Take a screenshot of Project browser (exact match - won't match "Project Settings")
uloop screenshot --window-name Project

# Take a screenshot of all windows starting with "Project" (prefix match)
uloop screenshot --window-name Project --match-mode prefix

# Take a screenshot of custom EditorWindow by title
uloop screenshot --window-name "My Custom Window"
```

## Output

Returns JSON with:
- `ScreenshotCount`: Number of windows captured
- `Screenshots`: Array of screenshot info, each containing:
  - `ImagePath`: Absolute path to the saved PNG file
  - `FileSizeBytes`: Size of the saved file in bytes
  - `Width`: Captured image width in pixels
  - `Height`: Captured image height in pixels

When multiple windows match (e.g., multiple Inspector windows or when using `contains` mode), all matching windows are captured with numbered filenames (e.g., `Inspector_1_*.png`, `Inspector_2_*.png`).

## Notes

- Use `uloop focus-window` first if needed
- Target window must be open in Unity Editor
- Window name matching is always case-insensitive
