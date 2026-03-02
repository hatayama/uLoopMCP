---
name: uloop-device-get-screenshot
description: "Capture a screenshot from a running app on a physical device. Use when you need to: (1) see current state of the app on device, (2) verify UI layout or visual state, (3) capture evidence of test results. Retrieves the screenshot via the Device Agent over TCP on port 8800."
---

# uloop device-get-screenshot

Capture a screenshot from a running app on a connected physical device.

## Usage

```bash
uloop device-get-screenshot [options]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--format` | string | `png` | Image format: `png` or `jpg` |
| `--quality` | integer | - | JPEG quality (1-100, only applicable when format is `jpg`) |
| `--output` | string | - | File path to save the screenshot. When omitted, returns Base64-encoded image data. |

## Examples

```bash
# Capture screenshot as PNG (default)
uloop device-get-screenshot

# Capture screenshot as JPEG with quality setting
uloop device-get-screenshot --format jpg --quality 80

# Save screenshot to a specific file
uloop device-get-screenshot --output /tmp/device-screenshot.png

# Save as JPEG to a specific file
uloop device-get-screenshot --format jpg --quality 90 --output /tmp/screenshot.jpg
```

## Output

Returns JSON with:
- `ImageData`: Base64-encoded image data (when `--output` is not specified)
- `ImagePath`: Absolute path to the saved file (when `--output` is specified)
- `Width`: Image width in pixels
- `Height`: Image height in pixels
- `Format`: Image format used (`png` or `jpg`)

## Notes

- Default format is PNG for lossless quality
- The maximum long side of the image defaults to 1568 pixels
