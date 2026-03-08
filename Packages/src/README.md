[日本語](README_ja.md)

[![Unity](https://img.shields.io/badge/Unity-2022.3+-red.svg)](https://unity3d.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.md)<br>
![ClaudeCode](https://img.shields.io/badge/Claude_Code-555?logo=claude)
![Cursor](https://img.shields.io/badge/Cursor-111?logo=Cursor)
![OpenAICodex](https://img.shields.io/badge/OpenAI_Codex-111?logo=openai)
![GoogleGemini](https://img.shields.io/badge/Google_Gemini-111?logo=googlegemini)
![GitHubCopilot](https://img.shields.io/badge/GitHub_Copilot-111?logo=githubcopilot)
![Windsurf](https://img.shields.io/badge/Windsurf-111?logo=Windsurf)

<h1 align="center">
    <img width="500" alt="uLoopMCP" src="https://github.com/user-attachments/assets/a8b53cca-5444-445d-aa39-9024d41763e6" />
</h1>

Let an AI agent compile, test, and operate your Unity project from popular LLM tools via CLI (recommended) or MCP.

Designed to keep AI-driven development loops running autonomously inside your existing Unity projects.

# Concept
uLoopMCP is a Unity integration tool designed so that **AI can drive your Unity project forward with minimal human intervention**.
Tasks that humans typically handle manually—compiling, running the Test Runner, checking logs, editing scenes, and capturing windows to verify UI layouts—are exposed as tools that LLMs can orchestrate.

uLoopMCP is built around two core ideas:

1. **Provide a "self-hosted development loop" where an AI can repeatedly compile, run tests, inspect logs, and fix issues using tools like `compile`, `run-tests`, `get-logs`, and `clear-console`.**
2. **Allow AI to operate the Unity Editor itself—creating objects, calling menu items, inspecting scenes, and refining UI layouts from screenshots—via tools like `execute-dynamic-code`, `execute-menu-item`, and `screenshot`.**

https://github.com/user-attachments/assets/569a2110-7351-4cf3-8281-3a83fe181817

# Installation

> [!WARNING]
> The following software is required
>
> - **Unity 2022.3 or later**
> - **Node.js 22.0 or later** - Required for CLI and MCP server execution
> - Install via the [official site](https://nodejs.org/en/download) or your preferred version manager

## Via Unity Package Manager

1. Open Unity Editor
2. Open Window > Package Manager
3. Click the "+" button
4. Select "Add package from git URL"
5. Enter the following URL:
```text
https://github.com/hatayama/uLoopMCP.git?path=/Packages/src
```

## Via OpenUPM (Recommended)

## Using Scoped registry in Unity Package Manager
1. Open Project Settings window and go to Package Manager page
2. Add the following entry to the Scoped Registries list:
```text
Name: OpenUPM
URL: https://package.openupm.com
Scope(s): io.github.hatayama.uloopmcp
```

3. Open Package Manager window and select OpenUPM in the My Registries section. uLoopMCP will be displayed.

# Quickstart

uLoopMCP provides two connection methods: **CLI** and **MCP**. Both offer the same core functionality.

| Connection | Characteristics | Recommended For |
|---------|------|-----------|
| **CLI (uloop)** Recommended | Auto-recognized by Skills-compatible LLM tools. No MCP config needed | Claude Code, Codex, and other Skills-compatible tools |
| **MCP** | Connect as an MCP server from LLM tools | Cursor, Windsurf, and other MCP-compatible tools |

> For MCP connection, CLI and Skills installation is not required.
> Proceed to [MCP Connection Steps](#mcp-connection-cli-alternative).

## Step 1: Install the CLI (CLI users only)

> **💡 CLI and MCP Relationship**
> CLI provides all MCP functionality plus additional CLI-specific features such as launching and restarting Unity.

Select Window > uLoop. A dedicated window will open — confirm that the **CLI** button is highlighted in blue.

Press the **Install CLI** button.  
<img width="277" height="327" alt="CleanShot 2026-02-26 at 20 14 25" src="https://github.com/user-attachments/assets/680b9586-6323-4bde-a2f0-1c3166f0c224" />


If you see the following display, the installation was successful.  
<img width="272" height="332" alt="CleanShot 2026-02-26 at 20 17 08" src="https://github.com/user-attachments/assets/1681b124-fac8-4ac9-8ea3-3e8651be9128" />



<details>
<summary>To install from terminal</summary>

```bash
npm install -g uloop-cli
```
</details>

## Step 2: Install Skills (CLI users only)

Select your target (Claude Code, Codex, etc.) and press the **Install Skills** button.  
<img width="272" height="328" alt="CleanShot 2026-02-26 at 20 20 42" src="https://github.com/user-attachments/assets/79b9514c-cdbf-4eb6-89e9-650ecd3f6f85" />


<details>
<summary>To install from terminal</summary>

```bash
# Install for Claude Code project
uloop skills install --claude

# Install for OpenAI Codex project
uloop skills install --codex

# Or install globally
uloop skills install --claude --global
```
</details>

That's it! After installing Skills, LLM tools can automatically handle instructions like these:

| Your Instruction | Skill Used by LLM Tools |
|---|---|
| "Launch Unity for this project" | `/uloop-launch` |
| "Fix the compile errors" | `/uloop-compile` |
| "Run the tests and tell me why they failed" | `/uloop-run-tests` + `/uloop-get-logs` |
| "Check the scene hierarchy" | `/uloop-get-hierarchy` |
| "Search for prefabs" | `/uloop-unity-search` |
| "Play the game and bring Unity to the front" | `/uloop-control-play-mode` + `/uloop-focus-window` |
| "Bulk-update prefab parameters" | `/uloop-execute-dynamic-code` |
| "Take a screenshot of Game View and adjust the UI layout" | `/uloop-screenshot` + `/uloop-execute-dynamic-code` |

> [!TIP]
> **No MCP configuration required!** As long as the server is running in the uLoopMCP Window and you have installed the CLI and Skills, LLM tools communicate directly with Unity.

<details>
<summary>All 15 Bundled Skills</summary>

- `/uloop-launch` - Launch Unity with correct version
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
- `/uloop-screenshot` - Capture EditorWindow
- `/uloop-control-play-mode` - Control Play Mode
- `/uloop-execute-dynamic-code` - Execute dynamic C# code
- `/uloop-get-unity-search-providers` - Get search provider details

</details>

<details>
<summary>Direct CLI Usage (Advanced)</summary>

You can also call the CLI directly without using Skills:

```bash
# List available tools
uloop list

# Launch Unity project with correct version
uloop launch

# Launch with build target (Android, iOS, StandaloneOSX, etc.)
uloop launch -p Android

# Kill running Unity and restart
uloop launch -r

# Execute compilation
uloop compile

# Compile and wait for Domain Reload to complete
uloop compile --wait-for-domain-reload true

# Get logs
uloop get-logs --max-count 10

# Run tests
uloop run-tests --filter-type all

# Execute dynamic code
uloop execute-dynamic-code --code 'using UnityEngine; Debug.Log("Hello from CLI!");'
```

</details>

<details>
<summary>Shell Completion (Optional)</summary>

You can install Bash/Zsh/PowerShell completion:

```bash
# Add completion script to shell config (auto-detects shell)
uloop completion --install

# Explicitly specify shell (when auto-detection fails on Windows)
uloop completion --shell bash --install        # Git Bash / MINGW64
uloop completion --shell powershell --install  # PowerShell

# Check completion script
uloop completion
```

</details>

## Project Path / Port Specification

If `--project-path` / `--port` is omitted, the port is automatically selected from the Unity project detected in the current directory.

To operate multiple Unity instances from a single LLM tool, explicitly specify a project path or port:

```bash
# Specify by project path (absolute or relative)
uloop compile --project-path /Users/foo/my-unity-project
uloop compile --project-path ../other-project

# Specify by port number
uloop compile --port {target-port}
```

> [!NOTE]
> - `--project-path` and `--port` cannot be used together.
> - You can find the port number in each Unity's uLoopMCP Window.

## MCP Connection (CLI Alternative)

You can also connect via MCP (Model Context Protocol) instead of CLI. No CLI or Skills installation required.

### MCP Connection Steps

1. Select Window > uLoop. A dedicated window will open — press the **MCP** button.
<img width="274" height="289" alt="CleanShot 2026-02-26 at 20 37 16" src="https://github.com/user-attachments/assets/5f2fc5db-fd33-4b5d-9f0e-3e2e0d134cf6" />

2. Next, select the target IDE in the LLM Tool Settings section. Press the yellow "Configure {LLM Tool Name}" button to automatically connect to the IDE.
<img width="335" alt="image" src="https://github.com/user-attachments/assets/25f1f4f9-e3c8-40a5-a2f3-903f9ed5f45b" />

3. IDE Connection Verification
  - For example, with Cursor, check the Tools & MCP in the settings page and find uLoopMCP. Click the toggle to enable MCP.

<img width="657" height="399" alt="image" src="https://github.com/user-attachments/assets/5137491d-0396-482f-b695-6700043b3f69" />

> [!WARNING]
> **About Windsurf**
> Project-level configuration is not supported; only a global configuration is available.

<details>
<summary>Manual Setup (Usually Unnecessary)</summary>

> [!NOTE]
> Usually automatic setup is sufficient, but if needed, you can manually edit the configuration file (e.g., `mcp.json`):

```json
{
  "mcpServers": {
    "uLoopMCP": {
      "command": "node",
      "args": [
        "[Unity Package Path]/TypeScriptServer~/dist/server.bundle.js"
      ],
      "env": {
        "UNITY_TCP_PORT": "{port}"
      }
    }
  }
}
```

**Path Examples**:
- **Via Package Manager**: `"/Users/username/UnityProject/Library/PackageCache/io.github.hatayama.uloopmcp@[hash]/TypeScriptServer~/dist/server.bundle.js"`
> [!NOTE]
> When installed via Package Manager, the package is placed in `Library/PackageCache` with a hashed directory name. Using the "Auto Configure Cursor" button will automatically set the correct path.

</details>

### Multiple Unity Instance Support
> [!NOTE]
> Multiple Unity instances can be supported by changing port numbers. uLoopMCP automatically assigns unused ports when starting up.

# Key Features
## Development Loop Tools
### 1. compile - Execute Compilation
Performs AssetDatabase.Refresh() and then compiles, returning the results. Can detect errors and warnings that built-in linters cannot find.
You can choose between incremental compilation and forced full compilation.
```text
→ Execute compile, analyze error and warning content
→ Automatically fix relevant files
→ Verify with compile again
```

### 2. get-logs - Retrieve Logs Same as Unity Console
Filter by LogType or search target string with advanced search capabilities. You can also choose whether to include stacktrace.
This allows you to retrieve logs while keeping the context small.
**MaxCount behavior**: Returns the latest logs (tail-like behavior). When MaxCount=10, returns the most recent 10 logs.
**Advanced Search Features**:
- **Regular Expression Support**: Use `UseRegex: true` for powerful pattern matching
- **Stack Trace Search**: Use `SearchInStackTrace: true` to search within stack traces
```
→ get-logs (LogType: Error, SearchText: "NullReference", MaxCount: 10)
→ get-logs (LogType: All, SearchText: "(?i).*error.*", UseRegex: true, MaxCount: 20)
→ get-logs (LogType: All, SearchText: "MyClass", SearchInStackTrace: true, MaxCount: 50)
→ Identify cause from stacktrace, fix relevant code
```

### 3. run-tests - Execute TestRunner (PlayMode, EditMode supported)
Executes Unity Test Runner and retrieves test results. You can set conditions with FilterType and FilterValue.
- FilterType: all (all tests), exact (individual test method name), regex (class name or namespace), assembly (assembly name)
- FilterValue: Value according to filter type (class name, namespace, etc.)
Test results can be output as xml. The output path is returned so AI can read it.
This is also a strategy to avoid consuming context.
```text
→ run-tests (FilterType: exact, FilterValue: "io.github.hatayama.uLoopMCP.ConsoleLogRetrieverTests.GetAllLogs_WithMaskAllOff_StillReturnsAllLogs")
→ Check failed tests, fix implementation to pass tests
```
> [!WARNING]
> During PlayMode test execution, Domain Reload is forcibly turned OFF. (Settings are restored after test completion)
> Note that static variables will not be reset during this period.

### Unity Editor Automation & Discovery Tools
### 4. clear-console - Log Cleanup
Clear logs that become noise during log searches.
```text
→ clear-console
→ Start new debug session
```

### 5. unity-search - Project Search with UnitySearch
You can use [UnitySearch](https://docs.unity3d.com/Manual/search-overview.html).
```text
→ unity-search (SearchQuery: "*.prefab")
→ List prefabs matching specific conditions
→ Identify problematic prefabs
```

### 6. get-unity-search-providers - Check UnitySearch Search Providers
Retrieve search providers offered by UnitySearch.
```text
→ Understand each provider's capabilities, choose optimal search method
```

### 7. get-menu-items - Retrieve Menu Items
Retrieve menu items defined with [MenuItem("xxx")] attribute. Can filter by string specification.

### 8. execute-menu-item - Execute Menu Items
Execute menu items defined with [MenuItem("xxx")] attribute.
```text
→ Execute project-specific tools
→ Check results with get-logs
```

### 9. find-game-objects - Search Scene Objects
Retrieve objects and examine component parameters. Also retrieve information about currently selected GameObjects (multiple selection supported) in Unity Editor.
```text
→ find-game-objects (RequiredComponents: ["Camera"])
→ Investigate Camera component parameters

→ find-game-objects (SearchMode: "Selected")
→ Get detailed information about currently selected GameObjects in Unity Editor (supports multiple selection)
```

### 10. get-hierarchy - Analyze Scene Structure
Retrieve information about the currently active Hierarchy in nested JSON format. Works at runtime as well.
**Automatic File Export**: Retrieved hierarchy data is always saved as JSON in `{project_root}/.uloop/outputs/HierarchyResults/` directory. The MCP response only returns the file path, minimizing token consumption even for large datasets.
**Selection Mode**: Use `UseSelection: true` to get hierarchy starting from currently selected GameObject(s) in Unity Editor. Supports multiple selection - when parent and child are both selected, only the parent is used as root to avoid duplicate traversal.
```text
→ Understand parent-child relationships between GameObjects, discover and fix structural issues
→ Regardless of scene size, hierarchy data is saved to a file and the path is returned instead of raw JSON
→ get-hierarchy (UseSelection: true)
→ Get hierarchy of currently selected GameObjects without specifying paths manually
```

### 11. focus-window - Bring Unity Editor Window to Front (macOS & Windows)
Ensures the Unity Editor window associated with the active MCP session becomes the foreground application on macOS and Windows Editor builds.
Great for keeping visual feedback in sync after other apps steal focus. (Linux is currently unsupported.)

### 12. screenshot - Take a Screenshot of EditorWindow
Take a screenshot of any EditorWindow as a PNG. Specify the window name (the text displayed in the title bar/tab) to capture.
When multiple windows of the same type are open (e.g., 3 Inspector windows), all windows are saved with numbered filenames.
Supports three matching modes: `exact` (default), `prefix`, and `contains` - all case-insensitive.
```text
→ screenshot (WindowName: "Console")
→ Save Console window state as PNG
→ Provide visual feedback to AI
```

### 13. control-play-mode - Control Play Mode
Control Unity Editor's Play Mode. Supports three actions: Play (start/resume), Stop, and Pause.
```
→ control-play-mode (Action: Play)
→ Start Play Mode to verify game behavior
→ control-play-mode (Action: Pause)
→ Pause to inspect state
```

### 14. execute-dynamic-code - Dynamic C# Code Execution
Execute C# code dynamically within Unity Editor.

> **⚠️ Important Prerequisites**
> To use this tool, you must install the `Microsoft.CodeAnalysis.CSharp` package using [OpenUPM NuGet](https://openupm.com/nuget/).

<details>
<summary>View Microsoft.CodeAnalysis.CSharp installation steps</summary>

**Installation steps:**

Use a scoped registry in Unity Package Manager via OpenUPM (recommended).

1. Open Project Settings window and go to the Package Manager page
2. Add the following entry to the Scoped Registries list:

```yaml
Name: OpenUPM
URL: https://package.openupm.com
Scope(s): org.nuget
```

3. Open the Package Manager window, select OpenUPM in the My Registries section, and install `Microsoft.CodeAnalysis.CSharp`.

</details>

Async support:
- You can write await in your snippet (Task/ValueTask/UniTask and any awaitable type)
- Cancellation is propagated when you pass a CancellationToken to the tool

**Security Level Support**: Implements 3-tier security control to progressively restrict executable code:

  - **Level 0 - Disabled**
    - No compilation or execution allowed

  - **Level 1 - Restricted** 【Recommended Setting】
    - All Unity APIs and .NET standard libraries are generally available
    - User-defined assemblies (Assembly-CSharp, etc.) are also accessible
    - Only pinpoint blocking of security-critical operations:
      - **File deletion**: `File.Delete`, `Directory.Delete`, `FileUtil.DeleteFileOrDirectory`
      - **File writing**: `File.WriteAllText`, `File.WriteAllBytes`, `File.Replace`
      - **Network communication**: All `HttpClient`, `WebClient`, `WebRequest`, `Socket`, `TcpClient` operations
      - **Process execution**: `Process.Start`, `Process.Kill`
      - **Dynamic code execution**: `Assembly.Load*`, `Type.InvokeMember`, `Activator.CreateComInstanceFrom`
      - **Thread manipulation**: Direct `Thread`, `Task` manipulation
      - **Registry operations**: All `Microsoft.Win32` namespace operations
    - Safe operations are allowed:
      - File reading (`File.ReadAllText`, `File.Exists`, etc.)
      - Path operations (all `Path.*` operations)
      - Information retrieval (`Assembly.GetExecutingAssembly`, `Type.GetType`, etc.)
    - Use cases: Normal Unity development, automation with safety assurance

  - **Level 2 - FullAccess**
    - **All assemblies are accessible (no restrictions)**
    - ⚠️ **Warning**: Security risks exist, use only with trusted code
```
→ execute-dynamic-code (Code: "GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube); return \"Cube created\";")
→ Rapid prototype verification, batch processing automation
→ Unity API usage restricted according to security level
```


> [!IMPORTANT]
> **Security Settings**
>
> Some tools are disabled by default for security reasons.
> To use these tools, enable the corresponding items in the uLoopMCP window "Security Settings":
>
> **Basic Security Settings**:
> - **Allow Tests Execution**: Enable `run-tests` tool
> - **Allow Menu Item Execution**: Enable `execute-menu-item` tool
> - **Allow Third Party Tools**: Enable user-developed custom tools
>
> **Dynamic Code Security Level** (`execute-dynamic-code` tool):
> - **Level 0 (Disabled)**: Complete code execution disabled (safest)
> - **Level 1 (Restricted)**: Unity API only, dangerous operations blocked (recommended)
> - **Level 2 (FullAccess)**: All APIs available (use with caution)
>
> Setting changes take effect immediately without server restart.
>
> **Warning**: When using these features for AI-driven code generation, we strongly recommend running in sandbox environments or containers to prepare for unexpected behavior and security risks.

## Tool Reference

For detailed specifications of all tools (parameters, responses, examples), see **[TOOL_REFERENCE.md](/Packages/src/TOOL_REFERENCE.md)**.

## uLoopMCP Extension Development
uLoopMCP enables efficient development of project-specific tools without requiring changes to the core package.
The type-safe design allows for reliable custom tool implementation in minimal time.
(If you ask AI, they should be able to make it for you soon ✨)

You can publish your extension tools on GitHub and reuse them across other projects. See [uLoopMCP-extensions-sample](https://github.com/hatayama/uLoopMCP-extensions-sample) for an example.

> [!TIP]
> **For AI-assisted development**: Detailed implementation guides are available in [.claude/rules/mcp-tools.md](/.claude/rules/mcp-tools.md) for tool development and [.claude/rules/cli.md](/.claude/rules/cli.md) for CLI/Skills development. These guides are automatically loaded by Claude Code when working in the relevant directories.

> [!IMPORTANT]
> **Security Settings**
>
> Project-specific tools require enabling **Allow Third Party Tools** in the uLoopMCP window "Security Settings".
> When developing custom tools that involve dynamic code execution, also consider the **Dynamic Code Security Level** setting.

<details>
<summary>View Implementation Guide</summary>

**Step 1: Create Schema Class** (define parameters):
```csharp
using System.ComponentModel;

public class MyCustomSchema : BaseToolSchema
{
    [Description("Parameter description")]
    public string MyParameter { get; set; } = "default_value";

    [Description("Example enum parameter")]
    public MyEnum EnumParameter { get; set; } = MyEnum.Option1;
}

public enum MyEnum
{
    Option1 = 0,
    Option2 = 1,
    Option3 = 2
}
```

**Step 2: Create Response Class** (define return data):
```csharp
public class MyCustomResponse : BaseToolResponse
{
    public string Result { get; set; }
    public bool Success { get; set; }

    public MyCustomResponse(string result, bool success)
    {
        Result = result;
        Success = success;
    }

    // Required parameterless constructor
    public MyCustomResponse() { }
}
```

**Step 3: Create Tool Class**:
```csharp
using System.Threading;
using System.Threading.Tasks;

[McpTool(Description = "Description of my custom tool")]  // ← Auto-registered with this attribute
public class MyCustomTool : AbstractUnityTool<MyCustomSchema, MyCustomResponse>
{
    public override string ToolName => "my-custom-tool";

    // Executed on main thread
    protected override Task<MyCustomResponse> ExecuteAsync(MyCustomSchema parameters, CancellationToken cancellationToken)
    {
        // Type-safe parameter access
        string param = parameters.MyParameter;
        MyEnum enumValue = parameters.EnumParameter;

        // Check for cancellation before long-running operations
        cancellationToken.ThrowIfCancellationRequested();

        // Implement custom logic here
        string result = ProcessCustomLogic(param, enumValue);
        bool success = !string.IsNullOrEmpty(result);

        // For long-running operations, periodically check for cancellation
        // cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new MyCustomResponse(result, success));
    }

    private string ProcessCustomLogic(string input, MyEnum enumValue)
    {
        // Implement custom logic
        return $"Processed '{input}' with enum '{enumValue}'";
    }
}
```

> [!IMPORTANT]
> **Important Notes**:
> - **Thread Safety**: Tools execute on Unity's main thread, so Unity API calls are safe without additional synchronization.

Please also refer to [Custom Tool Samples](/Assets/Editor/CustomToolSamples).

</details>

### Custom Skills for Your Tools

When you create a custom tool, you can create a `Skill/` subfolder within the tool folder and place a `SKILL.md` file there. This allows LLM tools to automatically discover and use your custom tool through the Skills system.

**How it works:**
1. Create a `Skill/` subfolder in your custom tool's folder
2. Place `SKILL.md` inside the `Skill/` folder
3. Run `uloop skills install --claude` to install all skills (bundled + project)
4. LLM tools will automatically recognize your custom skill

**Directory structure:**
```
Assets/Editor/CustomTools/MyTool/
├── MyTool.cs           # Tool implementation
└── Skill/
    ├── SKILL.md        # Skill definition (required)
    └── references/     # Additional files (optional)
        └── usage.md
```

**SKILL.md format:**
```markdown
---
name: uloop-my-custom-tool
description: "Description of what the tool does and when to use it."
---

# uloop my-custom-tool

Detailed documentation for the tool...
```

**Scanned locations** (searches for `Skill/SKILL.md` files):
- `Assets/**/Editor/<ToolFolder>/Skill/SKILL.md`
- `Packages/*/Editor/<ToolFolder>/Skill/SKILL.md`
- `Library/PackageCache/*/Editor/<ToolFolder>/Skill/SKILL.md`

> [!TIP]
> - Add `internal: true` to the frontmatter to exclude a skill from installation (useful for internal/debug tools)
> - Additional files in the `Skill/` folder (such as `references/`, `scripts/`, `assets/`) are also copied during installation

See [HelloWorld sample](/Assets/Editor/CustomCommandSamples/HelloWorld/Skill/SKILL.md) for a complete example.

For a more comprehensive example project, see [uLoopMCP-extensions-sample](https://github.com/hatayama/uLoopMCP-extensions-sample).

## Other
> [!TIP]
> **File Output**
>
> The `run-tests`, `unity-search`, and `get-hierarchy` tools can save results to the `{project_root}/.uloop/outputs/` directory to avoid massive token consumption when dealing with large datasets.
> **Recommendation**: Add `.uloop/` to `.gitignore` to exclude from version control.

## License
MIT License
