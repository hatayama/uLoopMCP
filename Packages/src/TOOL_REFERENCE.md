# uLoopMCP Tool Reference

This document provides detailed specifications for all uLoopMCP tools.

## Common Parameters & Response Format

All Unity MCP tools share the following common elements:

### Common Parameters
- `TimeoutSeconds` (number): Tool execution timeout in seconds

### Common Response Properties
All tools automatically include the following property:
- `Ver` (string): uLoopMCP server version for CLI compatibility check

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
    - **PlayMode Warning**: During PlayMode test execution, domain reload is temporarily disabled
  - `SaveXml` (boolean): Whether to save test results as XML file (default: false)
    - XML files are saved to `{project root}/.uloop/outputs/TestResults/` folder
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
  - **Small hierarchies** (<=100KB): Direct nested JSON structure
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
    - `hierarchyFilePath` (string): Relative path to saved hierarchy file (e.g., "{project_root}/.uloop/outputs/HierarchyResults/hierarchy_2025-07-10_21-30-15.json")
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

### 11. execute-dynamic-code
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
  - `ErrorMessage` (string): Error message (if failed)
  - `SecurityLevel` (string): Current security level ("Disabled", "Restricted", "FullAccess")
  - `UpdatedCode` (string): Updated code (after applying fixes)

### 12. focus-window
- **Description**: Brings Unity Editor window to front on macOS and Windows
- **Parameters**: None
- **Response**:
  - `Success` (boolean): Whether the operation was successful
  - `Message` (string): Operation result message
  - `ErrorMessage` (string): Error message if operation failed

---

## Related Documentation

- [Main README](README.md) - Project overview and setup
- [Architecture Documentation](ARCHITECTURE.md) - Technical architecture details
- [Changelog](CHANGELOG.md) - Version history and updates
