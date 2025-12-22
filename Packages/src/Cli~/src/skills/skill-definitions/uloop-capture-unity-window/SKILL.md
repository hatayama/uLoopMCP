---
name: uloop-capture-unity-window
description: Capture Unity Editor windows as PNG via uloop CLI. Use when you need to: (1) Take a screenshot of Game View or Scene View, (2) Capture visual state for debugging or verification, (3) Capture Prefab edit mode view with UI support, (4) Save editor output as an image file.
---

# uloop capture-unity-window

Capture Unity Editor windows (Game View, Scene View, or Prefab edit mode) and save as PNG image.

## Usage

```bash
uloop capture-unity-window [--target <target>] [--resolution-scale <scale>]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--target` | enum | `GameView` | Target window: `GameView` (0) or `SceneView` (1) |
| `--resolution-scale` | number | `1.0` | Resolution scale (0.1 to 1.0) |

## Target Options

- **GameView (0)**: Captures the Game View window
- **SceneView (1)**: Captures the Scene View window. Auto-detects Prefab edit mode and handles accordingly

## Examples

```bash
# Capture Game View at full resolution
uloop capture-unity-window

# Capture Game View at half resolution
uloop capture-unity-window --target GameView --resolution-scale 0.5

# Capture Scene View
uloop capture-unity-window --target SceneView

# Capture Scene View (will capture Prefab if in Prefab edit mode)
uloop capture-unity-window --target SceneView --resolution-scale 0.8
```

## Output

Returns JSON with:
- `ImagePath`: Absolute path to the saved PNG image
- `FileSizeBytes`: Size of the saved file in bytes
- `Width`: Captured image width in pixels
- `Height`: Captured image height in pixels

## Features

- **Screen Space Overlay Canvas support**: UI canvases are temporarily converted to World Space for proper capture
- **Prefab edit mode detection**: Automatically detects and properly captures Prefab view when in edit mode
- **3D Prefab lighting**: Adds temporary directional light for 3D model prefabs
- **Aspect ratio preservation**: Uses Scene View camera pixel rect for accurate capture

## Notes

- Use `uloop focus-window` first if needed
- Target window must be visible in Unity Editor
- For Prefab captures, open the prefab in edit mode before capturing with `--target SceneView`
