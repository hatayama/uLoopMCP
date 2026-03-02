---
name: uloop-device-connect
description: "Connect to Device Agent running on an Android device via ADB. Use when you need to: (1) establish connection to a real device for automated testing, (2) set up ADB port forwarding, (3) authenticate with the Device Agent."
---

# uloop device-connect

Connect to Device Agent on an Android device. Sets up ADB port forwarding and authenticates.

## Usage

```bash
uloop device-connect [options]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--token <token>` | string | - | Auth token (or set `ULOOP_DEVICE_TOKEN` env var) |
| `--serial <serial>` | string | - | ADB device serial (for multi-device setups) |
| `-p, --port <port>` | number | `8800` | Device Agent port |
| `--skip-forward` | boolean | `false` | Skip ADB port forwarding if already set up |

## Prerequisites

- Android SDK Platform Tools installed (adb in PATH)
- Device connected via USB with USB debugging enabled
- App with Device Agent built and running on the device

## Examples

```bash
# Connect with token from environment variable
export ULOOP_DEVICE_TOKEN="my-secret-token"
uloop device-connect

# Connect with explicit token
uloop device-connect --token my-secret-token

# Connect to specific device (multi-device)
uloop device-connect --token my-token --serial DEVICE_SERIAL

# Skip port forwarding (already set up manually)
uloop device-connect --token my-token --skip-forward
```

## Output

Returns JSON with Device Agent capabilities, protocol version, and agent version.
