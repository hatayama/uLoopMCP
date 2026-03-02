---
name: uloop-device-find-game-objects
description: "Search for GameObjects in a running app on a physical device. Use when you need to: (1) find UI elements by name, tag, or component type, (2) discover interactive elements for automated testing, (3) get objectIds for use with device-tap-object or other device commands. Queries the Device Agent over TCP on port 8800."
---

# uloop device-find-game-objects

Search for GameObjects in a running app on a connected physical device.

## Usage

```bash
uloop device-find-game-objects [options]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--name` | string | - | GameObject name to search for |
| `--tag` | string | - | Tag filter |
| `--component-type` | string | - | Required component type (e.g., `Button`, `Text`) |
| `--active-only` | boolean | `false` | Only return active GameObjects |

## Examples

```bash
# Find GameObjects by name
uloop device-find-game-objects --name "StartButton"

# Find GameObjects by tag
uloop device-find-game-objects --tag "Player"

# Find GameObjects with a specific component
uloop device-find-game-objects --component-type Button

# Find only active GameObjects by name
uloop device-find-game-objects --name "Panel" --active-only
```

## Output

Returns JSON with matching GameObjects, each containing:
- `ObjectId`: Session-scoped identifier for use with other device commands (e.g., `device-tap-object`)
- `Name`: GameObject name
- `Path`: Hierarchy path
- `Components`: List of attached components

## Notes

- The `ObjectId` is session-scoped and valid only for the current Device Agent session
- Use the returned `ObjectId` with `device-tap-object` to interact with found elements
