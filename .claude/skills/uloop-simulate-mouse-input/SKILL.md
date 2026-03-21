---
name: uloop-simulate-mouse-input
description: "Simulate low-level mouse input in PlayMode via Input System. Inject button clicks, mouse delta movement, and scroll wheel directly into Mouse.current for game logic that reads Input System (e.g. WasPressedThisFrame, Mouse.current.delta). Use when you need to: (1) Click or long-press for game logic that reads Input System directly (not UI/EventSystem), (2) Inject mouse delta for camera rotation or FPS-style look controls, (3) Simulate scroll wheel input for zoom or weapon switching, (4) Smoothly move the mouse over a duration for continuous camera panning."
context: fork
---

# Task

Simulate low-level mouse input on Unity PlayMode: $ARGUMENTS

## When to use this vs simulate-mouse-ui

| Tool | Target | Use case |
|------|--------|----------|
| `simulate-mouse-input` | Input System (`Mouse.current`) | Game logic: FPS camera, shooting, scroll-to-zoom, any code reading `Mouse.current.delta`, `WasPressedThisFrame()`, etc. |
| `simulate-mouse-ui` | EventSystem (uGUI) | UI interaction: clicking buttons, dragging sliders, drop targets — anything with `GraphicRaycaster` |

## Workflow

1. Ensure Unity is in PlayMode (use `uloop control-play-mode --action Play` if not)
2. Execute the appropriate `uloop simulate-mouse-input` command
3. Take a screenshot to verify the result if needed: `uloop screenshot --capture-mode rendering`
4. Report what happened

## Tool Reference

```bash
uloop simulate-mouse-input --action <action> [options]
```

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--action` | enum | `Click` | `Click`, `LongPress`, `MoveDelta`, `Scroll`, `SmoothDelta` |
| `--x` | number | `0` | Target X position in screen pixels (origin: top-left). Used by Click and LongPress to set mouse position. |
| `--y` | number | `0` | Target Y position in screen pixels (origin: top-left). Used by Click and LongPress to set mouse position. |
| `--button` | enum | `Left` | Mouse button: `Left`, `Right`, `Middle`. Used by Click and LongPress. |
| `--duration` | number | `0` | Hold duration in seconds. For LongPress: hold time. For Click: minimum hold (0 = instant tap). For SmoothDelta: total animation duration. |
| `--delta-x` | number | `0` | Delta X in pixels for MoveDelta/SmoothDelta. Positive = right. |
| `--delta-y` | number | `0` | Delta Y in pixels for MoveDelta/SmoothDelta. Positive = up (Unity screen space). |
| `--scroll-x` | number | `0` | Horizontal scroll delta for Scroll action. |
| `--scroll-y` | number | `0` | Vertical scroll delta for Scroll action. Positive = up, negative = down. Typically 120 per notch. |

### Actions

| Action | Description |
|--------|-------------|
| `Click` | Set mouse position to (x, y), then inject button press + release. If `--duration` > 0, holds the button for that duration. |
| `LongPress` | Set mouse position to (x, y), then hold button for `--duration` seconds, then release. |
| `MoveDelta` | Inject a one-shot mouse delta (delta-x, delta-y) into `Mouse.current.delta`. For FPS camera look, weapon sway, etc. |
| `SmoothDelta` | Inject mouse delta smoothly spread over `--duration` seconds. For gradual camera panning. |
| `Scroll` | Inject scroll wheel input (scroll-x, scroll-y). 120 per notch is the standard increment. |

### Global Options

| Option | Description |
|--------|-------------|
| `--project-path <path>` | Target a specific Unity project (mutually exclusive with `--port`) |
| `-p, --port <port>` | Specify Unity TCP port directly (mutually exclusive with `--project-path`) |

## Examples

```bash
# Left click at screen position (for game logic, not UI)
uloop simulate-mouse-input --action Click --x 400 --y 300

# Right click (e.g. aim-down-sights)
uloop simulate-mouse-input --action Click --x 400 --y 300 --button Right

# Long-press left button for 2 seconds
uloop simulate-mouse-input --action LongPress --x 400 --y 300 --duration 2.0

# FPS camera: look right 200px, up 50px (one-shot)
uloop simulate-mouse-input --action MoveDelta --delta-x 200 --delta-y 50

# Smooth camera pan over 1 second
uloop simulate-mouse-input --action SmoothDelta --delta-x 300 --delta-y 0 --duration 1.0

# Scroll up (e.g. zoom in or next weapon)
uloop simulate-mouse-input --action Scroll --scroll-y 120

# Scroll down 3 notches
uloop simulate-mouse-input --action Scroll --scroll-y -360
```

## Prerequisites

- Unity must be in **PlayMode** (not paused)
- The **Input System** package (`com.unity.inputsystem`) must be installed
- Active Input Handling must be set to "Input System Package (New)" or "Both" in Player Settings
