---
name: uloop-device-swipe
description: "Perform a swipe gesture on a real device. Use when you need to: (1) scroll through lists or pages, (2) simulate drag gestures, (3) navigate between screens with swipe."
---

# uloop device-swipe

Perform a swipe gesture on the device screen.

## Usage

```bash
uloop device-swipe --start-x <v> --start-y <v> --end-x <v> --end-y <v> [options]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--start-x <value>` | number | - | Start X (0.0-1.0) |
| `--start-y <value>` | number | - | Start Y (0.0-1.0) |
| `--end-x <value>` | number | - | End X (0.0-1.0) |
| `--end-y <value>` | number | - | End Y (0.0-1.0) |
| `--duration-ms <ms>` | integer | `300` | Swipe duration in milliseconds |
| `--token <token>` | string | - | Auth token (or set `ULOOP_DEVICE_TOKEN` env var) |
| `-p, --port <port>` | number | `8800` | Device Agent port |

## Examples

```bash
# Swipe up (scroll down)
uloop device-swipe --start-x 0.5 --start-y 0.3 --end-x 0.5 --end-y 0.7

# Swipe left (next page)
uloop device-swipe --start-x 0.8 --start-y 0.5 --end-x 0.2 --end-y 0.5

# Slow swipe
uloop device-swipe --start-x 0.5 --start-y 0.3 --end-x 0.5 --end-y 0.7 --duration-ms 1000
```

## Output

Returns JSON with `swiped` (boolean).
