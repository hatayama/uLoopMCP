# Unity CLI Loop
[日本語](README_ja.md)

[![Unity](https://img.shields.io/badge/Unity-2022.3+-red.svg)](https://unity3d.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.md)<br>
![ClaudeCode](https://img.shields.io/badge/Claude_Code-555?logo=claude)
![Cursor](https://img.shields.io/badge/Cursor-111?logo=Cursor)
![Codex](https://img.shields.io/badge/Codex-111?logo=data:image/svg+xml;base64,PHN2ZyByb2xlPSJpbWciIHZpZXdCb3g9IjAgMCAyNCAyNCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48cGF0aCBmaWxsPSJ3aGl0ZSIgZD0iTTIyLjI4MTkgOS44MjExYTUuOTg0NyA1Ljk4NDcgMCAwIDAtLjUxNTctNC45MTA4IDYuMDQ2MiA2LjA0NjIgMCAwIDAtNi41MDk4LTIuOUE2LjA2NTEgNi4wNjUxIDAgMCAwIDQuOTgwNyA0LjE4MThhNS45ODQ3IDUuOTg0NyAwIDAgMC0zLjk5NzcgMi45IDYuMDQ2MiA2LjA0NjIgMCAwIDAgLjc0MjcgNy4wOTY2IDUuOTggNS45OCAwIDAgMCAuNTExIDQuOTEwNyA2LjA1MSA2LjA1MSAwIDAgMCA2LjUxNDYgMi45MDAxQTUuOTg0NyA1Ljk4NDcgMCAwIDAgMTMuMjU5OSAyNGE2LjA1NTcgNi4wNTU3IDAgMCAwIDUuNzcxOC00LjIwNTggNS45ODk0IDUuOTg5NCAwIDAgMCAzLjk5NzctMi45MDAxIDYuMDU1NyA2LjA1NTcgMCAwIDAtLjc0NzUtNy4wNzI5em0tOS4wMjIgMTIuNjA4MWE0LjQ3NTUgNC40NzU1IDAgMCAxLTIuODc2NC0xLjA0MDhsLjE0MTktLjA4MDQgNC43NzgzLTIuNzU4MmEuNzk0OC43OTQ4IDAgMCAwIC4zOTI3LS42ODEzdi02LjczNjlsMi4wMiAxLjE2ODZhLjA3MS4wNzEgMCAwIDEgLjAzOC4wNTJ2NS41ODI2YTQuNTA0IDQuNTA0IDAgMCAxLTQuNDk0NSA0LjQ5NDR6bS05LjY2MDctNC4xMjU0YTQuNDcwOCA0LjQ3MDggMCAwIDEtLjUzNDYtMy4wMTM3bC4xNDIuMDg1MiA0Ljc4MyAyLjc1ODJhLjc3MTIuNzcxMiAwIDAgMCAuNzgwNiAwbDUuODQyOC0zLjM2ODV2Mi4zMzI0YS4wODA0LjA4MDQgMCAwIDEtLjAzMzIuMDYxNUw5Ljc0IDE5Ljk1MDJhNC40OTkyIDQuNDk5MiAwIDAgMS02LjE0MDgtMS42NDY0ek0yLjM0MDggNy44OTU2YTQuNDg1IDQuNDg1IDAgMCAxIDIuMzY1NS0xLjk3MjhWMTEuNmEuNzY2NC43NjY0IDAgMCAwIC4zODc5LjY3NjVsNS44MTQ0IDMuMzU0My0yLjAyMDEgMS4xNjg1YS4wNzU3LjA3NTcgMCAwIDEtLjA3MSAwbC00LjgzMDMtMi43ODY1QTQuNTA0IDQuNTA0IDAgMCAxIDIuMzQwOCA3Ljg3MnptMTYuNTk2MyAzLjg1NThMMTMuMTAzOCA4LjM2NCAxNS4xMTkyIDcuMmEuMDc1Ny4wNzU3IDAgMCAxIC4wNzEgMGw0LjgzMDMgMi43OTEzYTQuNDk0NCA0LjQ5NDQgMCAwIDEtLjY3NjUgOC4xMDQydi01LjY3NzJhLjc5Ljc5IDAgMCAwLS40MDctLjY2N3ptMi4wMTA3LTMuMDIzMWwtLjE0Mi0uMDg1Mi00Ljc3MzUtMi43ODE4YS43NzU5Ljc3NTkgMCAwIDAtLjc4NTQgMEw5LjQwOSA5LjIyOTdWNi44OTc0YS4wNjYyLjA2NjIgMCAwIDEgLjAyODQtLjA2MTVsNC44MzAzLTIuNzg2NmE0LjQ5OTIgNC40OTkyIDAgMCAxIDYuNjgwMiA0LjY2ek04LjMwNjUgMTIuODYzbC0yLjAyLTEuMTYzOGEuMDgwNC4wODA0IDAgMCAxLS4wMzgtLjA1NjdWNi4wNzQyYTQuNDk5MiA0LjQ5OTIgMCAwIDEgNy4zNzU3LTMuNDUzN2wtLjE0Mi4wODA1TDguNzA0IDUuNDU5YS43OTQ4Ljc5NDggMCAwIDAtLjM5MjcuNjgxM3ptMS4wOTc2LTIuMzY1NGwyLjYwMi0xLjQ5OTggMi42MDY5IDEuNDk5OHYyLjk5OTRsLTIuNTk3NCAxLjQ5OTctMi42MDY3LTEuNDk5N1oiLz48L3N2Zz4=)
![Antigravity](https://img.shields.io/badge/Antigravity-111?logo=google)
![GitHubCopilot](https://img.shields.io/badge/GitHub_Copilot-111?logo=githubcopilot)

<img width="1000" alt="logo" align="center" src="https://github.com/user-attachments/assets/864aa12e-8fd0-4b3a-9794-15cecdf95f94" />
<p align="right"><sub>(Logo inspired by Daft Punk's <i>Homework</i> album art)</sub></p>


Let an AI agent compile, test, and operate your Unity project from popular LLM tools via CLI.

Designed to keep AI-driven development loops running autonomously inside your existing Unity projects.

> **Note**: This project was formerly known as **uLoopMCP**.

# Concept
Unity CLI Loop is a Unity integration tool designed so that **AI can drive your Unity project forward with minimal human intervention**.
Tasks that humans typically handle manually—compiling, running the Test Runner, checking logs, editing scenes, and capturing windows to verify UI layouts—are exposed as tools that LLMs can orchestrate.

Unity CLI Loop is built around three core ideas:

1. **A self-hosted development loop where AI autonomously compiles, tests, inspects logs, and fixes issues.** Uses `compile`, `run-tests`, `get-logs`, `clear-console`.
2. **AI-driven Unity Editor operation—scene building, object manipulation, menu execution, and UI refinement from screenshots.** Uses `execute-dynamic-code`, `execute-menu-item`, `screenshot`.
3. **PlayMode automated testing—AI clicks buttons, drags elements, and verifies game behavior.** (Currently focused on mouse simulation, with plans to expand to keyboard input, touch gestures, and more.) Uses `simulate-mouse`, `execute-dynamic-code`, `screenshot`.

https://github.com/user-attachments/assets/569a2110-7351-4cf3-8281-3a83fe181817

# Installation

> [!WARNING]
> The following software is required
>
> - **Unity 2022.3 or later**
> - **Node.js 22.0 or later** - Required for CLI execution
> - Install via the [official site](https://nodejs.org/en/download) or your preferred version manager

## Via Unity Package Manager

1. Open Unity Editor
2. Open Window > Package Manager
3. Click the "+" button
4. Select "Add package from git URL"
5. Enter the following URL:
```text
https://github.com/hatayama/unity-cli-loop.git?path=/Packages/src
```

> **If you installed via git URL before v1.0.0**: The repository was renamed from `uLoopMCP` to `unity-cli-loop` in v1.0.0. Please update your `manifest.json`:
> ```text
> Old: https://github.com/hatayama/uLoopMCP.git?path=/Packages/src
> New: https://github.com/hatayama/unity-cli-loop.git?path=/Packages/src
> ```
> The old URL still works via GitHub redirect, but updating is recommended. OpenUPM users are not affected.

## Via OpenUPM (Recommended)

## Using Scoped registry in Unity Package Manager
1. Open Project Settings window and go to Package Manager page
2. Add the following entry to the Scoped Registries list:
```text
Name: OpenUPM
URL: https://package.openupm.com
Scope(s): io.github.hatayama.uloopmcp
```

3. Open Package Manager window and select OpenUPM in the My Registries section. Unity CLI Loop will be displayed.

# Quickstart

## Step 1: Install the CLI

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

See [uloop-cli on npm](https://www.npmjs.com/package/uloop-cli) for details.
</details>

## Step 2: Install Skills

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


<details>
<summary>All 16 Bundled Skills</summary>

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
- `/uloop-simulate-mouse` - Simulate mouse input on PlayMode UI
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

<details>
<summary>Using MCP instead of CLI</summary>

> [!WARNING]
> MCP connection may be deprecated or removed in a future release. We recommend using the CLI instead.

You can also connect via MCP (Model Context Protocol). CLI and Skills installation is not required for MCP.

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
> Multiple Unity instances can be supported by changing port numbers. Unity CLI Loop automatically assigns unused ports when starting up.

</details>

# Key Features
## Development Loop Tools
### 1. compile - Execute Compilation
Performs AssetDatabase.Refresh() and then compiles, returning the results. Can detect errors and warnings that built-in linters cannot find.
You can choose between incremental compilation and forced full compilation.
With `WaitForDomainReload=true`, results are returned after Domain Reload completes, regardless of the `ForceRecompile` value.
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
→ run-tests (FilterType: exact, FilterValue: "PlayerControllerTests.TestJump")
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
**Automatic File Export**: Retrieved hierarchy data is always saved as JSON in `{project_root}/.uloop/outputs/HierarchyResults/` directory. The response only returns the file path, minimizing token consumption even for large datasets.
**Selection Mode**: Use `UseSelection: true` to get hierarchy starting from currently selected GameObject(s) in Unity Editor. Supports multiple selection - when parent and child are both selected, only the parent is used as root to avoid duplicate traversal.
```text
→ Understand parent-child relationships between GameObjects, discover and fix structural issues
→ Regardless of scene size, hierarchy data is saved to a file and the path is returned instead of raw JSON
→ get-hierarchy (UseSelection: true)
→ Get hierarchy of currently selected GameObjects without specifying paths manually
```

### 11. focus-window - Bring Unity Editor Window to Front (macOS & Windows)
Ensures the Unity Editor window becomes the foreground application on macOS and Windows Editor builds.
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

### PlayMode Automated Testing Tools
### 15. simulate-mouse - Simulate Mouse Input on PlayMode UI
Simulate mouse click, long-press, and drag on PlayMode UI elements. Uses EventSystem and ExecuteEvents to dispatch pointer events directly — works independently of both old and new Input System.

Supports 6 actions: Click, LongPress, Drag (one-shot), DragStart/DragMove/DragEnd (split drag).

```text
→ screenshot (CaptureMode: rendering, AnnotateElements: true)
→ Get element coordinates from AnnotatedElements (SimX/SimY)
→ simulate-mouse (Action: Click, X: 400, Y: 300)
→ simulate-mouse (Action: LongPress, X: 400, Y: 300, Duration: 5.0)
→ simulate-mouse (Action: Drag, FromX: 100, FromY: 500, X: 400, Y: 300)
→ simulate-mouse (Action: DragStart, X: 100, Y: 500)
→ simulate-mouse (Action: DragMove, X: 200, Y: 400, DragSpeed: 300)
→ simulate-mouse (Action: DragEnd, X: 400, Y: 300)
```
https://github.com/user-attachments/assets/c7ee9103-c282-4f90-8b01-64bb17400f3e

## Tool Reference

For detailed specifications of all tools (parameters, responses, examples), see **[TOOL_REFERENCE.md](/Packages/src/TOOL_REFERENCE.md)**.

## Unity CLI Loop Extension Development
Unity CLI Loop enables efficient development of project-specific tools without requiring changes to the core package.
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
