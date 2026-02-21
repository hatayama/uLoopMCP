---
name: uloop-find-game-objects
description: "Find specific GameObjects in scene. Use when: searching for objects by name, finding objects with specific components, locating tagged/layered objects, getting currently selected GameObjects in Unity Editor, or when user asks to find GameObjects. Returns matching GameObjects with paths and components."
---

# uloop find-game-objects

Find GameObjects with search criteria or get currently selected objects.

## Usage

```bash
uloop find-game-objects [options]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--name-pattern` | string | - | Name pattern to search |
| `--search-mode` | string | `Contains` | Search mode: `Exact`, `Path`, `Regex`, `Contains`, `Selected` |
| `--required-components` | array | - | Required components |
| `--tag` | string | - | Tag filter |
| `--layer` | string | - | Layer filter |
| `--max-results` | integer | `20` | Maximum number of results |
| `--include-inactive` | boolean | `false` | Include inactive GameObjects |

## Search Modes

| Mode | Description |
|------|-------------|
| `Exact` | Exact name match |
| `Path` | Hierarchy path search (e.g., `Canvas/Button`) |
| `Regex` | Regular expression pattern |
| `Contains` | Partial name match (default) |
| `Selected` | Get currently selected GameObjects in Unity Editor |

## Global Options

| Option | Description |
|--------|-------------|
| `--project-path <path>` | Target a specific Unity project (mutually exclusive with `--port`). Path resolution follows the same rules as `cd` — absolute paths are used as-is, relative paths are resolved from cwd. |
| `-p, --port <port>` | Specify Unity TCP port directly (mutually exclusive with `--project-path`). |

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

# Get selected GameObjects
uloop find-game-objects --search-mode Selected

# Get selected including inactive
uloop find-game-objects --search-mode Selected --include-inactive
```

## Output

Returns JSON with matching GameObjects.

For `Selected` mode with multiple objects, results are exported to file:
- Single selection: JSON response directly
- Multiple selection: File at `.uloop/outputs/FindGameObjectsResults/`
- No selection: Empty results with message
