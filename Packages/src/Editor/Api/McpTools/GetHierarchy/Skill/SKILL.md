---
name: uloop-get-hierarchy
description: "Get Unity Hierarchy structure. Use when: inspecting scene structure, exploring GameObjects, checking parent-child relationships, or when user asks about hierarchy. Returns the scene's GameObject tree with components."
---

# uloop get-hierarchy

Get Unity Hierarchy structure.

## Usage

```bash
uloop get-hierarchy [options]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--root-path` | string | - | Root GameObject path to start from |
| `--max-depth` | integer | `-1` | Maximum depth (-1 for unlimited) |
| `--include-components` | boolean | `true` | Include component information |
| `--include-inactive` | boolean | `true` | Include inactive GameObjects |
| `--include-paths` | boolean | `false` | Include full path information |

## Examples

```bash
# Get entire hierarchy
uloop get-hierarchy

# Get hierarchy from specific root
uloop get-hierarchy --root-path "Canvas/UI"

# Limit depth
uloop get-hierarchy --max-depth 2

# Without components
uloop get-hierarchy --include-components false
```

## Output

Returns JSON with hierarchical structure of GameObjects and their components.
