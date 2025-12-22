# MCP Tool Development Guide

This document describes how to create new MCP tools for uLoopMCP.

## Directory Structure

```
McpTools/
├── Core/                    # Base classes and infrastructure
│   ├── AbstractUnityTool.cs # Base class for all tools
│   ├── BaseToolSchema.cs    # Base class for parameter schemas
│   ├── BaseToolResponse.cs  # Base class for responses
│   └── McpToolAttribute.cs  # Attribute for tool registration
├── YourNewTool/             # Your new tool folder
│   ├── YourNewToolSchema.cs
│   ├── YourNewToolResponse.cs
│   └── YourNewToolTool.cs
└── ...
```

## Step-by-Step: Creating a New MCP Tool

### Step 1: Create Tool Folder

Create a new folder under `McpTools/` with your tool name (PascalCase):

```bash
mkdir McpTools/YourNewTool
```

### Step 2: Create Schema Class

The Schema defines the input parameters for your tool.

`YourNewToolSchema.cs`:

```csharp
using System.ComponentModel;

namespace io.github.hatayama.uLoopMCP
{
    public class YourNewToolSchema : BaseToolSchema
    {
        [Description("Description shown in MCP tool schema")]
        public string SomeParameter { get; set; } = "default value";

        [Description("Another parameter with enum")]
        public SomeEnum Mode { get; set; } = SomeEnum.Default;

        [Description("Numeric parameter")]
        public float Scale { get; set; } = 1.0f;
    }

    public enum SomeEnum
    {
        Default = 0,
        Option1 = 1,
        Option2 = 2
    }
}
```

**Key Points:**
- Inherit from `BaseToolSchema`
- Use `[Description]` attribute for parameter documentation
- Set default values for optional parameters
- Enums are automatically converted to string options in MCP schema

### Step 3: Create Response Class

The Response defines what your tool returns.

`YourNewToolResponse.cs`:

```csharp
#nullable enable

namespace io.github.hatayama.uLoopMCP
{
    public class YourNewToolResponse : BaseToolResponse
    {
        public string? ResultPath { get; set; }
        public int? Count { get; set; }
        public bool Success { get; set; }

        public YourNewToolResponse(string resultPath, int count)
        {
            ResultPath = resultPath;
            Count = count;
            Success = true;
        }

        public YourNewToolResponse(bool failure)
        {
            ResultPath = null;
            Count = null;
            Success = false;
        }

        public YourNewToolResponse()
        {
        }
    }
}
```

**Key Points:**
- Inherit from `BaseToolResponse`
- Use `#nullable enable` for null safety
- Provide constructors for success and failure cases
- Include default constructor for JSON deserialization

### Step 4: Create Tool Class

The Tool contains the main logic.

`YourNewToolTool.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    [McpTool(Description = "Brief description of what this tool does")]
    public class YourNewToolTool : AbstractUnityTool<YourNewToolSchema, YourNewToolResponse>
    {
        public override string ToolName => "your-new-tool";  // kebab-case

        protected override async Task<YourNewToolResponse> ExecuteAsync(
            YourNewToolSchema parameters,
            CancellationToken ct)
        {
            string correlationId = McpConstants.GenerateCorrelationId();

            VibeLogger.LogInfo(
                "your_new_tool_start",
                "Tool execution started",
                new { Mode = parameters.Mode.ToString() },
                correlationId: correlationId
            );

            // Validate parameters
            ValidateParameters(parameters);

            // Your tool logic here
            // Note: Already on main thread, no need to call MainThreadSwitcher

            VibeLogger.LogInfo(
                "your_new_tool_success",
                "Tool completed successfully",
                new { ResultPath = "some/path" },
                correlationId: correlationId
            );

            return new YourNewToolResponse("some/path", 42);
        }

        private void ValidateParameters(YourNewToolSchema parameters)
        {
            if (parameters.Scale < 0.1f || parameters.Scale > 2.0f)
            {
                throw new ArgumentException(
                    $"Scale must be between 0.1 and 2.0, got: {parameters.Scale}");
            }
        }
    }
}
```

**Key Points:**
- Add `[McpTool(Description = "...")]` attribute
- Inherit from `AbstractUnityTool<TSchema, TResponse>`
- Set `ToolName` as kebab-case string
- Use `CancellationToken ct` parameter name
- Use `VibeLogger` for logging
- No try-catch needed (follow project policy)

### Step 5: Compile and Test

1. Compile in Unity:
   ```
   mcp_uLoopMCP_compile
   ```

2. Test via MCP:
   ```
   mcp_uLoopMCP_your-new-tool
   ```

### Step 6: Create SKILL.md

Create skill documentation for CLI usage.

`Packages/src/Cli~/src/skills/skill-definitions/uloop-your-new-tool/SKILL.md`:

```markdown
---
name: uloop-your-new-tool
description: Brief description for AI context. Use when you need to: (1) First use case, (2) Second use case.
---

# uloop your-new-tool

One-line description of what this tool does.

## Usage

\`\`\`bash
uloop your-new-tool [--some-parameter <value>] [--mode <mode>]
\`\`\`

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--some-parameter` | string | `""` | Description |
| `--mode` | enum | `Default` | Options: `Default`, `Option1`, `Option2` |
| `--scale` | number | `1.0` | Scale factor (0.1 to 2.0) |

## Examples

\`\`\`bash
# Basic usage
uloop your-new-tool

# With parameters
uloop your-new-tool --mode Option1 --scale 0.5
\`\`\`

## Output

Returns JSON with:
- `ResultPath`: Path to the result
- `Count`: Number of items processed
- `Success`: Whether the operation succeeded

## Notes

- Any important notes or limitations
- Related tools: `uloop other-tool`
```

### Step 7: Generate bundled-skills.ts (Auto-Generated)

The `bundled-skills.ts` file is **automatically generated** from SKILL.md files.

**How it works:**
- The script `scripts/generate-bundled-skills.ts` scans `skill-definitions/` directory
- It reads all `SKILL.md` files and generates import statements
- Skills with `internal: true` in frontmatter are excluded

**Generate command:**
```bash
cd Packages/src/Cli~
npx tsx scripts/generate-bundled-skills.ts
```

**Output:**
```
Generated .../bundled-skills.ts
  - Included: 14 skills
  - Excluded (internal): uloop-get-project-info, uloop-get-version
```

**Note:** This is also run automatically during `npm run build`.

### Step 8: Update default-tools.json (Manual)

Add your tool schema to `Packages/src/Cli~/src/default-tools.json`:

```json
{
  "name": "your-new-tool",
  "description": "Brief description",
  "inputSchema": {
    "type": "object",
    "properties": {
      "SomeParameter": {
        "type": "string",
        "description": "Description"
      },
      "Mode": {
        "type": "string",
        "enum": ["Default", "Option1", "Option2"],
        "default": "Default"
      }
    }
  }
}
```

### Step 9: Run Lint and Build

```bash
cd Packages/src/Cli~
npm run lint && npm run build
```

This will:
1. Run ESLint on TypeScript files
2. Regenerate `bundled-skills.ts` from SKILL.md files
3. Bundle the CLI with esbuild

## Naming Conventions

| Item | Convention | Example |
|------|------------|---------|
| Folder | PascalCase | `YourNewTool/` |
| Schema class | PascalCase + Schema | `YourNewToolSchema` |
| Response class | PascalCase + Response | `YourNewToolResponse` |
| Tool class | PascalCase + Tool | `YourNewToolTool` |
| ToolName property | kebab-case | `"your-new-tool"` |
| SKILL.md folder | uloop- prefix + kebab-case | `uloop-your-new-tool/` |

## Tips

- **Test with EditorWindow first**: For complex tools, create a test EditorWindow in `Assets/Editor/` before implementing the MCP tool
- **Use async/await properly**: Use `TimerDelay.Wait()` for delays, not `Task.Delay()`
- **Handle Unity Editor state**: Consider both playing and non-playing states
- **Clean up resources**: Always clean up textures, render textures, temporary objects

## Reference Implementations

- Simple tool: `ClearConsole/`
- Tool with enum parameter: `ControlPlayMode/`
- Complex tool with async operations: `CaptureUnityWindow/`
- Tool with file output: `UnitySearch/`

