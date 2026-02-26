[日本語](README_ja.md)

# uloop-cli

[![npm version](https://img.shields.io/npm/v/uloop-cli.svg)](https://www.npmjs.com/package/uloop-cli)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Node.js](https://img.shields.io/badge/Node.js-20+-green.svg)](https://nodejs.org/)

**CLI companion for [uLoopMCP](https://github.com/hatayama/uLoopMCP)** - Let AI agents compile, test, and operate your Unity project.

> **Prerequisites**: This CLI requires [uLoopMCP](https://github.com/hatayama/uLoopMCP) to be installed in your Unity project and the server running. See the [main repository](https://github.com/hatayama/uLoopMCP) for setup instructions.

## Installation

```bash
npm install -g uloop-cli
```

## Quick Start

### Step 1: Install Skills

Skills allow LLM tools (Claude Code, Cursor, etc.) to automatically invoke Unity operations.

```bash
# Install for Claude Code (project-level)
uloop skills install --claude

# Install for OpenAI Codex (project-level)
uloop skills install --codex

# Or install globally
uloop skills install --claude --global
uloop skills install --codex --global
```

### Step 2: Use with LLM Tools

After installing Skills, LLM tools can automatically handle instructions like:

| Your Instruction | Skill Used |
|---|---|
| "Fix the compile errors" | `/uloop-compile` |
| "Run the tests and tell me why they failed" | `/uloop-run-tests` |
| "Check the scene hierarchy" | `/uloop-get-hierarchy` |
| "Search for prefabs" | `/uloop-unity-search` |

> **No MCP configuration required!** As long as the server is running in the uLoopMCP Window, LLM tools communicate directly with Unity through Skills.

## Available Skills

Skills are dynamically loaded from the uLoopMCP package in your Unity project. These are the default skills provided by uLoopMCP:

- `/uloop-compile` - Execute compilation
- `/uloop-get-logs` - Get console logs
- `/uloop-run-tests` - Run tests
- `/uloop-clear-console` - Clear console
- `/uloop-focus-window` - Bring Unity Editor to front
- `/uloop-get-hierarchy` - Get scene hierarchy
- `/uloop-unity-search` - Unity Search
- `/uloop-get-menu-items` - Get menu items
- `/uloop-execute-menu-item` - Execute menu item
- `/uloop-find-game-objects` - Find GameObjects
- `/uloop-capture-window` - Capture EditorWindow
- `/uloop-control-play-mode` - Control Play Mode
- `/uloop-execute-dynamic-code` - Execute dynamic C# code
- `/uloop-get-provider-details` - Get search provider details

Custom skills defined in your project are also automatically detected.

## CLI Command Reference

You can also call the CLI directly without using Skills.

### Utility Commands

```bash
uloop list           # List available tools from Unity
uloop sync           # Sync tool definitions from Unity to local cache
uloop completion     # Setup shell completion
```

### compile

Execute Unity project compilation.

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--force-recompile` | boolean | `false` | Force full recompilation |
| `--wait-for-domain-reload` | boolean | `false` | Wait for Domain Reload completion |

```bash
uloop compile
uloop compile --force-recompile true --wait-for-domain-reload true
```

### get-logs

Retrieve logs from Unity Console.

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--log-type` | enum | `All` | Log type filter: `Error`, `Warning`, `Log`, `All` |
| `--max-count` | integer | `100` | Maximum number of logs to retrieve |
| `--search-text` | string | | Text to search within logs |
| `--include-stack-trace` | boolean | `false` | Include stack trace in output |
| `--use-regex` | boolean | `false` | Use regex for search |
| `--search-in-stack-trace` | boolean | `false` | Search within stack trace |

```bash
uloop get-logs
uloop get-logs --log-type Error --max-count 10
uloop get-logs --search-text "NullReference" --include-stack-trace true
```

### run-tests

Execute Unity Test Runner.

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--test-mode` | enum | `EditMode` | Test mode: `EditMode`, `PlayMode` |
| `--filter-type` | enum | `all` | Filter type: `all`, `exact`, `regex`, `assembly` |
| `--filter-value` | string | | Filter value (used when filter-type is not `all`) |

```bash
uloop run-tests
uloop run-tests --test-mode EditMode --filter-type regex --filter-value "MyTests"
uloop run-tests --filter-type assembly --filter-value "MyApp.Tests.Editor"
```

### clear-console

Clear Unity console logs.

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--add-confirmation-message` | boolean | `true` | Add confirmation message after clearing |

```bash
uloop clear-console
```

### focus-window

Bring Unity Editor window to front.

No parameters.

```bash
uloop focus-window
```

### get-hierarchy

Get Unity Hierarchy structure.

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--root-path` | string | | Root GameObject path to start from |
| `--max-depth` | integer | `-1` | Maximum depth (-1 for unlimited) |
| `--include-components` | boolean | `true` | Include component information |
| `--include-inactive` | boolean | `true` | Include inactive GameObjects |
| `--include-paths` | boolean | `false` | Include path information |
| `--use-components-lut` | string | `auto` | Use LUT for components: `auto`, `true`, `false` |
| `--use-selection` | boolean | `false` | Use selected GameObject(s) as root(s) |

```bash
uloop get-hierarchy
uloop get-hierarchy --root-path "Canvas" --max-depth 3
uloop get-hierarchy --use-selection true
```

### unity-search

Search Unity project.

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--search-query` | string | | Search query (Unity Search syntax) |
| `--providers` | array | | Search providers (e.g., `asset`, `scene`, `menu`) |
| `--max-results` | integer | `50` | Maximum number of results |
| `--include-description` | boolean | `true` | Include detailed descriptions in results |
| `--include-metadata` | boolean | `false` | Include file metadata (size, modified date) |
| `--search-flags` | enum | `Default` | Search flags: `Default`, `Synchronous`, `WantsMore`, `Packages`, `Sorted` |
| `--save-to-file` | boolean | `false` | Save results to file |
| `--output-format` | enum | `JSON` | Output format when saving: `JSON`, `CSV`, `TSV` |
| `--auto-save-threshold` | integer | `100` | Auto-save threshold (0 to disable) |
| `--file-extensions` | array | | Filter by file extension (e.g., `cs`, `prefab`, `mat`) |
| `--asset-types` | array | | Filter by asset type (e.g., `Texture2D`, `GameObject`) |
| `--path-filter` | string | | Filter by path pattern (supports wildcards) |

```bash
uloop unity-search --search-query "*.prefab"
uloop unity-search --search-query "t:Texture2D" --max-results 10
uloop unity-search --search-query "t:MonoScript *.cs" --save-to-file true
```

### get-menu-items

Retrieve Unity MenuItems.

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--filter-text` | string | | Text to filter MenuItem paths |
| `--filter-type` | enum | `contains` | Filter type: `contains`, `exact`, `startswith` |
| `--max-count` | integer | `200` | Maximum number of items |
| `--include-validation` | boolean | `false` | Include validation functions |

```bash
uloop get-menu-items
uloop get-menu-items --filter-text "GameObject" --filter-type startswith
```

### execute-menu-item

Execute Unity MenuItem by path.

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--menu-item-path` | string | | Menu item path (e.g., "GameObject/Create Empty") |
| `--use-reflection-fallback` | boolean | `true` | Use reflection fallback if direct execution fails |

```bash
uloop execute-menu-item --menu-item-path "File/Save"
uloop execute-menu-item --menu-item-path "GameObject/Create Empty"
```

### find-game-objects

Find GameObjects with search criteria.

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--name-pattern` | string | | Name pattern to search |
| `--search-mode` | enum | `Exact` | Search mode: `Exact`, `Path`, `Regex`, `Contains`, `Selected` |
| `--required-components` | array | | Required component type names |
| `--tag` | string | | Tag filter |
| `--layer` | integer | | Layer filter |
| `--max-results` | integer | `20` | Maximum number of results |
| `--include-inactive` | boolean | `false` | Include inactive GameObjects |
| `--include-inherited-properties` | boolean | `false` | Include inherited properties |

```bash
uloop find-game-objects --name-pattern "Player"
uloop find-game-objects --required-components "Camera" --include-inactive true
uloop find-game-objects --tag "Enemy" --max-results 50
```

### capture-window

Capture Unity EditorWindow and save as PNG.

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--window-name` | string | `Game` | Window name (e.g., "Game", "Scene", "Console", "Inspector") |
| `--resolution-scale` | number | `1` | Resolution scale (0.1 to 1.0) |
| `--match-mode` | enum | `exact` | Matching mode: `exact`, `prefix`, `contains` (case-insensitive) |

```bash
uloop capture-window
uloop capture-window --window-name Scene
uloop capture-window --window-name Project --match-mode prefix
```

### execute-dynamic-code

Execute C# code in Unity Editor.

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--code` | string | | C# code to execute |
| `--parameters` | object | | Runtime parameters for execution |
| `--compile-only` | boolean | `false` | Compile only without execution |

```bash
uloop execute-dynamic-code --code 'using UnityEngine; Debug.Log("Hello!");'
uloop execute-dynamic-code --code 'Selection.activeGameObject.name' --compile-only true
```

### get-provider-details

Get Unity Search provider details.

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--provider-id` | string | | Specific provider ID (e.g., "asset", "scene") |
| `--active-only` | boolean | `false` | Only active providers |
| `--include-descriptions` | boolean | `true` | Include descriptions |
| `--sort-by-priority` | boolean | `true` | Sort by priority |

```bash
uloop get-provider-details
uloop get-provider-details --provider-id asset
uloop get-provider-details --active-only true
```

### control-play-mode

Control Unity Editor play mode.

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--action` | enum | `Play` | Action: `Play`, `Stop`, `Pause` |

```bash
uloop control-play-mode --action Play
uloop control-play-mode --action Stop
uloop control-play-mode --action Pause
```

## Shell Completion

Install Bash/Zsh/PowerShell completion for tab-completion support:

```bash
# Auto-detect shell and install
uloop completion --install

# Explicitly specify shell
uloop completion --shell bash --install        # Git Bash / MINGW64
uloop completion --shell powershell --install  # PowerShell
```

## Port Specification

You can operate multiple Unity instances by specifying the `--port` option:

```bash
uloop compile --port 8700
uloop compile --port 8701
```

If `--port` is omitted, the port configured for the current project is automatically used.

You can find the port number in each Unity's uLoopMCP Window.

## Requirements

- **Node.js 20.0 or later**
- **Unity 2022.3 or later** with [uLoopMCP](https://github.com/hatayama/uLoopMCP) installed
- uLoopMCP server running (Window > uLoopMCP > Start Server)

## Links

- [uLoopMCP Repository](https://github.com/hatayama/uLoopMCP) - Main package and documentation
- [Tool Reference](https://github.com/hatayama/uLoopMCP/blob/main/Packages/src/TOOL_REFERENCE.md) - Detailed API specifications and response schemas

## License

MIT License
