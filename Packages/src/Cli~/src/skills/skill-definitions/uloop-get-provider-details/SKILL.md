---
name: uloop-get-provider-details
description: Get Unity Search provider details via uloop CLI. Use when you need to discover available search providers, understand search capabilities, or configure searches with specific providers.
---

# uloop get-provider-details

Get detailed information about Unity Search providers.

## Usage

```bash
uloop get-provider-details [options]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--provider-id` | string | - | Specific provider ID to query |
| `--active-only` | boolean | `false` | Only show active providers |
| `--include-descriptions` | boolean | `true` | Include descriptions |
| `--sort-by-priority` | boolean | `true` | Sort by priority |

## Examples

```bash
# List all providers
uloop get-provider-details

# Get specific provider
uloop get-provider-details --provider-id asset

# Active providers only
uloop get-provider-details --active-only
```

## Output

Returns JSON:
- `Providers`: array of provider info (ID, name, description, priority)

## Notes

Use provider IDs with `uloop unity-search --providers` option.
