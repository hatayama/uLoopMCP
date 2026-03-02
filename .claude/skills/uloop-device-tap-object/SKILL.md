---
name: uloop-device-tap-object
description: "Tap a GameObject on a physical device via EventSystem. Use when you need to: (1) tap UI buttons or interactive elements, (2) automate user interactions on device, (3) simulate touch input on specific GameObjects. Sends tap events through the Device Agent over TCP on port 8800."
---

# uloop device-tap-object

Tap a GameObject on a connected physical device via the Unity EventSystem.

## Usage

```bash
uloop device-tap-object [options]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--object-id` | string | - | Session-scoped objectId from `device-find-game-objects` (preferred) |
| `--object-path` | string | - | Hierarchy path to the target GameObject (fallback) |

## Object Targeting

- **Preferred**: Use `--object-id` obtained from `device-find-game-objects` for reliable targeting
- **Fallback**: Use `--object-path` with the full hierarchy path (e.g., `Canvas/Panel/Button`)
- **Same-name siblings**: Append `[n]` index to disambiguate (e.g., `Canvas/Panel/Button[2]` for the third Button)

## Examples

```bash
# Tap by objectId (preferred)
uloop device-tap-object --object-id "abc123"

# Tap by hierarchy path
uloop device-tap-object --object-path "Canvas/MainMenu/StartButton"

# Tap a specific sibling by index
uloop device-tap-object --object-path "Canvas/Panel/Button[2]"
```

## Output

Returns JSON with:
- `Success`: Whether the tap was delivered
- `TargetObject`: Name of the tapped GameObject

## Notes

- The tap is delivered through Unity's EventSystem, simulating a real touch event
- `--object-id` is preferred over `--object-path` for reliability, as IDs are unambiguous within a session
- Use `device-find-game-objects` first to discover available objects and obtain their `ObjectId`
- The `[n]` index for same-name siblings is zero-based
