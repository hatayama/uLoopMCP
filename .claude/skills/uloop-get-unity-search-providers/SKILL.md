---
name: uloop-get-unity-search-providers
description: "Get details about available Unity Search providers. Use when you need to: (1) Discover available search providers and their IDs, (2) Understand search capabilities and filters, (3) Configure unity-search with specific provider options. Returns provider metadata."
---

# uloop get-unity-search-providers

Get detailed information about Unity Search providers.

## Usage

```bash
uloop get-unity-search-providers [options]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--provider-id` | string | - | Specific provider ID to query |
| `--active-only` | boolean | `false` | Only show active providers |
| `--include-descriptions` | boolean | `true` | Include descriptions |
| `--sort-by-priority` | boolean | `true` | Sort by priority |

## Global Options

| Option | Description |
|--------|-------------|
| `--project-path <path>` | Target a specific Unity project (mutually exclusive with `--port`) |
| `-p, --port <port>` | Specify Unity TCP port directly (mutually exclusive with `--project-path`) |

## Examples

```bash
# List all providers
uloop get-unity-search-providers

# Get specific provider
uloop get-unity-search-providers --provider-id asset

# Active providers only
uloop get-unity-search-providers --active-only
```

## Output

Returns JSON:
- `Providers`: array of provider info (ID, name, description, priority)

## Notes

Use provider IDs with `uloop unity-search --providers` option.
