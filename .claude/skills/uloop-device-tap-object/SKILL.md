---
name: uloop-device-tap-object
description: "Tap a UI element on a real device by objectId or hierarchy path. Use when you need to: (1) click buttons or UI elements during automated testing, (2) interact with specific GameObjects on the device, (3) simulate user taps on identified objects."
---

# uloop device-tap-object

Tap a GameObject on the device by objectId or hierarchy path.

## Usage

```bash
uloop device-tap-object [options]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--object-id <id>` | integer | - | Object ID from device-find-game-objects (preferred) |
| `--object-path <path>` | string | - | Hierarchy path (e.g., `Canvas/Panel/Button[0]`) |
| `--token <token>` | string | - | Auth token (or set `ULOOP_DEVICE_TOKEN` env var) |
| `-p, --port <port>` | number | `8800` | Device Agent port |

## Path Syntax

- Use `/` to separate hierarchy levels: `Canvas/Panel/Button`
- Use `[n]` to disambiguate same-name siblings: `Canvas/Panel/Item[2]` (third "Item")

## Examples

```bash
# Tap by object ID (from find-game-objects)
uloop device-tap-object --object-id 42

# Tap by hierarchy path
uloop device-tap-object --object-path "Canvas/LoginButton"

# Tap specific sibling
uloop device-tap-object --object-path "Canvas/List/Item[1]"
```

## Output

Returns JSON with `tapped` (boolean) and `targetName` (string).
