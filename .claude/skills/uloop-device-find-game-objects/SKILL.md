---
name: uloop-device-find-game-objects
description: "Find GameObjects on a real device. Use when you need to: (1) search for UI elements by name on a running device, (2) discover objects by tag or component type, (3) get objectIds for tap-object commands."
---

# uloop device-find-game-objects

Find GameObjects on a device by name, tag, or component type. Returns objectIds for use with tap-object.

## Usage

```bash
uloop device-find-game-objects [options]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--name <name>` | string | - | GameObject name to search |
| `--tag <tag>` | string | - | Tag filter |
| `--component-type <type>` | string | - | Component type filter |
| `--active-only <value>` | boolean | `true` | Only include active GameObjects |
| `--token <token>` | string | - | Auth token (or set `ULOOP_DEVICE_TOKEN` env var) |
| `-p, --port <port>` | number | `8800` | Device Agent port |

## Examples

```bash
# Find by name
uloop device-find-game-objects --name "LoginButton"

# Find by tag
uloop device-find-game-objects --tag "Player"

# Find by component
uloop device-find-game-objects --component-type "Button"

# Include inactive objects
uloop device-find-game-objects --name "Panel" --active-only false
```

## Output

Returns JSON with matching GameObjects including objectId, name, path, and components.
