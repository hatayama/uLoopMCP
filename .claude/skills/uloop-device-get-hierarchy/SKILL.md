---
name: uloop-device-get-hierarchy
description: "Get scene hierarchy from a real device. Use when you need to: (1) inspect the scene structure on a running device, (2) explore GameObject parent-child relationships, (3) find hierarchy paths for tap-object commands."
---

# uloop device-get-hierarchy

Get the scene hierarchy tree from a device.

## Usage

```bash
uloop device-get-hierarchy [options]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--depth <n>` | integer | `10` | Maximum hierarchy depth |
| `--include-components <value>` | boolean | `false` | Include component names |
| `--token <token>` | string | - | Auth token (or set `ULOOP_DEVICE_TOKEN` env var) |
| `-p, --port <port>` | number | `8800` | Device Agent port |

## Examples

```bash
# Get full hierarchy
uloop device-get-hierarchy

# Limit depth
uloop device-get-hierarchy --depth 3

# Include component info
uloop device-get-hierarchy --include-components true
```

## Output

Returns JSON with scene hierarchy tree including GameObject names, paths, and optionally component names.
