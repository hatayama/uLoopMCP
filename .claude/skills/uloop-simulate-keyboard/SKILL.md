---
name: uloop-simulate-keyboard
description: "Simulate keyboard key input in PlayMode via Input System. Press, hold, and release keys (WASD, Space, Shift, etc.) for game controls. Use when you need to: (1) Press keys during PlayMode testing (e.g. WASD movement, Space to jump), (2) Hold a key down for a duration then release, (3) Manually control key-down and key-up separately for complex input sequences."
context: fork
---

# Task

Simulate keyboard input on Unity PlayMode: $ARGUMENTS

## Workflow

1. Ensure Unity is in PlayMode (use `uloop control-play-mode --action Play` if not)
2. Execute the appropriate `uloop simulate-keyboard` command
3. Take a screenshot to verify the result if needed: `uloop screenshot --capture-mode rendering`
4. Report what happened

## Tool Reference

```bash
uloop simulate-keyboard --key <key> [--action <action>] [--duration <seconds>]
```

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--action` | enum | `Press` | `Press` - one-shot key tap (Down then Up), `KeyDown` - hold key down, `KeyUp` - release held key |
| `--key` | string | `""` | Key name matching the Input System Key enum. Case-insensitive. Examples: `W`, `A`, `S`, `D`, `Space`, `LeftShift`, `Enter`, `Escape`, `LeftArrow`, `RightArrow` |
| `--duration` | number | `0` | Hold duration in seconds for Press action (0 = one-shot tap). Ignored by KeyDown/KeyUp. |

### Actions

| Action | Behavior | Description |
|--------|----------|-------------|
| `Press` | KeyDown → (hold duration) → KeyUp | One-shot key tap. If `--duration` > 0, holds the key for that many seconds before releasing. |
| `KeyDown` | KeyDown only | Hold the key down indefinitely. Must be followed by a matching `KeyUp`. |
| `KeyUp` | KeyUp only | Release a previously held key. |

### Split Key Hold Rules

- `KeyUp` must be preceded by a matching `KeyDown` for the same key
- Failing to call `KeyUp` after `KeyDown` leaves the key stuck in the pressed state

### Global Options

| Option | Description |
|--------|-------------|
| `--project-path <path>` | Target a specific Unity project (mutually exclusive with `--port`) |
| `-p, --port <port>` | Specify Unity TCP port directly (mutually exclusive with `--project-path`) |

## Examples

```bash
# One-shot key press (tap W)
uloop simulate-keyboard --key W

# Hold Space for 2 seconds (e.g. charge jump)
uloop simulate-keyboard --key Space --duration 2.0

# Manual key-down / key-up (e.g. hold Shift while pressing W)
uloop simulate-keyboard --action KeyDown --key LeftShift
uloop simulate-keyboard --key W
uloop simulate-keyboard --action KeyUp --key LeftShift

# WASD movement sequence
uloop simulate-keyboard --key W --duration 1.0 && \
uloop simulate-keyboard --key A --duration 0.5 && \
uloop simulate-keyboard --key S --duration 1.0 && \
uloop simulate-keyboard --key D --duration 0.5
```

## Prerequisites

- Unity must be in **PlayMode**
- The **Input System** package (`com.unity.inputsystem`) must be installed
- Active Input Handling must be set to "Input System Package (New)" or "Both" in Player Settings
