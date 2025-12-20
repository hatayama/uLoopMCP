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
│   ├── skills-manager.ts
│   ├── bundled-skills.ts
│   └── skill-definitions/ # 13 skill definitions (.md)
└── __tests__/
    └── cli-e2e.test.ts    # E2E tests
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

## npm Publishing

This directory is published to npm as the `uloop-cli` package.
Version is synchronized with `Packages/src/package.json` (managed by release-please).

## Notes

- `version.ts` is a separate file from TypeScriptServer~ (not a copy)
- Build artifact `dist/cli.bundle.cjs` is excluded via `.gitignore`
- `node_modules/` is also excluded via `.gitignore`
