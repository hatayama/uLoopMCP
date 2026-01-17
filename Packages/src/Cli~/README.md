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

## Direct CLI Usage

You can also call the CLI directly without using Skills:

```bash
# List available tools
uloop list

# Sync tool definitions from Unity to local cache
uloop sync

# Execute compilation
uloop compile

# Get logs
uloop get-logs --max-count 10

# Run tests
uloop run-tests --filter-type all

# Execute dynamic code
uloop execute-dynamic-code --code 'using UnityEngine; Debug.Log("Hello from CLI!");'
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
- [Tool Reference](https://github.com/hatayama/uLoopMCP/blob/main/Packages/src/TOOL_REFERENCE.md) - Detailed tool specifications

## License

MIT License
