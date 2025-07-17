[日本語](/Packages/src/README_ja.md)

[![Unity](https://img.shields.io/badge/Unity-2022.3+-red.svg)](https://unity3d.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.md)
[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/hatayama/uLoopMCP)  
![ClaudeCode](https://img.shields.io/badge/Claude_Code-555?logo=claude)
![Cursor](https://img.shields.io/badge/Cursor-111?logo=Cursor)
![GitHubCopilot](https://img.shields.io/badge/GitHub_Copilot-111?logo=githubcopilot)
![Windsurf](https://img.shields.io/badge/Windsurf-111?logo=Windsurf)

<h1 align="center">
    <img width="500" alt="uLoopMCP" src="https://github.com/user-attachments/assets/a8b53cca-5444-445d-aa39-9024d41763e6" />  
</h1>     

Control Unity Editor from various LLM tools.

Accelerates AI-driven development cycles to enable continuous improvement loops.

# Concept
This project was created with the concept of enabling AI-driven coding to run autonomously for as long as possible.
Normally, humans need to handle tasks like compiling Unity, running tests, and communicating logs to AI. uLoopMCP solves this hassle.

# Features
1. Simply install the package and press the button to connect to your LLM tool to start using it immediately.
2. Easily extensible functionality. You can quickly create your own custom MCP tools. (AI should be able to create them for you quickly)
3. Options are implemented to minimize context consumption.

# Tool Window
<img width="350" alt="image" src="https://github.com/user-attachments/assets/5863b58b-7b48-48ae-9a40-c874ddc11488" />

 - Manages and monitors server status
 - Provides visibility into LLM tool connection status
 - Enables easy connection to tools via the LLM tool settings button

# Key Features
#### 1. compile - Execute Compilation
Performs AssetDatabase.Refresh() and then compiles, returning the results. Can detect errors and warnings that built-in linters cannot find.  
You can choose between incremental compilation and forced full compilation.
```
→ Execute compile, analyze error and warning content
→ Automatically fix relevant files
→ Verify with compile again
```

#### 2. get-logs - Retrieve Logs Same as Unity Console
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

#### 3. run-tests - Execute TestRunner (PlayMode, EditMode supported)
Executes Unity Test Runner and retrieves test results. You can set conditions with FilterType and FilterValue.
- FilterType: all (all tests), exact (individual test method name), regex (class name or namespace), assembly (assembly name)
- FilterValue: Value according to filter type (class name, namespace, etc.)  
Test results can be output as xml. The output path is returned so AI can read it.  
This is also a strategy to avoid consuming context.
```
→ run-tests (FilterType: exact, FilterValue: "io.github.hatayama.uLoopMCP.ConsoleLogRetrieverTests.GetAllLogs_WithMaskAllOff_StillReturnsAllLogs")
→ Check failed tests, fix implementation to pass tests
```
> [!WARNING]  
> During PlayMode test execution, Domain Reload is forcibly turned OFF. (Settings are restored after test completion)  
> Note that static variables will not be reset during this period.

#### 4. clear-console - Log Cleanup
Clear logs that become noise during log searches.
```
→ clear-console
→ Start new debug session
```

#### 5. unity-search - Project Search with UnitySearch
You can use [UnitySearch](https://docs.unity3d.com/Manual/search-overview.html).
```
→ unity-search (SearchQuery: "*.prefab")
→ List prefabs matching specific conditions
→ Identify problematic prefabs
```

#### 6. get-provider-details - Check UnitySearch Search Providers
Retrieve search providers offered by UnitySearch.
```
→ Understand each provider's capabilities, choose optimal search method
```

#### 7. get-menu-items - Retrieve Menu Items
Retrieve menu items defined with [MenuItem("xxx")] attribute. Can filter by string specification.

#### 8. execute-menu-item - Execute Menu Items
Execute menu items defined with [MenuItem("xxx")] attribute.
```
→ Have AI generate test program
→ execute-menu-item (MenuItemPath: "Tools/xxx") to execute generated test program
→ Check results with get-logs
```

#### 9. find-game-objects - Search Scene Objects
Retrieve objects and examine component parameters.
```
→ find-game-objects (RequiredComponents: ["Camera"])
→ Investigate Camera component parameters
```

#### 10. get-hierarchy - Analyze Scene Structure
Retrieve information about the currently active Hierarchy in nested JSON format. Works at runtime as well.
**Automatic File Export**: Large hierarchies (>100KB) are automatically saved to `{project_root}/uLoopMCPOutputs/HierarchyResults/` directory to minimize token consumption.
```
→ Understand parent-child relationships between GameObjects, discover and fix structural issues
→ For large scenes, hierarchy data is saved to file and path is returned instead of raw JSON
```

> [!IMPORTANT]
> **Security Settings**
>
> `run-tests` and `execute-menu-item` tools are disabled by default because AI can execute arbitrary code.  
> To use these tools, enable the corresponding settings in the uLoopMCP window "Security Settings":
> - **Allow Tests Execution**: Enable `run-tests` tool
> - **Allow Menu Item Execution**: Enable `execute-menu-item` tool
> - **Allow Third Party Tools**: Enable user-developed custom tools
>
> Setting changes take effect immediately without server restart.  
> 
> **Warning**: When using these features for AI-driven code generation, we strongly recommend running in sandbox environments or containers to prepare for unexpected behavior and security risks.

## Feature Specifications
<details>
<summary>View Detailed Specifications</summary>

## Common Parameters & Response Format

All Unity MCP tools share the following common elements:

### Common Parameters
- `TimeoutSeconds` (number): Tool execution timeout in seconds

### Common Response Properties
All tools automatically include the following timing information:
- `StartedAt` (string): Tool execution start time (local time)
- `EndedAt` (string): Tool execution end time (local time)  
- `ExecutionTimeMs` (number): Tool execution duration in milliseconds

---

## Unity Core Tools

### 1. compile
- **Description**: Executes compilation after AssetDatabase.Refresh(). Returns compilation results with detailed timing information.
- **Parameters**: 
  - `ForceRecompile` (boolean): Whether to perform forced recompilation (default: false)
- **Response**: 
  - `Success` (boolean): Whether compilation was successful
  - `ErrorCount` (number): Total number of errors
  - `WarningCount` (number): Total number of warnings
  - `CompletedAt` (string): Compilation completion timestamp (ISO format)
  - `Errors` (array): Array of compilation errors (if any)
    - `Message` (string): Error message
    - `File` (string): File path where error occurred
    - `Line` (number): Line number where error occurred
  - `Warnings` (array): Array of compilation warnings (if any)
    - `Message` (string): Warning message
    - `File` (string): File path where warning occurred
    - `Line` (number): Line number where warning occurred
  - `Message` (string): Optional message for additional information

### 2. get-logs
- **Description**: Retrieves log information from Unity console with filtering and advanced search capabilities
- **Parameters**: 
  - `LogType` (enum): Log type to filter - "Error", "Warning", "Log", "All" (default: "All")
  - `MaxCount` (number): Maximum number of logs to retrieve (default: 100)
  - `SearchText` (string): Text to search within log messages (retrieve all if empty) (default: "")
  - `UseRegex` (boolean): Whether to use regular expression for search (default: false)
  - `SearchInStackTrace` (boolean): Whether to search within stack trace as well (default: false)
  - `IncludeStackTrace` (boolean): Whether to display stack traces (default: true)
- **Response**: 
  - `TotalCount` (number): Total number of logs available
  - `DisplayedCount` (number): Number of logs displayed in this response
  - `LogType` (string): Log type filter used
  - `MaxCount` (number): Maximum count limit used
  - `SearchText` (string): Search text filter used
  - `IncludeStackTrace` (boolean): Whether stack trace was included
  - `Logs` (array): Array of log entries
    - `Type` (string): Log type (Error, Warning, Log)
    - `Message` (string): Log message
    - `StackTrace` (string): Stack trace (if IncludeStackTrace is true)

### 3. run-tests
- **Description**: Executes Unity Test Runner and retrieves test results with comprehensive reporting
- **Parameters**: 
  - `FilterType` (enum): Type of test filter - "all"(0), "exact"(1), "regex"(2), "assembly"(3) (default: "all")
  - `FilterValue` (string): Filter value (specify when FilterType is other than all) (default: "")
    - `exact`: Individual test method name (exact match) (e.g.: io.github.hatayama.uLoopMCP.ConsoleLogRetrieverTests.GetAllLogs_WithMaskAllOff_StillReturnsAllLogs)
    - `regex`: Class name or namespace (regex pattern) (e.g.: io.github.hatayama.uLoopMCP.ConsoleLogRetrieverTests, io.github.hatayama.uLoopMCP)
    - `assembly`: Assembly name (e.g.: uLoopMCP.Tests.Editor)
  - `TestMode` (enum): Test mode - "EditMode"(0), "PlayMode"(1) (default: "EditMode")
    - ⚠️ **PlayMode Warning**: During PlayMode test execution, domain reload is temporarily disabled
  - `SaveXml` (boolean): Whether to save test results as XML file (default: false)
    - XML files are saved to `{project root}/uLoopMCPOutputs/TestResults/` folder
- **Response**: 
  - `Success` (boolean): Whether test execution was successful
  - `Message` (string): Test execution message
  - `CompletedAt` (string): Test execution completion timestamp (ISO format)
  - `TestCount` (number): Total number of tests executed
  - `PassedCount` (number): Number of passed tests
  - `FailedCount` (number): Number of failed tests
  - `SkippedCount` (number): Number of skipped tests
  - `XmlPath` (string): XML result file path (if SaveXml is true)

### 4. clear-console
- **Description**: Clears Unity console logs for clean development workflow
- **Parameters**: 
  - `AddConfirmationMessage` (boolean): Whether to add a confirmation log message after clearing (default: true)
- **Response**: 
  - `Success` (boolean): Whether the console clear operation was successful
  - `ClearedLogCount` (number): Number of logs that were cleared from the console
  - `ClearedCounts` (object): Breakdown of cleared logs by type
    - `ErrorCount` (number): Number of error logs that were cleared
    - `WarningCount` (number): Number of warning logs that were cleared
    - `LogCount` (number): Number of info logs that were cleared
  - `Message` (string): Message describing the clear operation result
  - `ErrorMessage` (string): Error message if the operation failed

### 5. find-game-objects
- **Description**: Find multiple GameObjects with advanced search criteria (component type, tag, layer, etc.)
- **Parameters**: 
  - `NamePattern` (string): GameObject name pattern to search for (default: "")
  - `SearchMode` (enum): Search mode - "Exact", "Path", "Regex", "Contains" (default: "Exact")
  - `RequiredComponents` (array): Array of component type names that GameObjects must have (default: [])
  - `Tag` (string): Tag filter (default: "")
  - `Layer` (number): Layer filter (default: null)
  - `IncludeInactive` (boolean): Whether to include inactive GameObjects (default: false)
  - `MaxResults` (number): Maximum number of results to return (default: 20)
  - `IncludeInheritedProperties` (boolean): Whether to include inherited properties (default: false)
- **Response**: 
  - `results` (array): Array of found GameObjects
    - `name` (string): GameObject name
    - `path` (string): Full hierarchy path
    - `isActive` (boolean): Whether the GameObject is active
    - `tag` (string): GameObject tag
    - `layer` (number): GameObject layer
    - `components` (array): Array of components on the GameObject
      - `TypeName` (string): Component type name
      - `AssemblyQualifiedName` (string): Full assembly qualified name
      - `Properties` (object): Component properties (if IncludeInheritedProperties is true)
  - `totalFound` (number): Total number of GameObjects found
  - `errorMessage` (string): Error message if search failed

---

## Unity Search & Discovery Tools

### 6. unity-search
- **Description**: Search Unity project using Unity Search API with comprehensive filtering and export options
- **Parameters**: 
  - `SearchQuery` (string): Search query string (supports Unity Search syntax) (default: "")
    - Examples: "*.cs", "t:Texture2D", "ref:MyScript", "p:MyPackage"
    - For detailed Unity Search documentation see: https://docs.unity3d.com/6000.1/Documentation/Manual/search-expressions.html and https://docs.unity3d.com/6000.0/Documentation/Manual/search-query-operators.html. Common queries: "*.cs" (all C# files), "t:Texture2D" (Texture2D assets), "ref:MyScript" (assets referencing MyScript), "p:MyPackage" (search in package), "t:MonoScript *.cs" (C# scripts only), "Assets/Scripts/*.cs" (C# files in specific folder). Japanese guide: https://light11.hatenadiary.com/entry/2022/12/12/193119
  - `Providers` (array): Specific search providers to use (empty = all active providers) (default: [])
    - Common providers: "asset", "scene", "menu", "settings", "packages"
  - `MaxResults` (number): Maximum number of search results to return (default: 50)
  - `IncludeDescription` (boolean): Whether to include detailed descriptions in results (default: true)
  - `IncludeMetadata` (boolean): Whether to include file metadata (size, modified date) (default: false)
  - `SearchFlags` (enum): Search flags for controlling Unity Search behavior (default: "Default"(0), "Synchronous"(1), "WantsMore"(2), "Packages"(4), "Sorted"(8))
  - `SaveToFile` (boolean): Whether to save search results to external file to avoid massive token consumption when dealing with large result sets. Results are saved as JSON/CSV files for external reading (default: false)
  - `OutputFormat` (enum): Output file format when SaveToFile is enabled (default: "JSON"(0), "CSV"(1), "TSV"(2))
  - `AutoSaveThreshold` (number): Threshold for automatic file saving (if result count exceeds this, automatically save to file). Set to 0 to disable automatic file saving (default: 100)
  - `FileExtensions` (array): Filter results by file extension (e.g., "cs", "prefab", "mat") (default: [])
  - `AssetTypes` (array): Filter results by asset type (e.g., "Texture2D", "GameObject", "MonoScript") (default: [])
  - `PathFilter` (string): Filter results by path pattern (supports wildcards) (default: "")
- **Response**: 
  - `Results` (array): Array of search result items (empty if results were saved to file)
  - `TotalCount` (number): Total number of search results found
  - `DisplayedCount` (number): Number of results displayed in this response
  - `SearchQuery` (string): Search query that was executed
  - `ProvidersUsed` (array): Search providers that were used for the search
  - `SearchDurationMs` (number): Search duration in milliseconds
  - `Success` (boolean): Whether the search was completed successfully
  - `ErrorMessage` (string): Error message if search failed
  - `ResultsFilePath` (string): Path to saved search results file (when SaveToFile is enabled)
  - `ResultsSavedToFile` (boolean): Whether results were saved to file
  - `SavedFileFormat` (string): File format of saved results
  - `SaveToFileReason` (string): Reason why results were saved to file
  - `AppliedFilters` (object): Applied filter information
    - `FileExtensions` (array): Filtered file extensions
    - `AssetTypes` (array): Filtered asset types
    - `PathFilter` (string): Applied path filter pattern
    - `FilteredOutCount` (number): Number of results filtered out

### 7. get-hierarchy
- **Description**: Get Unity Hierarchy structure in nested JSON format for AI-friendly processing
- **Parameters**: 
  - `IncludeInactive` (boolean): Whether to include inactive GameObjects in the hierarchy result (default: true)
  - `MaxDepth` (number): Maximum depth to traverse the hierarchy (-1 for unlimited depth) (default: -1)
  - `RootPath` (string): Root GameObject path to start hierarchy traversal from (empty/null for all root objects) (default: null)
  - `IncludeComponents` (boolean): Whether to include component information for each GameObject in the hierarchy (default: true)
  - `MaxResponseSizeKB` (number): Maximum response size in KB before saving to file (default: 100KB)
- **Response**: 
  - **Small hierarchies** (≤100KB): Direct nested JSON structure
    - `hierarchy` (array): Array of root level GameObjects in nested format
      - `id` (number): Unity's GetInstanceID() - unique within session
      - `name` (string): GameObject name
      - `depth` (number): Depth level in hierarchy (0 for root)
      - `isActive` (boolean): Whether the GameObject is active
      - `components` (array): Array of component type names attached to this GameObject
      - `children` (array): Recursive array of child GameObjects with same structure
    - `context` (object): Context information about the hierarchy
      - `sceneType` (string): Scene type ("editor", "runtime", "prefab")
      - `sceneName` (string): Scene name or prefab path
      - `nodeCount` (number): Total number of nodes in hierarchy
      - `maxDepth` (number): Maximum depth reached during traversal
  - **Large hierarchies** (>100KB): Automatic file export
    - `hierarchySavedToFile` (boolean): Always true for large hierarchies
    - `hierarchyFilePath` (string): Relative path to saved hierarchy file (e.g., "{project_root}/uLoopMCPOutputs/HierarchyResults/hierarchy_2025-07-10_21-30-15.json")
    - `saveToFileReason` (string): Reason for file export ("auto_threshold")
    - `context` (object): Same context information as above
  - `Message` (string): Operation message
  - `ErrorMessage` (string): Error message if operation failed

### 8. get-provider-details
- **Description**: Get detailed information about Unity Search providers including display names, descriptions, active status, and capabilities
- **Parameters**: 
  - `ProviderId` (string): Specific provider ID to get details for (empty = all providers) (default: "")
    - Examples: "asset", "scene", "menu", "settings"
  - `ActiveOnly` (boolean): Whether to include only active providers (default: false)
  - `SortByPriority` (boolean): Sort providers by priority (lower number = higher priority) (default: true)
  - `IncludeDescriptions` (boolean): Include detailed descriptions for each provider (default: true)
- **Response**: 
  - `Providers` (array): Array of provider information
  - `TotalCount` (number): Total number of providers found
  - `ActiveCount` (number): Number of active providers
  - `InactiveCount` (number): Number of inactive providers
  - `Success` (boolean): Whether the request was successful
  - `ErrorMessage` (string): Error message if request failed
  - `AppliedFilter` (string): Filter applied (specific provider ID or "all")
  - `SortedByPriority` (boolean): Whether results are sorted by priority

### 9. get-menu-items
- **Description**: Retrieve Unity MenuItems with detailed metadata for programmatic execution. Unlike Unity Search menu provider, this provides implementation details (method names, assemblies, execution compatibility) needed for automation and debugging
- **Parameters**: 
  - `FilterText` (string): Text to filter MenuItem paths (empty for all items) (default: "")
  - `FilterType` (enum): Type of filter to apply (contains(0), exact(1), startswith(2)) (default: "contains")
  - `IncludeValidation` (boolean): Include validation functions in the results (default: false)
  - `MaxCount` (number): Maximum number of menu items to retrieve (default: 200)
- **Response**: 
  - `MenuItems` (array): List of discovered MenuItems matching the filter criteria
    - `Path` (string): MenuItem path
    - `MethodName` (string): Execution method name
    - `TypeName` (string): Implementation class name
    - `AssemblyName` (string): Assembly name
    - `Priority` (number): Menu item priority
    - `IsValidateFunction` (boolean): Whether it's a validation function
  - `TotalCount` (number): Total number of MenuItems discovered before filtering
  - `FilteredCount` (number): Number of MenuItems returned after filtering
  - `AppliedFilter` (string): The filter text that was applied
  - `AppliedFilterType` (string): The filter type that was applied

### 10. execute-menu-item
- **Description**: Execute Unity MenuItem by path
- **Parameters**: 
  - `MenuItemPath` (string): The menu item path to execute (e.g., "GameObject/Create Empty") (default: "")
  - `UseReflectionFallback` (boolean): Whether to use reflection as fallback if EditorApplication.ExecuteMenuItem fails (default: true)
- **Response**: 
  - `MenuItemPath` (string): The menu item path that was executed
  - `Success` (boolean): Whether the execution was successful
  - `ExecutionMethod` (string): The execution method used (EditorApplication or Reflection)
  - `ErrorMessage` (string): Error message if execution failed
  - `Details` (string): Additional information about the execution
  - `MenuItemFound` (boolean): Whether the menu item was found in the system

---

## Related Documentation

- [Main README](README_ja.md) - Project overview and setup
- [Architecture Documentation](Editor/ARCHITECTURE.md) - Technical architecture details
- [TypeScript Server Architecture](TypeScriptServer~/ARCHITECTURE.md) - TypeScript server implementation
- [Changelog](CHANGELOG.md) - Version history and updates

</details>

## Usage
1. Select Window > uLoopMCP. A dedicated window will open, so press the "Start Server" button.  
<img width="335" alt="image" src="https://github.com/user-attachments/assets/4cfd7f26-7739-442d-bad9-b3f6d113a0d7" />

2. Next, select the target IDE in the LLM Tool Settings section. Press the yellow "Configure {LLM Tool Name}" button to automatically connect to the IDE.  
<img width="335" alt="image" src="https://github.com/user-attachments/assets/25f1f4f9-e3c8-40a5-a2f3-903f9ed5f45b" />

3. IDE Connection Verification
  - For example, with Cursor, check the Tools & Integrations in the settings page and find uLoopMCP. Click the toggle to enable MCP. If a red circle appears, restart Cursor.  
<img width="545" alt="image" src="https://github.com/user-attachments/assets/ed54d051-b78a-4bb4-bb2f-7ab23ebc1840" />


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

5. Multiple Unity Instance Support
> [!NOTE]
> Multiple Unity instances can be supported by changing port numbers. uLoopMCP automatically assigns unused ports when starting up.

## Installation

> [!WARNING]  
> The following software is required
>
> - **Unity 2022.3 or later**
> - **Node.js 18.0 or later** - Required for MCP server execution
> - Install Node.js from [here](https://nodejs.org/en/download)

### Via Unity Package Manager

1. Open Unity Editor
2. Open Window > Package Manager
3. Click the "+" button
4. Select "Add package from git URL"
5. Enter the following URL:
```
https://github.com/hatayama/uLoopMCP.git?path=/Packages/src
```

### Via OpenUPM (Recommended)

### Using Scoped registry in Unity Package Manager
1. Open Project Settings window and go to Package Manager page
2. Add the following entry to the Scoped Registries list:
```
Name: OpenUPM
URL: https://package.openupm.com
Scope(s): io.github.hatayama.uloopmcp
```

3. Open Package Manager window and select OpenUPM in the My Registries section. uLoopMCP will be displayed.

## Project-Specific Tool Development
uLoopMCP enables efficient development of project-specific MCP tools without requiring changes to the core package.  
The type-safe design allows for reliable custom tool implementation in minimal time.
(If you ask AI, they should be able to make it for you soon ✨)

> [!IMPORTANT]  
> **Security Settings**
> 
> Project-specific tools require enabling **Allow Third Party Tools** in the uLoopMCP window "Security Settings".

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
> - **Timeout Handling**: All tools inherit `TimeoutSeconds` parameter from `BaseToolSchema`. Implement `cancellationToken.ThrowIfCancellationRequested()` checks in long-running operations to ensure proper timeout behavior.
> - **Thread Safety**: Tools execute on Unity's main thread, so Unity API calls are safe without additional synchronization.

Please also refer to [Custom Tool Samples](/Assets/Editor/CustomToolSamples).

</details>

## Other
> [!TIP]
> **File Output**  
> 
> The `run-tests`, `unity-search`, and `get-hierarchy` tools can save results to the `{project_root}/uLoopMCPOutputs/` directory to avoid massive token consumption when dealing with large datasets.
> **Recommendation**: Add `uLoopMCPOutputs/` to `.gitignore` to exclude from version control.

> [!TIP]
> **Automatic MCP Execution in Cursor**  
> 
> By default, Cursor requires user permission when executing MCP.
> To disable this, go to Cursor Settings > Chat > MCP Tools Protection and turn it Off.
> Note that this cannot be controlled per MCP type or tool, so all MCPs will no longer require permission. This is a security tradeoff, so please configure it with that in mind.

> [!WARNING]
> **Windows Claude Code**  
> 
> When using Claude Code on Windows, version 1.0.51 or higher is recommended. (Git for Windows is required)  
> Please refer to [Claude Code CHANGELOG](https://github.com/anthropics/claude-code/blob/main/CHANGELOG.md).

## License
MIT License