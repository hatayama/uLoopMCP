---
name: uloop-device-list
description: "List connected physical devices available for Device Agent. Use when you need to: (1) discover connected Android/iOS devices, (2) check device connection status, (3) find device serial numbers for targeting. Shows connected devices with serial, model, and status."
---

# uloop device-list

List connected physical devices available for Device Agent.

## Usage

```bash
uloop device-list
```

## Parameters

This command takes no parameters.

## Examples

```bash
# List all connected devices
uloop device-list
```

## Output

Returns JSON with an array of connected devices, each containing:
- `Serial`: Device serial number
- `Model`: Device model name
- `Status`: Connection status (e.g., connected, offline)
