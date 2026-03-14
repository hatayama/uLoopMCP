---
name: uloop-simulate-mouse
description: "Simulate mouse click and drag on PlayMode UI elements via screen coordinates. Use when you need to: (1) Click buttons or interactive UI elements during PlayMode testing, (2) Drag UI elements from one position to another, (3) Hold a drag at a position for inspection before releasing."
context: fork
---

# Task

Simulate mouse interaction on Unity PlayMode UI: $ARGUMENTS

## Workflow

1. Ensure Unity is in PlayMode (use `uloop control-play-mode --action Play` if not)
2. Get UI element info: `uloop screenshot --capture-mode rendering --annotate-elements --elements-only`
3. Use the `AnnotatedElements` array to find the target element by `Name` or `Label` (A=frontmost, B=next, ...). Use `SimX`/`SimY` directly as `--x`/`--y` coordinates.
4. Execute the appropriate `uloop simulate-mouse` command
5. Take a screenshot to verify the result: `uloop screenshot --capture-mode rendering --annotate-elements`
6. Report what happened

## Tool Reference

```bash
uloop simulate-mouse --action <action> --x <x> --y <y> [options]
```

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--action` | enum | `Click` | `Click`, `Drag`, `DragStart`, `DragMove`, `DragEnd` |
| `--x` | number | `0` | X position in screen pixels (origin: top-left) |
| `--y` | number | `0` | Y position in screen pixels (origin: top-left) |
| `--end-x` | number | `0` | End X position for Drag action |
| `--end-y` | number | `0` | End Y position for Drag action |
| `--drag-speed` | number | `2000` | Drag speed in pixels per second (0 for instant). 2000 is fast (default), 200 is slow enough to watch. Applies to Drag, DragMove, and DragEnd actions. |

### Actions

| Action | Event Fired | Description |
|--------|-------------|-------------|
| `Click` | PointerDown → PointerUp → PointerClick | Left click at (x, y) |
| `Drag` | BeginDrag → Drag×N → EndDrag | One-shot drag from (x, y) to (endX, endY) at the specified speed |
| `DragStart` | BeginDrag | Begin drag at (x, y) and hold |
| `DragMove` | Drag×N | Animate from current position to (x, y) at the specified speed |
| `DragEnd` | Drag×N → EndDrag | Animate to (x, y) at the specified speed, then release drag |

### Split Drag Rules

- `DragStart` must be called before `DragMove` or `DragEnd`
- `DragEnd` must be called to release an active drag — failing to call it leaves drag state stuck
- Calling `DragMove` or `DragEnd` without an active drag returns an error

### Global Options

| Option | Description |
|--------|-------------|
| `--project-path <path>` | Target a specific Unity project (mutually exclusive with `--port`) |
| `-p, --port <port>` | Specify Unity TCP port directly (mutually exclusive with `--project-path`) |

## Coordinate System

- Origin is **top-left** (0, 0)
- All positions are in **screen pixels**
- Get coordinates from `AnnotatedElements` JSON (`SimX`/`SimY`) — do NOT look up GameObject positions
- Clicking or dragging on empty space (no UI element) still succeeds with a message indicating no element was hit

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

## Prerequisites

- Unity must be in **PlayMode**
- Target scene must have an **EventSystem** GameObject
- UI elements must have a **GraphicRaycaster** on their Canvas
