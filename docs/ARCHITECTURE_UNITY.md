# uLoopMCP Unity Editor-Side Architecture

> Related Documentation: [TypeScript-Side Architecture](ARCHITECTURE_TYPESCRIPT.md) | [Integrated Architecture Overview](ARCHITECTURE.md)

## 1. Overview

This document details the architecture of the C# code within the `Packages/src/Editor` directory. This code runs inside the Unity Editor and serves as the bridge between the Unity environment and the external TypeScript-based MCP (Model-Context-Protocol) server.

### Unity Editor System Architecture Overview

```mermaid
graph TB
    subgraph "Unity Editor (TCP Server + Push Notification Client)"
        MB[McpBridgeServer<br/>TCP Server<br/>McpBridgeServer.cs]
        CMD[Tool System<br/>UnityApiHandler.cs]
        UI[McpEditorWindow<br/>GUI<br/>McpEditorWindow.cs]
        API[Unity APIs]
        SM[McpSessionManager<br/>McpSessionManager.cs]
        PC[UnityPushClient<br/>Push Notification Client<br/>UnityPushClient.cs]
        UPCM[UnityPushConnectionManager<br/>Push Connection Manager<br/>UnityPushConnectionManager.cs]
    end
    
    subgraph "External TypeScript"
        TS[TypeScript Server<br/>MCP Protocol Handler]
        PNS[Push Notification Server<br/>TypeScript Side]
    end
    
    TS <-->|TCP/JSON-RPC<br/>UNITY_TCP_PORT| MB
    TS -->|sendPushNotificationEndpoint| MB
    MB <--> CMD
    CMD <--> API
    UI --> MB
    UI --> CMD
    MB --> SM
    PC <-->|TCP Push Notifications<br/>Random Port| PNS
    UPCM --> PC
    
    classDef server fill:#f3e5f5,stroke:#4a148c,stroke-width:2px
    classDef bridge fill:#fff3e0,stroke:#e65100,stroke-width:2px
    classDef external fill:#e8f5e8,stroke:#2e7d32,stroke-width:2px
    classDef push fill:#e8f5e8,stroke:#2e7d32,stroke-width:2px
    
    class MB,CMD,UI,API,SM,PC,UPCM server
    class TS,PNS external
```

### Primary Responsibilities
1. **Running a TCP Server (`McpBridgeServer`)**: Listens for connections from the TypeScript server to receive tool requests.
2. **Executing Unity Operations**: Processes received tool requests to perform actions within the Unity Editor, such as compiling the project, running tests, or retrieving logs.
3. **Security Management**: Validates and controls tool execution through `McpSecurityChecker` to prevent unauthorized operations.
4. **Session Management**: Maintains client sessions and connection state through `McpSessionManager`.
5. **Providing a User Interface (`McpEditorWindow`)**: Offers a GUI within the Unity Editor for developers to manage and monitor the MCP server.
6. **Managing Configuration**: Handles the setup of `mcp.json` files required by LLM tools like Cursor, Claude, and VSCode.

## 2. Core Architectural Principles

The architecture is built upon several key design principles to ensure robustness, extensibility, and maintainability.

### 2.1. Tool Pattern
The system is centered around the **Tool Pattern**. Each action that can be triggered by an LLM tool is encapsulated in its own tool class.

- **`IUnityTool`**: The common interface for all tools.
- **`AbstractUnityTool<TSchema, TResponse>`**: A generic abstract base class that provides type-safe handling of parameters and responses.
- **`McpToolAttribute`**: Attribute used to mark tools for automatic registration, including Description configuration.
- **`UnityToolRegistry`**: A central registry that discovers and holds all available tools.
- **`UnityApiHandler`**: These classes receive a tool name and parameters, look up the tool in the registry, and execute it.
- **`McpSecurityChecker`**: Validates tool execution permissions based on security settings.

This pattern makes the system highly extensible. To add a new feature, a developer simply needs to create a new class that implements `IUnityTool` and decorate it with the `[McpTool(Description = "...")]` attribute. The system will automatically discover and expose it.

### 2.2. Security Architecture
The system implements comprehensive security controls to prevent unauthorized tool execution:

- **`McpSecurityChecker`**: Central security validation component that checks tool permissions before execution.
- **Attribute-Based Security**: Tools can be decorated with security attributes to define their execution requirements.
- **Default Deny Policy**: Unknown tools are blocked by default to prevent unauthorized operations.
- **Settings-Based Control**: Security policies can be configured through Unity Editor settings interface.

### 2.3. Session Management
The system maintains robust session management to handle client connections and state:

- **`McpSessionManager`**: Singleton session manager implemented as `ScriptableSingleton` for domain reload persistence.
- **Client State Tracking**: Maintains connection state, client identification, and session metadata.
- **Domain Reload Resilience**: Session state survives Unity domain reloads through persistent storage.
- **Reconnection Support**: Handles client reconnection scenarios gracefully.

### 2.4. Tool System Architecture

```mermaid
classDiagram
    class IUnityTool {
        <<interface>>
        +ToolName: string
        +Description: string
        +ParameterSchema: object
        +ExecuteAsync(JToken): Task~object~
    }

    class AbstractUnityTool {
        <<abstract>>
        +ToolName: string
        +Description: string
        +ParameterSchema: object
        +ExecuteAsync(JToken): Task~object~
        #ExecuteAsync(TSchema)*: Task~TResponse~
    }

    class UnityToolRegistry {
        -tools: Dictionary
        +RegisterTool(IUnityTool)
        +GetTool(string): IUnityTool
        +GetAllTools(): IEnumerable
    }

    class McpToolAttribute {
        <<attribute>>
        +Description: string
        +DisplayDevelopmentOnly: bool
        +RequiredSecuritySetting: SecuritySettings
    }

    class CompileTool {
        +ExecuteAsync(CompileSchema): Task~CompileResponse~
    }

    class RunTestsTool {
        +ExecuteAsync(RunTestsSchema): Task~RunTestsResponse~
    }

    IUnityTool <|.. AbstractUnityTool : implements
    AbstractUnityTool <|-- CompileTool : extends
    AbstractUnityTool <|-- RunTestsTool : extends
    UnityToolRegistry --> IUnityTool : manages
    CompileTool ..|> McpToolAttribute : uses
    RunTestsTool ..|> McpToolAttribute : uses
```

### 2.5. MVP + Helper Architecture for UI

```mermaid
classDiagram
    class McpEditorWindow {
        <<Presenter>>
        -model: McpEditorModel
        -view: McpEditorWindowView
        -eventHandler: McpEditorWindowEventHandler
        -serverOperations: McpServerOperations
        +OnEnable()
        +OnGUI()
        +OnDisable()
    }

    class McpEditorModel {
        <<Model>>
        -serverPort: int
        -isServerRunning: bool
        -selectedEditor: EditorType
        +LoadState()
        +SaveState()
        +UpdateServerStatus()
    }

    class McpEditorWindowView {
        <<View>>
        +DrawServerSection(ViewData)
        +DrawConfigSection(ViewData)
        +DrawDeveloperTools(ViewData)
    }

    class McpEditorWindowViewData {
        <<DTO>>
        +ServerPort: int
        +IsServerRunning: bool
        +SelectedEditor: EditorType
    }

    class McpEditorWindowEventHandler {
        <<Helper>>
        +HandleEditorUpdate()
        +HandleServerEvents()
        +HandleLogUpdates()
    }

    class McpServerOperations {
        <<Helper>>
        +StartServer()
        +StopServer()
        +ValidateServerConfig()
    }

    McpEditorWindow --> McpEditorModel : manages state
    McpEditorWindow --> McpEditorWindowView : delegates rendering
    McpEditorWindow --> McpEditorWindowEventHandler : delegates events
    McpEditorWindow --> McpServerOperations : delegates operations
    McpEditorWindowView --> McpEditorWindowViewData : receives
    McpEditorModel --> McpEditorWindowViewData : creates
```

### 2.6. Schema-Driven and Type-Safe Communication
To avoid manual and error-prone JSON parsing, the system uses a schema-driven approach for tools.

- **`*Schema.cs` files** (e.g., `CompileSchema.cs`, `GetLogsSchema.cs`): These classes define the expected parameters for a tool using simple C# properties. Attributes like `[Description]` and default values are used to automatically generate a JSON Schema for the client.
- **`*Response.cs` files** (e.g., `CompileResponse.cs`): These define the structure of the data returned to the client.
- **`ToolParameterSchemaGenerator.cs`**: This utility uses reflection on the `*Schema.cs` files to generate the parameter schema dynamically, ensuring the C# code is the single source of truth.

This design eliminates inconsistencies between the server and client and provides strong type safety within the C# code.

### 2.7. Resilience to Domain Reloads
A significant challenge in the Unity Editor is the "domain reload," which resets the application's state. The architecture handles this gracefully:
- **`McpServerController`**: Uses `[InitializeOnLoad]` to hook into Editor lifecycle events.
- **`AssemblyReloadEvents`**: Before a reload, `OnBeforeAssemblyReload` is used to save the server's running state (port, status) into `SessionState`.
- **`SessionState`**: A Unity Editor feature that persists simple data across domain reloads.
- After a reload, `OnAfterAssemblyReload` reads the `SessionState` and automatically restarts the server if it was previously running, ensuring a seamless experience for the connected client.

## 3. Implemented Tools

The system currently implements 13 production-ready tools, each following the established Tool Pattern architecture:

### 3.1. Core System Tools
- **`ping`**: Connection health check and latency testing
- **`compile`**: Project compilation with detailed error reporting
- **`clear-console`**: Unity Console log clearing with confirmation
- **`set-client-name`**: Client identification and session management
- **`get-tool-details`**: Tool introspection and metadata retrieval

### 3.2. Information Retrieval Tools
- **`get-logs`**: Console log retrieval with filtering and type selection
- **`get-hierarchy`**: Scene hierarchy export with component information
- **`get-menu-items`**: Unity menu item discovery and metadata
- **`get-provider-details`**: Unity Search provider information

### 3.3. GameObject and Scene Tools
- **`find-game-objects`**: Advanced GameObject search with multiple criteria
- **`unity-search`**: Unified search across assets, scenes, and project resources

### 3.4. Execution Tools
- **`run-tests`**: Test execution with NUnit XML export (security-controlled)
- **`execute-menu-item`**: MenuItem execution via reflection (security-controlled)

### 3.5. Security-Controlled Tools
Several tools are subject to security restrictions and can be disabled via settings:
- **Test Execution**: `run-tests` requires "Enable Tests Execution" setting
- **Menu Item Execution**: `execute-menu-item` requires "Allow Menu Item Execution" setting
- **Unknown Tools**: Blocked by default unless explicitly configured

## 4. Key Components (Directory Breakdown)

### `/Server`
This directory contains the core networking and lifecycle management components.

### `/Security`
Contains the security infrastructure for tool execution control.

### `/Api`
This is the heart of the tool processing logic.

### `/Core`
Contains core infrastructure components for session and state management.

### `/UI`
Contains the code for the user-facing Editor Window, implemented using the **MVP (Model-View-Presenter) + Helper Pattern**.

### `/Config`
Manages the creation and modification of `mcp.json` configuration files.

### `/Tools`
Contains higher-level utilities that wrap core Unity Editor functionality.

### `/Utils`
Contains low-level, general-purpose helper classes.

For detailed directory structure and component descriptions, please refer to the [Integrated Architecture Overview](../Packages/src/ARCHITECTURE.md).