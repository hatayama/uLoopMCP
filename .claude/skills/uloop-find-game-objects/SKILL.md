---
name: uloop-find-game-objects
description: "Find specific GameObjects in scene. Use when: searching for objects by name, finding objects with specific components, locating tagged/layered objects, or when user asks to find GameObjects. Returns matching GameObjects with paths and components."
---

# uloop find-game-objects

Find GameObjects with search criteria.

## Usage

```bash
uloop find-game-objects [options]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--name-pattern` | string | - | Name pattern to search |
| `--search-mode` | string | `Contains` | Search mode: `Exact`, `Path`, `Regex`, `Contains` |
| `--required-components` | array | - | Required components |
| `--tag` | string | - | Tag filter |
| `--layer` | string | - | Layer filter |
| `--max-results` | integer | `20` | Maximum number of results |
| `--include-inactive` | boolean | `false` | Include inactive GameObjects |

## Examples

```bash
# Find by name
uloop find-game-objects --name-pattern "Player"

# Find with component
uloop find-game-objects --required-components Rigidbody

# Find by tag
uloop find-game-objects --tag "Enemy"

# Regex search
uloop find-game-objects --name-pattern "UI_.*" --search-mode Regex
```

## Output

Returns JSON array of matching GameObjects with paths and components.
