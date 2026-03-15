[日本語](TOOL_REFERENCE_ja.md)

# uLoopMCP Tool Reference

This document provides detailed specifications for all uLoopMCP tools.

## Common Response Format

All Unity MCP tools share the following common elements:

### Common Response Properties
All tools automatically include the following property:
- `Ver` (string): uLoopMCP server version for CLI compatibility check

---

## Unity Core Tools

### 1. compile
- **Description**: Executes compilation after AssetDatabase.Refresh(). Returns compilation results with detailed timing information.
- **Parameters**:
  - `ForceRecompile` (boolean): Whether to perform forced recompilation (default: false)
  - `WaitForDomainReload` (boolean): Whether to wait for domain reload completion before returning (default: false)
- **Response**:
  - `Success` (boolean | null): Whether compilation was successful. Null when ForceRecompile=true because results are unavailable until domain reload completes
  - `ErrorCount` (number | null): Total number of errors. Null when ForceRecompile=true
  - `WarningCount` (number | null): Total number of warnings. Null when ForceRecompile=true
  - `Errors` (array | null): Array of compilation errors. Null when ForceRecompile=true
    - `Message` (string): Error message
    - `File` (string): File path where error occurred
    - `Line` (number): Line number where error occurred
  - `Warnings` (array | null): Array of compilation warnings. Null when ForceRecompile=true
    - `Message` (string): Warning message
    - `File` (string): File path where warning occurred
    - `Line` (number): Line number where warning occurred
  - `Message` (string): Optional message for additional information
  - `ProjectRoot` (string): Unity project root path. Set only when WaitForDomainReload=true

### 2. get-logs
- **Description**: Retrieves log information from Unity console with filtering and advanced search capabilities
- **Parameters**:
  - `LogType` (enum): Log type to filter - "Error", "Warning", "Log", "All" (default: "All")
  - `MaxCount` (number): Maximum number of logs to retrieve (default: 100)
  - `SearchText` (string): Text to search within log messages (retrieve all if empty) (default: "")
  - `UseRegex` (boolean): Whether to use regular expression for search (default: false)
  - `SearchInStackTrace` (boolean): Whether to search within stack trace as well (default: false)
  - `IncludeStackTrace` (boolean): Whether to display stack traces (default: false)
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
    - **PlayMode Warning**: During PlayMode test execution, domain reload is temporarily disabled
- **Response**:
  - `Success` (boolean): Whether test execution was successful
  - `Message` (string): Test execution message
  - `CompletedAt` (string): Test execution completion timestamp (ISO format)
  - `TestCount` (number): Total number of tests executed
  - `PassedCount` (number): Number of passed tests
  - `FailedCount` (number): Number of failed tests
  - `SkippedCount` (number): Number of skipped tests
  - `XmlPath` (string): NUnit XML result file path (auto-saved when tests fail). XML files are saved to `{project root}/.uloop/outputs/TestResults/` folder

### 4. clear-console
- **Description**: Clears Unity console logs for clean development workflow
- **Parameters**:
  - `AddConfirmationMessage` (boolean): Whether to add a confirmation log message after clearing (default: false)
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
  - `SearchMode` (enum): Search mode - "Exact", "Path", "Regex", "Contains", "Selected" (default: "Exact")
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
      - `type` (string): Component type name
      - `fullTypeName` (string): Full assembly qualified type name
      - `properties` (array): Component properties (if IncludeInheritedProperties is true)
  - `totalFound` (number): Total number of GameObjects found
  - `errorMessage` (string): Error message if search failed
  - `resultsFilePath` (string): File path where results were saved (when results are exported to file)
  - `message` (string): Operation message
  - `processingErrors` (array): Array of objects that failed to serialize
    - `gameObjectName` (string): Name of the GameObject that failed
    - `gameObjectPath` (string): Path of the GameObject that failed
    - `error` (string): Error description

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
  - `IncludePaths` (boolean): Whether to include path information for nodes (default: false)
  - `UseComponentsLut` (string): Use LUT for components - "auto", "true", "false" (default: "auto")
  - `UseSelection` (boolean): Whether to use currently selected GameObject(s) as root(s). When true, RootPath is ignored (default: false)
- **Response**:
  - `message` (string): Human-readable guidance for clients to locate and read the JSON file
  - `hierarchyFilePath` (string): File path where hierarchy data was saved (e.g., "{project_root}/.uloop/outputs/HierarchyResults/hierarchy_2025-07-10_21-30-15.json"). The exported JSON file contains `hierarchy` (nested array of GameObjects) and `context` (scene info, node count, max depth)

### 8. get-menu-items
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
    - `CanExecuteViaEditorApplication` (boolean): Whether the menu item can be executed via EditorApplication.ExecuteMenuItem
    - `WarningMessage` (string): Warning message if there are issues with this MenuItem (e.g., duplicate attributes)
  - `TotalCount` (number): Total number of MenuItems discovered before filtering
  - `FilteredCount` (number): Number of MenuItems returned after filtering
  - `AppliedFilter` (string): The filter text that was applied
  - `AppliedFilterType` (string): The filter type that was applied

### 9. execute-menu-item
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
  - `WarningMessage` (string): Warning message if there are issues with this MenuItem (e.g., duplicate attributes)

### 10. execute-dynamic-code
- **Description**: Execute C# code dynamically within Unity Editor. Implements security levels and automatic using statement processing with enhanced error messaging
- **Parameters**:
  - `Code` (string): The C# code to execute (default: "")
  - `Parameters` (Dictionary<string, object>): Runtime parameters for execution (default: {})
  - `CompileOnly` (boolean): Only compile, do not execute (default: false)
- **Response**:
  - `Success` (boolean): Whether execution was successful
  - `Result` (string): Execution result
  - `Logs` (array): Array of log messages
  - `CompilationErrors` (array): Array of compilation errors (if any)
    - `Message` (string): Error message
    - `Line` (number): Line number where error occurred
    - `Column` (number): Column number where error occurred
    - `ErrorCode` (string): Compiler error code (e.g., CS0103)
    - `Hint` (string): Optional hint for resolving the error
    - `Suggestions` (array of string): Suggested fixes (e.g., add using or qualify)
    - `Context` (string): Context lines around the error with a caret pointer
    - `PointerColumn` (number): Pointer column for caret rendering (1-based)
  - `ErrorMessage` (string): Error message (if failed)
  - `SecurityLevel` (string): Current security level ("Disabled", "Restricted", "FullAccess")
  - `UpdatedCode` (string): Updated code (after applying fixes)
  - `DiagnosticsSummary` (string): Summary of diagnostics (unique count, total count, first error brief)
  - `Diagnostics` (array): Structured diagnostics for rich clients (same structure as CompilationErrors)

### 11. focus-window
- **Description**: Brings Unity Editor window to front on macOS and Windows
- **Parameters**: None
- **Response**:
  - `Success` (boolean): Whether the operation was successful
  - `Message` (string): Operation result message
  - `ErrorMessage` (string): Error message if operation failed

### 12. screenshot
- **Description**: Take a screenshot of Unity EditorWindow and save as PNG. Supports capturing any open EditorWindow by name with flexible matching modes
- **Parameters**:
  - `WindowName` (string): Window name to capture (e.g., "Game", "Scene", "Console", "Inspector", "Project", "Hierarchy") (default: "Game")
  - `ResolutionScale` (number): Resolution scale for the captured image, 0.1 to 1.0 (default: 1)
  - `MatchMode` (enum): Window name matching mode (all case-insensitive) - "exact", "prefix", "contains" (default: "exact")
    - `exact`: Window name must match exactly
    - `prefix`: Window name must start with the input
    - `contains`: Window name must contain the input anywhere
  - `OutputDirectory` (string): Output directory path for saving screenshots. When empty, uses default path (.uloop/outputs/Screenshots/). Accepts absolute paths (default: "")
- **Response**:
  - `ScreenshotCount` (number): Number of windows captured
  - `Screenshots` (array): Array of screenshot info
    - `ImagePath` (string): Absolute path to the saved PNG file
    - `FileSizeBytes` (number): Size of the saved file in bytes
    - `Width` (number): Captured image width in pixels
    - `Height` (number): Captured image height in pixels

### 13. control-play-mode
- **Description**: Control Unity Editor play mode (play/stop/pause)
- **Parameters**:
  - `Action` (enum): Action to perform - "Play", "Stop", "Pause" (default: "Play")
    - `Play`: Start play mode (also resumes from pause)
    - `Stop`: Exit play mode and return to edit mode
    - `Pause`: Pause the game while remaining in play mode
- **Response**:
  - `IsPlaying` (boolean): Whether Unity is currently in play mode
  - `IsPaused` (boolean): Whether play mode is paused
  - `Message` (string): Description of the action performed

---

### 14. simulate-mouse
- **Description**: Simulate mouse click, long-press, and drag on PlayMode UI elements via screen coordinates. Uses EventSystem and ExecuteEvents to dispatch pointer events directly — works independently of both old and new Input System
- **Parameters**:
  - `Action` (enum): Mouse action - "Click", "Drag", "DragStart", "DragMove", "DragEnd", "LongPress" (default: "Click")
    - `Click`: Left click at (X, Y). Fires PointerDown → PointerUp → PointerClick
    - `LongPress`: Press and hold at (X, Y) for Duration seconds, then release. No PointerClick fired
    - `Drag`: One-shot drag from (FromX, FromY) to (X, Y). Fires BeginDrag → Drag×N → EndDrag
    - `DragStart`: Begin drag at (X, Y) and hold
    - `DragMove`: Animate from current position to (X, Y) at the specified speed
    - `DragEnd`: Animate to (X, Y), then release drag
  - `X` (number): Target X position in screen pixels, origin: top-left (default: 0)
  - `Y` (number): Target Y position in screen pixels, origin: top-left (default: 0)
  - `FromX` (number): Start X position for Drag action (default: 0)
  - `FromY` (number): Start Y position for Drag action (default: 0)
  - `DragSpeed` (number): Drag speed in pixels per second, 0 for instant (default: 2000)
  - `Duration` (number): Hold duration in seconds for LongPress action (default: 0.5)
- **Response**:
  - `Success` (boolean): Whether the action completed successfully
  - `Message` (string): Description of the action performed
  - `Action` (string): The action that was executed
  - `HitGameObjectName` (string): Name of the UI element hit (null if none)
  - `PositionX` (number): X position used
  - `PositionY` (number): Y position used
  - `EndPositionX` (number): End X position (for Drag actions)
  - `EndPositionY` (number): End Y position (for Drag actions)

### 15. get-unity-search-providers

Get detailed information about available Unity Search providers.

**Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ProviderId` | string | `""` | Specific provider ID (empty = all providers). Examples: `asset`, `scene`, `menu`, `settings` |
| `ActiveOnly` | boolean | `false` | Return only active providers |
| `SortByPriority` | boolean | `true` | Sort by priority (lower = higher priority) |
| `IncludeDescriptions` | boolean | `true` | Include detailed descriptions |

**Response:**

- `Providers` (array): Provider details
  - `Id` (string): Provider identifier
  - `DisplayName` (string): Display name
  - `Description` (string): Provider description
  - `IsActive` (boolean): Whether the provider is active
  - `Priority` (integer): Provider priority
- `TotalCount` (integer): Total number of providers returned
- `ActiveCount` (integer): Number of active providers in the result
- `InactiveCount` (integer): Number of inactive providers in the result
- `Success` (boolean): Whether the request succeeded
- `ErrorMessage` (string): Error message if the request failed
- `AppliedFilter` (string): Filter that was applied
- `SortedByPriority` (boolean): Whether results are sorted by priority

---

## Related Documentation

- [Main README](README.md) - Project overview and setup
- [Architecture Documentation](ARCHITECTURE.md) - Technical architecture details
- [Changelog](CHANGELOG.md) - Version history and updates
