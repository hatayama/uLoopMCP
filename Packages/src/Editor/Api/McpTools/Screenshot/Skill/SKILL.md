---
name: uloop-screenshot
description: "Capture screenshots of Unity Editor windows as PNG files. Use when you need to: (1) Screenshot Game View, Scene View, Console, Inspector, or other windows, (2) Capture current visual state for debugging or documentation, (3) Save editor window appearance as image files."
---

# uloop screenshot

Take a screenshot of any Unity EditorWindow by name and save as PNG.

## Usage

```bash
uloop screenshot [--window-name <name>] [--resolution-scale <scale>] [--match-mode <mode>] [--output-directory <path>]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--window-name` | string | `Game` | Window name to capture (e.g., "Game", "Scene", "Console", "Inspector", "Project", "Hierarchy", or any EditorWindow title) |
| `--resolution-scale` | number | `1.0` | Resolution scale (0.1 to 1.0) |
| `--match-mode` | enum | `exact` | Window name matching mode: `exact`, `prefix`, or `contains`. All modes are case-insensitive. |
| `--output-directory` | string | `""` | Output directory path for saving screenshots. When empty, uses default path (.uloop/outputs/Screenshots/). Accepts absolute paths. |

## Match Modes

| Mode | Description | Example |
|------|-------------|---------|
| `exact` | Window name must match exactly (case-insensitive) | "Project" matches "Project" only |
| `prefix` | Window name must start with the input | "Project" matches "Project" and "Project Settings" |
| `contains` | Window name must contain the input anywhere | "set" matches "Project Settings" |

## Window Name

The window name is the text displayed in the window's title bar (tab). Common names: Game, Scene, Console, Inspector, Project, Hierarchy, Animation, Animator, Profiler. Custom EditorWindow titles are also supported.

## Global Options

| Option | Description |
|--------|-------------|
| `--project-path <path>` | Target a specific Unity project (mutually exclusive with `--port`) |
| `-p, --port <port>` | Specify Unity TCP port directly (mutually exclusive with `--project-path`) |

## Examples

```bash
# Take a screenshot of Game View (default)
uloop screenshot

# Take a screenshot of Scene View
uloop screenshot --window-name Scene

# Capture all windows starting with "Project" (prefix match)
uloop screenshot --window-name Project --match-mode prefix

# Save screenshot to a specific directory
uloop screenshot --output-directory /tmp/screenshots

# Combine options
uloop screenshot --window-name Scene --resolution-scale 0.5 --output-directory /tmp/screenshots
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
