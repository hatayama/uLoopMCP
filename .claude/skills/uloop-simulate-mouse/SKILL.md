---
name: uloop-simulate-mouse
description: "Simulate mouse click, long-press, and drag on PlayMode UI elements via screen coordinates. Use when you need to: (1) Click buttons or interactive UI elements during PlayMode testing, (2) Drag UI elements from one position to another, (3) Hold a drag at a position for inspection before releasing, (4) Long-press UI elements that respond to sustained pointer-down."
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
| `--action` | enum | `Click` | `Click`, `Drag`, `DragStart`, `DragMove`, `DragEnd`, `LongPress` |
| `--x` | number | `0` | Target X position in screen pixels (origin: top-left). For Drag action, this is the destination. |
| `--y` | number | `0` | Target Y position in screen pixels (origin: top-left). For Drag action, this is the destination. |
| `--from-x` | number | `0` | Start X position for Drag action. Drag starts here and moves to x,y. |
| `--from-y` | number | `0` | Start Y position for Drag action. Drag starts here and moves to x,y. |
| `--drag-speed` | number | `2000` | Drag speed in pixels per second (0 for instant). 2000 is fast (default), 200 is slow enough to watch. Applies to Drag, DragMove, and DragEnd actions. |
| `--duration` | number | `0.5` | Hold duration in seconds for LongPress action. |

### Actions

| Action | Event Fired | Description |
|--------|-------------|-------------|
| `Click` | PointerDown → PointerUp → PointerClick | Left click at (x, y) |
| `LongPress` | PointerDown → (hold) → PointerUp | Press and hold at (x, y) for `--duration` seconds, then release. No PointerClick is fired. |
| `Drag` | BeginDrag → Drag×N → EndDrag | One-shot drag from (fromX, fromY) to (x, y) at the specified speed |
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
- Clicking or long-pressing on empty space (no UI element) still succeeds with a message indicating no element was hit
- Dragging on empty space (no draggable UI element) returns `Success = false`

## Examples

```bash
# Click a button at screen position
uloop simulate-mouse --action Click --x 400 --y 300

# Long-press a button for 3 seconds
uloop simulate-mouse --action LongPress --x 400 --y 300 --duration 3.0

# One-shot drag (start to end in one call)
uloop simulate-mouse --action Drag --from-x 400 --from-y 300 --x 600 --y 300

# Slow drag for visual inspection
uloop simulate-mouse --action Drag --from-x 400 --from-y 300 --x 600 --y 300 --drag-speed 200

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
