---
name: uloop-find-game-objects
description: Find specific GameObjects in scene. Use when you need to: (1) Search for objects by name patterns (exact, contains, regex), (2) Find objects with specific components like Collider or Rigidbody, (3) Locate tagged or layered objects, (4) Get currently selected GameObjects in Unity Editor.
---

# uloop find-game-objects

Find multiple GameObjects with advanced search criteria including name patterns, components, tags, layers, and editor selection.

## Usage

```bash
uloop find-game-objects [--name-pattern <pattern>] [--search-mode <mode>] [--required-components <components>] [--tag <tag>] [--layer <layer>] [--include-inactive] [--max-results <count>]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--name-pattern` | string | `""` | Name pattern to search for |
| `--search-mode` | enum | `Exact` | Search mode: `Exact`, `Path`, `Regex`, `Contains`, `Selected` |
| `--required-components` | string[] | `[]` | Component types to filter by (e.g., `BoxCollider`, `Rigidbody`) |
| `--tag` | string | `""` | Tag to filter by |
| `--layer` | number | `null` | Layer index to filter by |
| `--include-inactive` | boolean | `false` | Include inactive GameObjects |
| `--max-results` | number | `20` | Maximum number of results to return |

## Search Modes

| Mode | Description |
|------|-------------|
| `Exact` | Exact name match |
| `Path` | Hierarchy path search (e.g., `Canvas/Button`) |
| `Regex` | Regular expression pattern |
| `Contains` | Partial name match |
| `Selected` | Get currently selected GameObjects in Unity Editor |

## Examples

### Search by Name Pattern

```bash
# Exact match
uloop find-game-objects --name-pattern "Player" --search-mode Exact

# Partial match (contains)
uloop find-game-objects --name-pattern "Enemy" --search-mode Contains

# Regex pattern
uloop find-game-objects --name-pattern "Enemy\\d+" --search-mode Regex

# Hierarchy path
uloop find-game-objects --name-pattern "Canvas/Panel/Button" --search-mode Path
```

### Search by Component

```bash
# Find objects with BoxCollider
uloop find-game-objects --required-components BoxCollider

# Find objects with multiple components
uloop find-game-objects --required-components BoxCollider,Rigidbody
```

### Search by Tag/Layer

```bash
# Find objects by tag
uloop find-game-objects --tag "Player"

# Find objects by layer
uloop find-game-objects --layer 8
```

### Get Selected Objects

```bash
# Get currently selected GameObjects in Unity Editor
uloop find-game-objects --search-mode Selected

# Include inactive selected objects
uloop find-game-objects --search-mode Selected --include-inactive
```

## Output

### Standard Search Result (JSON)

```json
{
  "results": [
    {
      "name": "Player",
      "path": "World/Characters/Player",
      "isActive": true,
      "tag": "Player",
      "layer": 0,
      "components": [
        { "type": "Transform", "properties": {...} },
        { "type": "Rigidbody", "properties": {...} }
      ]
    }
  ],
  "totalFound": 1
}
```

### Selected Mode - Single Selection (JSON)

Returns the same format as standard search.

### Selected Mode - Multiple Selection (File Export)

When multiple objects are selected, results are exported to a JSON file:

```json
{
  "resultsFilePath": ".uloop/outputs/FindGameObjectsResults/find-game-objects_2025-01-26_12-00-00.json",
  "totalFound": 5,
  "message": "Multiple objects selected (5). Results exported to file."
}
```

The exported file contains:

```json
{
  "ExportTimestamp": "2025-01-26 12:00:00",
  "TotalCount": 5,
  "Results": [...]
}
```
