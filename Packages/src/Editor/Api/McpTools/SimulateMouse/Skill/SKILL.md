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
| `--drag-steps` | number | `5` | Intermediate steps for Drag action (each step = 1 frame) |

## Global Options

| Option | Description |
|--------|-------------|
| `--project-path <path>` | Target a specific Unity project (mutually exclusive with `--port`) |
| `-p, --port <port>` | Specify Unity TCP port directly (mutually exclusive with `--project-path`) |

## Actions

| Action | Event Fired | Description |
|--------|-------------|-------------|
| `Click` | PointerDown → PointerUp → PointerClick | Left click at (x, y) |
| `Drag` | BeginDrag → Drag×N → EndDrag | One-shot drag from (x, y) to (endX, endY), spread across frames |
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

## Notes

- All positions use screen pixel coordinates with bottom-left as origin (0, 0)
- Click and DragStart use `EventSystem.RaycastAll()` to find the UI element at the given position
- One-shot Drag spreads events across multiple frames (1 frame per step) for correct UI updates
- DragStart/DragMove/DragEnd maintain state between calls for hold-and-inspect workflows
- Use `find-game-objects` or `get-hierarchy` to discover scene objects, and `screenshot` to see their positions
