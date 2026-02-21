---
paths: Packages/src/Cli~/**
---

# uloop CLI

A CLI tool for communicating with Unity Editor. Completely independent from the MCP server (TypeScriptServer~).

## Architecture

- **Zero dependency on TypeScriptServer~**
- Communicates with Unity directly via TCP connection in `direct-unity-client.ts`
- Interacts directly with Unity TCP server without going through MCP server

## Directory Structure

```text
src/
├── cli.ts                 # Entry point (commander.js)
├── version.ts             # Version management (auto-updated by release-please)
├── execute-tool.ts        # Tool execution logic
├── direct-unity-client.ts # Direct Unity TCP communication
├── simple-framer.ts       # TCP framing
├── port-resolver.ts       # Port detection
├── tool-cache.ts          # Tool cache (.uloop/tools.json)
├── arg-parser.ts          # Argument parsing
├── default-tools.json     # Default tool definitions
├── skills/                # Claude Code skills feature
│   ├── skills-command.ts
│   ├── skills-manager.ts  # Collects bundled + project skills
│   ├── bundled-skills.ts  # Auto-generated from SKILL.md files
│   └── skill-definitions/
│       └── cli-only/      # CLI-only internal skills
└── __tests__/
    └── cli-e2e.test.ts    # E2E tests
```

## Global Options

All commands that communicate with Unity support these global options:

| Option | Description |
|--------|-------------|
| `-p, --port <port>` | Specify Unity TCP port directly |
| `--project-path <path>` | Specify Unity project path to auto-resolve port |

`--port` and `--project-path` are mutually exclusive.

### --project-path

Resolves the target Unity instance by reading `UserSettings/UnityMcpSettings.json` from the specified project directory. Path resolution follows the same rules as `cd` — absolute paths (starting with `/`) are used as-is, relative paths are resolved from the current working directory.

```bash
# Absolute path
uloop compile --project-path /Users/foo/moorestech_server

# Relative path (resolved from cwd)
uloop compile --project-path ./moorestech_server
uloop compile --project-path ../other/project
```

## Build

```bash
npm run build    # Generates dist/cli.bundle.cjs
npm run lint     # Run ESLint
```

## E2E Tests

E2E tests communicate with actual Unity Editor, so the following prerequisites are required:

1. Unity Editor must be running
2. uLoopMCP package must be installed
3. CLI must be built (`npm run build`)

```bash
npm run test:cli # Run E2E tests (Unity must be running)
```

### Domain Reload and Connection Drops

**Important**: After `compile` command execution, Unity triggers a Domain Reload which forcibly disconnects the C# TCP server. This causes a few seconds of unavailability where Unity connections will fail. This behavior is unavoidable.

When writing E2E tests:
- Use `runCliWithRetry()` instead of `runCli()` for commands that run after `compile`
- Place `compile --force-recompile` tests at the end of the test suite to minimize impact on other tests
- Be aware that any test immediately following a compile-related test may experience connection instability

## npm Publishing

This directory is published to npm as the `uloop-cli` package.
Version is synchronized with `Packages/src/package.json` (managed by release-please).

## Skills System

Skills are collected from two sources:

1. **Bundled skills** (build-time): Auto-generated from `SKILL.md` files in:
   - `Editor/Api/McpTools/<ToolFolder>/SKILL.md`
   - `skill-definitions/cli-only/<SkillFolder>/SKILL.md`

2. **Project skills** (runtime): Scanned from Unity project's `Editor/` folders:
   - `Assets/**/Editor/`
   - `Packages/**/Editor/`
   - `Library/PackageCache/**/Editor/`

Run `npx tsx scripts/generate-bundled-skills.ts` to regenerate `bundled-skills.ts`.

Skills with `internal: true` in frontmatter are excluded from bundled skills.

Currently internal skills:
- `uloop-get-project-info`
- `uloop-get-version`

When updating README documentation about bundled skills count, remember to exclude internal skills from the count.

## Notes

- `version.ts` is a separate file from TypeScriptServer~ (not a copy)
- Build artifact `dist/cli.bundle.cjs` is excluded via `.gitignore`
- `node_modules/` is also excluded via `.gitignore`
