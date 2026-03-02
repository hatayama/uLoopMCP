---
name: uloop-device-input-text
description: "Input text to a field on a real device. Use when you need to: (1) type into InputFields during automated testing, (2) fill in forms or search boxes, (3) set text content of UI input elements."
---

# uloop device-input-text

Input text to an InputField on the device.

## Usage

```bash
uloop device-input-text --text <text> [options]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--text <text>` | string | - | Text to input |
| `--target-object-path <path>` | string | - | Target InputField hierarchy path (optional; finds first active InputField if omitted) |
| `--token <token>` | string | - | Auth token (or set `ULOOP_DEVICE_TOKEN` env var) |
| `-p, --port <port>` | number | `8800` | Device Agent port |

## Examples

```bash
# Input to first active InputField
uloop device-input-text --text "Hello World"

# Input to specific InputField
uloop device-input-text --text "user@example.com" --target-object-path "Canvas/LoginPanel/EmailInput"
```

## Output

Returns JSON with `success` (boolean) and `targetName` (string).
