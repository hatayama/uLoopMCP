---
name: uloop-device-tap-coordinate
description: "Tap at screen coordinates on a real device. Use when you need to: (1) tap a specific screen position during automated testing, (2) interact with UI elements by coordinate when path is unknown, (3) simulate precise touch input."
---

# uloop device-tap-coordinate

Tap at normalized screen coordinates on the device.

## Usage

```bash
uloop device-tap-coordinate --x <value> --y <value> [options]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--x <value>` | number | - | Normalized X coordinate (0.0 = left, 1.0 = right) |
| `--y <value>` | number | - | Normalized Y coordinate (0.0 = bottom, 1.0 = top) |
| `--token <token>` | string | - | Auth token (or set `ULOOP_DEVICE_TOKEN` env var) |
| `-p, --port <port>` | number | `8800` | Device Agent port |

## Examples

```bash
# Tap center of screen
uloop device-tap-coordinate --x 0.5 --y 0.5

# Tap top-right corner
uloop device-tap-coordinate --x 0.9 --y 0.9
```

## Output

Returns JSON with `tapped` (boolean) and `hitObjectName` (string or null).
