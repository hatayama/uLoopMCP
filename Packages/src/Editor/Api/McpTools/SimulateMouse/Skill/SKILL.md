---
name: uloop-simulate-mouse
description: "Simulate mouse click and drag on PlayMode UI elements via screen coordinates. Use when you need to: (1) Click buttons or interactive UI elements during PlayMode testing, (2) Drag UI elements from one position to another, (3) Hold a drag at a position for inspection before releasing."
---

# uloop simulate-mouse

Simulate mouse click and drag operations on PlayMode game UI elements via Unity EventSystem.

## Usage

```bash
uloop simulate-mouse --action <action> --x <x> --y <y> [options]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--action` | enum | `Click` | `Click`, `Drag`, `DragStart`, `DragMove`, `DragEnd` |
| `--x` | number | `0` | X position in screen pixels (origin: bottom-left) |
| `--y` | number | `0` | Y position in screen pixels (origin: bottom-left) |
| `--end-x` | number | `0` | End X position for Drag action |
| `--end-y` | number | `0` | End Y position for Drag action |
| `--drag-speed` | number | `2000` | Drag speed in pixels per second (0 for instant). Applies to Drag action only. |

## Global Options

| Option | Description |
|--------|-------------|
| `--project-path <path>` | Target a specific Unity project (mutually exclusive with `--port`) |
| `-p, --port <port>` | Specify Unity TCP port directly (mutually exclusive with `--project-path`) |

## Actions

| Action | Event Fired | Description |
|--------|-------------|-------------|
| `Click` | PointerDown → PointerUp → PointerClick | Left click at (x, y) |
| `Drag` | BeginDrag → Drag×N → EndDrag | One-shot drag from (x, y) to (endX, endY) at the specified speed |
| `DragStart` | BeginDrag | Begin drag at (x, y) and hold |
| `DragMove` | Drag | Move while holding drag to (x, y) |
| `DragEnd` | EndDrag | Release drag at (x, y) |

## Prerequisites

- Unity must be in **PlayMode** (use `uloop control-play-mode --action Play` first)
- Target scene must have an **EventSystem** GameObject
- UI elements must have a **GraphicRaycaster** on their Canvas

## Examples

```bash
# Click a button at screen position
uloop simulate-mouse --action Click --x 400 --y 300

# One-shot drag (start to end in one call)
uloop simulate-mouse --action Drag --x 400 --y 300 --end-x 600 --end-y 300

# Slow drag for visual inspection
uloop simulate-mouse --action Drag --x 400 --y 300 --end-x 600 --end-y 300 --drag-speed 200

# Split drag with hold (for inspection between steps)
uloop simulate-mouse --action DragStart --x 400 --y 300
uloop screenshot --window-name Game
uloop simulate-mouse --action DragMove --x 500 --y 300
uloop simulate-mouse --action DragEnd --x 600 --y 300
```

## Output

Returns JSON with:
- `Success`: Whether the operation succeeded
- `Message`: Description of what happened
- `Action`: The action that was executed
- `HitGameObjectName`: Name of the UI element found via raycast (null if none)
- `PositionX/Y`: The position used
- `EndPositionX/Y`: The end position (Drag action only)

## Workflow

This tool operates entirely on screen pixel coordinates — no need to look up GameObject names or positions. The typical workflow is:

1. `uloop screenshot --window-name Game` to capture the current Game View
2. Read the screenshot to visually identify target positions (coordinates use bottom-left origin)
3. `uloop simulate-mouse --action Click --x <x> --y <y>` to interact

## Notes

- All positions use screen pixel coordinates with bottom-left as origin (0, 0)
- Click and DragStart use `EventSystem.RaycastAll()` to find the UI element at the given position
- One-shot Drag interpolates position every frame at the specified speed (pixels/sec), so short and long drags move at the same visual speed
- DragStart/DragMove/DragEnd maintain state between calls for hold-and-inspect workflows
