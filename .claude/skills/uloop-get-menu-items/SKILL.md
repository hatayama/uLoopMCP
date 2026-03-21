---
name: uloop-get-menu-items
description: "Discover available Unity Editor menu items. Use when you need to: (1) Find available menu commands and their paths, (2) Search for specific menu items by name, (3) Prepare menu item paths for execute-menu-item. Returns menu item list."
---

# uloop get-menu-items

Retrieve Unity MenuItems.

## Usage

```bash
uloop get-menu-items [options]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--filter-text` | string | - | Filter text |
| `--filter-type` | string | `contains` | Filter type: `contains`, `exact`, `startswith` |
| `--max-count` | integer | `200` | Maximum number of items |
| `--include-validation` | boolean | `false` | Include validation functions |

## Global Options

| Option | Description |
|--------|-------------|
| `--project-path <path>` | Target a specific Unity project (mutually exclusive with `--port`) |
| `-p, --port <port>` | Specify Unity TCP port directly (mutually exclusive with `--project-path`) |

## Examples

```bash
# List all menu items
uloop get-menu-items

# Filter by text
uloop get-menu-items --filter-text "GameObject"

# Exact match
uloop get-menu-items --filter-text "File/Save" --filter-type exact
```

## Output

Returns JSON array of menu items with paths and metadata.

## Notes

Use with `uloop execute-menu-item` to run discovered menu commands.
