# Unity MCP Server

This is a Model Context Protocol (MCP) server that acts as a bridge between Unity and Cursor.

## Build Timing

### Automatic Builds
- **GitHub Actions**: Automatically builds and commits on push to the main branch.
- **postinstall**: Automatically builds when `npm install` is run.
- **prepublishOnly**: Automatically builds before publishing the package.

### Manual Builds

#### When is `npm install` required?
```bash
# New environment / initial setup
npm install
npm run build

# After changing package.json
npm install
npm run build

# After deleting node_modules
npm install
npm run build
```

#### ⚡ When npm install is not required
```bash
# For continuous development where node_modules already exists
npm run build  # Can be run directly
```

#### How to Check
```bash
# Check for the existence of node_modules
ls node_modules/ > /dev/null 2>&1 && echo "OK: can run npm run build" || echo "NG: npm install required"

# Check TypeScript compiler
npx tsc --version || echo "npm install required"
```

### Build Artifacts
- `dist/server.js` - Main MCP server
- `dist/unity-client.js` - Unity communication client
- `dist/tools/` - Various tools
- `dist/types/` - Type definitions

## Overview

This server provides a toolset for operating the Unity engine from the Cursor editor. It works in conjunction with the MCP Bridge on the Unity side via TCP/IP communication to enable operations such as compiling and fetching logs.

## Architecture

### Design Principles
- **High Cohesion**: Each component has a single responsibility.
- **Extensibility**: New tools can be easily added.
- **Type Safety**: Utilizes TypeScript's type system.

### Directory Structure

```
src/
├── types/
│   └── tool-types.ts          # Type definitions for tools
├── tools/
│   ├── base-tool.ts           # Base class for tools
│   ├── ping-tool.ts           # Ping tool for TypeScript side
│   ├── unity-ping-tool.ts     # Ping tool for Unity side
│   ├── compile-tool.ts        # Unity compile tool
│   ├── logs-tool.ts           # Unity log retrieval tool
│   └── tool-registry.ts       # Tool registration and management
├── server.ts                  # Main class for the MCP server
└── unity-client.ts            # Communication client for the Unity side
```

## Provided Tools

### 1. ping (development only)
- **Description**: Connection test for the TypeScript-side MCP server (enabled only in development).
- **Parameters**: 
  - `message` (string): Test message.
- **Activation Condition**: `NODE_ENV=development` or `ENABLE_PING_TOOL=true`.

### 2. unity.ping
- **Description**: Connection test to the Unity side (TCP/IP communication check).
- **Parameters**: 
  - `message` (string): Message to send to the Unity side.

### 3. action.compileUnity
- **Description**: Executes compilation of the Unity project and retrieves error information.
- **Parameters**: 
  - `forceRecompile` (boolean): Force recompile flag.

### 4. context.getUnityLogs
- **Description**: Retrieves log information from the Unity console.
- **Parameters**: 
  - `logType` (string): Log type to filter by (Error, Warning, Log, All).
  - `maxCount` (number): Maximum number of logs to retrieve.

## Setup

### Prerequisites
- Node.js 22 or higher
- Unity 2020.3 or higher
- Unity MCP Bridge package installed.

### Installation

```bash
cd Packages/src/TypeScriptServer
npm install
```

### Build

```bash
npm run build
```

### Run

#### Production Environment (ping tool disabled)
```bash
npm start
```

#### Development Environment (ping tool enabled)
```bash
npm run dev
# or
npm run start:dev
# Or control with environment variables
ENABLE_PING_TOOL=true npm start
```

## Direct Communication Test with Unity

If the MCP server on the Unity side is running on port 7400, you can execute commands directly via JSON-RPC communication.

### Run Compilation
```bash
echo '{"jsonrpc":"2.0","id":1,"method":"compile","params":{"forceRecompile":false}}' | nc localhost 7400
```

### Send Ping
```bash
echo '{"jsonrpc":"2.0","id":1,"method":"ping","params":{"message":"test"}}' | nc localhost 7400
```

### Get Logs
```bash
echo '{"jsonrpc":"2.0","id":1,"method":"getLogs","params":{"logType":"All","maxCount":10}}' | nc localhost 7400
```

### Run Tests
```bash
echo '{"jsonrpc":"2.0","id":1,"method":"runtests","params":{"filterType":"all","filterValue":"","saveXml":false}}' | nc localhost 7400
```

### Notes
- You need to start the MCP server on the Unity side by running "Window > Unity MCP > Start Server".
- The default port is 7400 (`McpServerConfig.DEFAULT_PORT`).

## Debug Scripts

These are various debug scripts for checking communication with the Unity side.

### Execution via npm scripts (recommended)

```bash
# Move to the TypeScript server directory
cd Packages/src/TypeScriptServer

# Run compilation
npm run debug:compile

# Force recompile
npm run debug:compile -- --force

# Get logs
npm run debug:logs

# Check connection
npm run debug:connection

# Get all logs
npm run debug:all-logs
```

### Direct Execution

```bash
# Move to the TypeScript server directory
cd Packages/src/TypeScriptServer

# Normal compilation
node debug/compile-check.js

# Force recompile
node debug/compile-check.js --force
# or
node debug/compile-check.js -f

# Show help
node debug/compile-check.js --help
```

### Available Debug Scripts

#### 1. Compilation Check (compile-check.js)
Checks communication with the Unity side and actually executes the compilation.

```bash
# Normal compilation
node debug/compile-check.js

# Force recompile
node debug/compile-check.js --force

# Show help
node debug/compile-check.js --help
```

#### 2. Get Logs (logs-fetch.js)
Retrieves and displays logs from the Unity Console.

```bash
# Get 10 of all logs
node debug/logs-fetch.js

# Get only error logs
node debug/logs-fetch.js --type Error

# Get 20 warning logs
node debug/logs-fetch.js -t Warning -c 20

# Show help
node debug/logs-fetch.js --help
```

#### 3. Connection Check (connection-check.js)
Tests basic connection and communication with the Unity side.

```bash
# Test all features (ping + compile + logs)
node debug/connection-check.js

# Run only ping test
node debug/connection-check.js --quick

# Run with verbose output
node debug/connection-check.js --verbose

# Show help
node debug/connection-check.js --help
```

#### 4. Get All Logs (all-logs-fetch.js)
Retrieves a large number of logs and displays statistics.

```bash
# Get 100 of all logs + display statistics
node debug/all-logs-fetch.js

# Get 200 of all logs
node debug/all-logs-fetch.js -c 200

# Display statistics only
node debug/all-logs-fetch.js --stats

# Show help
node debug/all-logs-fetch.js --help
```

### Execution Example

**Compile Test:**
```
=== Unity Compile Test ===
Force Recompile: OFF

1. Connecting to Unity...
✓ Connected successfully!

2. Executing compile...
✓ Compile completed!
Success: true
Errors: 0
Warnings: 0
Completed at: 2025-06-18T23:20:14.775Z

3. Disconnecting...
✓ Disconnected
```

**Connection Test (Quick):**
```
=== Unity Connection Test ===
Verbose: OFF
Quick Test: ON

1. Connecting to Unity...
✓ Connected successfully!

2. Testing ping...
✓ Ping response: Unity MCP Bridge received: Hello from connection test!

✓ Quick test completed successfully!

5. Disconnecting...
✓ Disconnected
```

### Prerequisites
- MCP server is running on the Unity side (Window > Unity MCP > Start Server).
- The Unity side is listening on localhost, port 7400.

### Features
- Connection test to the Unity side.
- Execution of normal and forced re-compilation.
- Log retrieval (filtering by type, statistics display).
- Control of behavior via command-line arguments.
- Retrieval and display of error/warning details.
- Automatic disconnection.

## How to Add a New Tool

### 1. Create a Tool Class

Create a new tool class in the `src/tools/` directory:

```typescript
import { z } from 'zod';
import { BaseTool } from './base-tool.js';

export class MyNewTool extends BaseTool {
  readonly name = 'my.newTool';
  readonly description = 'Description of the new tool';
  readonly inputSchema = {
    type: 'object',
    properties: {
      param1: {
        type: 'string',
        description: 'Description of parameter 1'
      }
    }
  };

  protected validateArgs(args: unknown) {
    const schema = z.object({
      param1: z.string()
    });
    return schema.parse(args || {});
  }

  protected async execute(args: { param1: string }): Promise<string> {
    // Implement the actual tool logic here
    return `Processing result: ${args.param1}`;
  }

  // Optionally, customize the response format
  protected formatResponse(result: string): ToolResponse {
    return {
      content: [
        {
          type: 'text',
          text: result
        }
      ]
    };
  }
}
```

### 2. Register in the Tool Registry

Add it to the `registerDefaultTools` method in `src/tools/tool-registry.ts`:

```typescript
private registerDefaultTools(context: ToolContext): void {
  this.register(new PingTool(context));
  this.register(new UnityPingTool(context));
  this.register(new CompileTool(context));
  this.register(new LogsTool(context));
  this.register(new MyNewTool(context)); // Add this
}
```

### 3. Add Type Definitions (if necessary)

If new types are needed, add them to `src/types/tool-types.ts`.

## Development Guidelines

### Coding Standards
- Type declarations are mandatory (no `var`, explicit type declarations are recommended).
- Keep nesting shallow with early returns.
- Use value objects utilizing record types.
- Error handling is standardized in the base class.

### Template Method Pattern

The `BaseTool` class provides the following template method pattern:

1. **validateArgs**: Argument validation.
2. **execute**: Actual processing.
3. **formatResponse**: Formatting for successful responses.
4. **formatErrorResponse**: Formatting for error responses.

## Troubleshooting

### Unity Connection Errors
- Check if the Unity MCP Bridge is running.
- Check if the port set in Window > uMPC is available.
- Run "Window > Unity MCP > Start Server" on the Unity side.

### Compilation Errors
```bash
npm run build
```
Check for TypeScript compilation errors.

### Dependency Errors
```bash
# Reinstall dependencies
rm -rf node_modules package-lock.json
npm install
```

### Type Errors
- Check type definitions in `src/types/tool-types.ts`.
- Check if it matches the return type of the MCP server.

## License

MIT License
 