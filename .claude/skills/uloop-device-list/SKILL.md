---
name: uloop-device-list
description: "List connected Android devices via ADB. Use when you need to: (1) check which devices are connected, (2) get device serial numbers for multi-device setups, (3) verify device connectivity before running tests."
---

# uloop device-list

List all connected Android devices with their serial numbers and status.

## Usage

```bash
uloop device-list
```

## Prerequisites

- Android SDK Platform Tools installed (adb in PATH)

## Examples

```bash
# List all connected devices
uloop device-list
```

## Output

Displays device serial, connection state, and model name for each connected device.
