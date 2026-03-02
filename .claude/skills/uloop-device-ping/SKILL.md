---
name: uloop-device-ping
description: "Health check for Device Agent on a real device. Use when you need to: (1) verify the Device Agent is running and responsive, (2) check device connection status, (3) test connectivity before running other device commands."
---

# uloop device-ping

Health check for Device Agent. Verifies the agent is running and responsive.

## Usage

```bash
uloop device-ping [options]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--token <token>` | string | - | Auth token (or set `ULOOP_DEVICE_TOKEN` env var) |
| `-p, --port <port>` | number | `8800` | Device Agent port |

## Examples

```bash
# Ping device agent
uloop device-ping
```

## Output

Returns JSON with `status` and `uptimeSeconds`.
