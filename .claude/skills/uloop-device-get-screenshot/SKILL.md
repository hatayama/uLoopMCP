---
name: uloop-device-get-screenshot
description: "Take a screenshot from a real device. Use when you need to: (1) capture the current screen state on a running device, (2) verify UI appearance during automated testing, (3) debug visual issues on the device."
---

# uloop device-get-screenshot

Take a screenshot from the device screen.

## Usage

```bash
uloop device-get-screenshot [options]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--format <format>` | string | `png` | Image format: `png` or `jpg` |
| `--quality <n>` | integer | `75` | JPEG quality (1-100, only for jpg) |
| `--max-long-side <n>` | integer | `1568` | Max long side in pixels (resizes if larger) |
| `--token <token>` | string | - | Auth token (or set `ULOOP_DEVICE_TOKEN` env var) |
| `-p, --port <port>` | number | `8800` | Device Agent port |

## Examples

```bash
# Take PNG screenshot
uloop device-get-screenshot

# Take JPEG screenshot with quality
uloop device-get-screenshot --format jpg --quality 90

# Take smaller screenshot
uloop device-get-screenshot --max-long-side 800
```

## Output

Returns JSON with base64-encoded image data, format, width, and height.
