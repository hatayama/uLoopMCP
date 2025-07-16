# uLoopMCP Unity Editor-Side Architecture

## 1. Overview

This document details the architecture of the C# code within the `Packages/src/Editor` directory. This code runs inside the Unity Editor and serves as the bridge between the Unity environment and the external TypeScript-based MCP (Model-Context-Protocol) server.

### System Architecture Overview

```mermaid
graph TB
    subgraph "1. LLM Tools (MCP Clients)"
        Claude[Claude Code<br/>MCP Client]
        Cursor[Cursor<br/>MCP Client]
        VSCode[VSCode<br/>MCP Client]
    end
    
    subgraph "2. TypeScript Server (MCP Server + Unity TCP Client)"
        MCP[UnityMcpServer<br/>MCP Protocol Server<br/>server.ts]
        UC[UnityClient<br/>TypeScript TCP Client<br/>unity-client.ts]
        UCM[UnityConnectionManager<br/>Connection Orchestrator<br/>unity-connection-manager.ts]
        UD[UnityDiscovery<br/>Unity Port Scanner<br/>unity-discovery.ts]
    end
    
    subgraph "3. Unity Editor (TCP Server)"
        MB[McpBridgeServer<br/>TCP Server<br/>McpBridgeServer.cs]
        CMD[Tool System<br/>UnityApiHandler.cs]
        UI[McpEditorWindow<br/>GUI<br/>McpEditorWindow.cs]
        API[Unity APIs]
        SM[McpSessionManager<br/>McpSessionManager.cs]
    end
    
    Claude -.->|MCP Protocol<br/>stdio/TCP| MCP
    Cursor -.->|MCP Protocol<br/>stdio/TCP| MCP
    VSCode -.->|MCP Protocol<br/>stdio/TCP| MCP
    
    MCP <--> UC
    UCM --> UC
    UCM --> UD
    UD -.->|Port Discovery<br/>Polling| MB
    UC <-->|TCP/JSON-RPC<br/>Port 8700+| MB
    UC -->|setClientName| MB
    MB <--> CMD
    CMD <--> API
    UI --> MB
    UI --> CMD
    MB --> SM
    
    classDef client fill:#e1f5fe,stroke:#01579b,stroke-width:2px
    classDef server fill:#f3e5f5,stroke:#4a148c,stroke-width:2px
    classDef bridge fill:#fff3e0,stroke:#e65100,stroke-width:2px
    
    class Claude,Cursor,VSCode client
    class MCP,MB server
    class UC,UD,UCM bridge
```

### Client-Server Relationship Breakdown

```mermaid
graph LR
    subgraph "Communication Layers"
        LLM[LLM Tools<br/>CLIENT]
        TS[TypeScript Server<br/>SERVER for MCP<br/>CLIENT for Unity]
        Unity[Unity Editor<br/>SERVER for TCP]
    end
    
    LLM -->|"MCP Protocol<br/>stdio/TCP<br/>Port: Various"| TS
    TS -->|"TCP/JSON-RPC<br/>Port: 8700-9100"| Unity
    
    classDef client fill:#e1f5fe,stroke:#01579b,stroke-width:2px
    classDef server fill:#f3e5f5,stroke:#4a148c,stroke-width:2px
    classDef hybrid fill:#fff3e0,stroke:#e65100,stroke-width:2px
    
    class LLM client
    class Unity server
    class TS hybrid
```

### Protocol and Communication Details

```mermaid
sequenceDiagram
    participant LLM as LLM Tool<br/>(CLIENT)
    participant TS as TypeScript Server<br/>(MCP SERVER)
    participant UC as UnityClient<br/>(TypeScript TCP CLIENT)<br/>unity-client.ts
    participant Unity as Unity Editor<br/>(TCP SERVER)<br/>McpBridgeServer.cs
    
    Note over LLM, Unity: 1. MCP Protocol Layer (stdio/TCP)
    LLM->>TS: MCP initialize request
    TS->>LLM: MCP initialize response
    
    Note over LLM, Unity: 2. TCP Protocol Layer (JSON-RPC)
    LLM->>TS: MCP tools/call request
    TS->>UC: Parse and forward
    UC->>Unity: TCP JSON-RPC request
    Unity->>UC: TCP JSON-RPC response
    UC->>TS: Parse and forward
    TS->>LLM: MCP tools/call response
    
    Note over LLM, Unity: Client-Server Roles:
    Note over LLM: CLIENT: Initiates requests
    Note over TS: SERVER: Serves MCP protocol
    Note over UC: TypeScript TCP CLIENT: Connects to Unity
    Note over Unity: SERVER: Accepts TCP connections
```

### Communication Protocol Summary

| Component | Role | Protocol | Port | Connection Type |
|-----------|------|----------|------|----------------|
| **LLM Tools** (Claude, Cursor, VSCode) | **CLIENT** | MCP Protocol | stdio/Various | Initiates MCP requests |
| **TypeScript Server** | **SERVER** (for MCP)<br/>**CLIENT** (for Unity) | MCP ↔ TCP/JSON-RPC | stdio ↔ 8700-9100 | Bridge between protocols |
| **Unity Editor** | **SERVER** | TCP/JSON-RPC | 8700-9100 | Accepts TCP connections |

### Communication Flow Details

#### Layer 1: LLM Tools ↔ TypeScript Server (MCP Protocol)
- **Protocol**: Model Context Protocol (MCP)
- **Transport**: stdio or TCP
- **Data Format**: JSON-RPC 2.0 with MCP extensions
- **Connection**: LLM tools act as MCP clients
- **Lifecycle**: Managed by LLM tool (Claude, Cursor, VSCode)

#### Layer 2: TypeScript Server ↔ Unity Editor (TCP Protocol)
- **Protocol**: Custom TCP with JSON-RPC 2.0
- **Transport**: TCP Socket
- **Ports**: 8700, 8800, 8900, 9000, 9100, 8600 (auto-discovery)
- **Connection**: TypeScript server acts as TCP client
- **Lifecycle**: Managed by UnityConnectionManager with automatic reconnection

#### Key Architectural Points:
1. **TypeScript Server serves as a Protocol Bridge**: Converts MCP protocol to TCP/JSON-RPC
2. **Unity Editor is the final TCP Server**: Processes tool requests and executes Unity operations
3. **LLM Tools are pure MCP Clients**: Send tool requests through standard MCP protocol
4. **Automatic Discovery**: TypeScript server discovers Unity instances through port scanning

### TCP/JSON-RPC Communication Specification

#### Transport Layer
- **Protocol**: TCP/IP over localhost
- **Default Port**: 8700 (configurable via environment variable)
- **Message Format**: JSON-RPC 2.0 compliant
- **Message Delimiter**: Newline character (`\n`)
- **Buffer Size**: 4096 bytes

#### JSON-RPC 2.0 Message Format

**Request Message:**
```json
{
  "jsonrpc": "2.0",
  "id": 1647834567890,
  "method": "ping",
  "params": {
    "Message": "Hello Unity MCP!"
  }
}
```

**Success Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 1647834567890,
  "result": {
    "Message": "Unity MCP Bridge received: Hello Unity MCP!",
    "ExecutionTimeMs": 5
  }
}
```

**Error Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 1647834567890,
  "error": {
    "code": -32603,
    "message": "Tool blocked by security settings",
    "data": {
      "type": "security_blocked",
      "command": "find-gameobjects",
      "reason": "GameObject search is disabled"
    }
  }
}
```

#### Connection Lifecycle

1. **Initial Connection**
   - TypeScript UnityClient connects to Unity McpBridgeServer
   - TCP socket established on localhost:8700
   - Connection test with ping command

2. **Client Registration**
   - `set-client-name` command sent immediately after connection
   - Client identity stored in Unity session manager
   - UI updated to show connected client

3. **Command Processing**
   - JSON-RPC requests processed through UnityApiHandler
   - Security validation via McpSecurityChecker
   - Tool execution through UnityCommandRegistry

4. **Connection Monitoring**
   - Automatic reconnection on connection loss
   - Periodic health checks via ping commands
   - SafeTimer cleanup on process termination

#### Push Notifications

Unity can send real-time push notifications to all connected TypeScript clients when tools or system state changes occur:

**Notification Format:**
```json
{
  "jsonrpc": "2.0",
  "method": "notifications/tools/list_changed",
  "params": {
    "timestamp": "2025-07-16T12:34:56.789Z",
    "message": "Unity tools have been updated"
  }
}
```

**Notification Triggers:**
- Assembly reloads/recompilation
- Custom tool registration
- Manual tool change notifications via `TriggerToolChangeNotification()`

**Broadcast Mechanism:**
- Sent to all connected clients simultaneously
- Uses same TCP/JSON-RPC communication channel
- Message terminated with newline character (`\n`)

**TypeScript Client Reception:**
```typescript
// TypeScript clients receive notifications via:
socket.on('data', (buffer: Buffer) => {
  const message = buffer.toString('utf8');
  if (message.includes('"method":"notifications/tools/list_changed"')) {
    // Handle tool list update
    this.refreshToolList();
  }
});
```

#### Error Handling

- **SecurityBlocked**: Tool blocked by security settings
- **InternalError**: Unity internal processing errors
- **Timeout**: Network timeout (default: 2 minutes)
- **Connection Loss**: Automatic reconnection with exponential backoff

#### Security Features

- **localhost-only**: External connections blocked
- **Tool-level Security**: McpSecurityChecker validates each command
- **Configurable Access Control**: Unity Editor security settings
- **Session Management**: Client isolation and state tracking

Its primary responsibilities are:
1.  **Running a TCP Server (`McpBridgeServer`)**: Listens for connections from the TypeScript server to receive tool requests.
2.  **Executing Unity Operations**: Processes received tool requests to perform actions within the Unity Editor, such as compiling the project, running tests, or retrieving logs.
3.  **Security Management**: Validates and controls tool execution through `McpSecurityChecker` to prevent unauthorized operations.
4.  **Session Management**: Maintains client sessions and connection state through `McpSessionManager`.
5.  **Providing a User Interface (`McpEditorWindow`)**: Offers a GUI within the Unity Editor for developers to manage and monitor the MCP server.
6.  **Managing Configuration**: Handles the setup of `mcp.json` files required by LLM tools like Cursor, Claude, and VSCode.

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

### 2.4. Command System Architecture

```mermaid
classDiagram
    class IUnityCommand {
        <<interface>>
        +CommandName: string
        +Description: string
        +ParameterSchema: object
        +ExecuteAsync(JToken): Task~object~
    }

    class AbstractUnityCommand {
        <<abstract>>
        +CommandName: string
        +Description: string
        +ParameterSchema: object
        +ExecuteAsync(JToken): Task~object~
        #ExecuteAsync(TSchema)*: Task~TResponse~
    }

    class UnityCommandRegistry {
        -commands: Dictionary
        +RegisterCommand(IUnityCommand)
        +GetCommand(string): IUnityCommand
        +GetAllCommands(): IEnumerable
    }

    class McpToolAttribute {
        <<attribute>>
        +Description: string
        +DisplayDevelopmentOnly: bool
        +RequiredSecuritySetting: SecuritySettings
    }

    class CompileCommand {
        +ExecuteAsync(CompileSchema): Task~CompileResponse~
    }

    class RunTestsCommand {
        +ExecuteAsync(RunTestsSchema): Task~RunTestsResponse~
    }

    IUnityCommand <|.. AbstractUnityCommand : implements
    AbstractUnityCommand <|-- CompileCommand : extends
    AbstractUnityCommand <|-- RunTestsCommand : extends
    UnityCommandRegistry --> IUnityCommand : manages
    CompileCommand ..|> McpToolAttribute : uses
    RunTestsCommand ..|> McpToolAttribute : uses
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
To avoid manual and error-prone JSON parsing, the system uses a schema-driven approach for commands.

- **`*Schema.cs` files** (e.g., `CompileSchema.cs`, `GetLogsSchema.cs`): These classes define the expected parameters for a command using simple C# properties. Attributes like `[Description]` and default values are used to automatically generate a JSON Schema for the client.
- **`*Response.cs` files** (e.g., `CompileResponse.cs`): These define the structure of the data returned to the client.
- **`CommandParameterSchemaGenerator.cs`**: This utility uses reflection on the `*Schema.cs` files to generate the parameter schema dynamically, ensuring the C# code is the single source of truth.

This design eliminates inconsistencies between the server and client and provides strong type safety within the C# code.

### 2.7. TypeScript Server to Unity Connection Architecture

#### 2.7.1. Connection Discovery and Management Components

The system implements a sophisticated connection discovery and management system that handles Unity Editor's frequent restarts and domain reloads:

- **`UnityClient`** (`unity-client.ts`): **TypeScript TCP client** that establishes and maintains connection to Unity Editor
- **`UnityDiscovery`** (`unity-discovery.ts`): **TypeScript singleton discovery service** that locates running Unity instances through port scanning
- **`UnityConnectionManager`** (`unity-connection-manager.ts`): **TypeScript orchestrator** that manages connection lifecycle and state management
- **`SafeTimer`** (`safe-timer.ts`): **TypeScript utility** that ensures proper timer cleanup to prevent orphaned processes

#### 2.7.2. Initial Connection Sequence

```mermaid
sequenceDiagram
    box LLM CLIENT
    participant MCP as MCP Client
    end
    
    box TypeScript MCP SERVER
    participant TS as TypeScript Server<br/>server.ts
    end
    
    box TypeScript Unity CLIENT
    participant UCM as UnityConnectionManager<br/>unity-connection-manager.ts
    participant UD as UnityDiscovery<br/>unity-discovery.ts
    participant UC as UnityClient<br/>unity-client.ts
    end
    
    box Unity TCP SERVER
    participant Unity as Unity Editor<br/>McpBridgeServer.cs
    end
    
    MCP->>TS: initialize (clientInfo.name)
    TS->>TS: Store client name
    TS->>UCM: Initialize connection
    UCM->>UD: Start discovery (1s polling)
    
    loop Every 1 second
        UD->>Unity: Check ports [8700, 8800, 8900, 9000, 9100, 8600]
        Unity-->>UD: Port 8700 responds
    end
    
    UD->>UC: Connect to port 8700
    UC->>Unity: TCP connection established
    UC->>Unity: Send setClientName command
    Unity->>Unity: Update UI with client name
    UD->>UD: Stop discovery (connection successful)
```

#### 2.7.3. Connection Health Monitoring and Reconnection

The system implements a robust reconnection mechanism that handles various failure scenarios:

**Connection State Detection:**
- **Socket Events**: `error`, `close`, `end` events trigger reconnection
- **Health Checks**: Periodic ping commands to verify connection integrity
- **Timeout Handling**: Connection attempts timeout after configured interval

**Reconnection Polling Process:**
1. **Detection Phase**: `UnityDiscovery` detects connection loss
2. **Restart Discovery**: Automatic restart of discovery process with 1-second intervals
3. **Port Scanning**: Systematic scanning of Unity ports (8700, 8800, 8900, 9000, 9100, 8600)
4. **Connection Establishment**: Automatic reconnection when Unity becomes available
5. **State Restoration**: Re-execution of `reconnectHandlers` to restore client state

```mermaid
sequenceDiagram
    box TypeScript CLIENT
    participant UC as UnityClient<br/>unity-client.ts
    participant UD as UnityDiscovery<br/>unity-discovery.ts
    end
    
    box Unity TCP SERVER
    participant Unity as Unity Editor<br/>McpBridgeServer.cs
    participant UI as Unity UI<br/>McpEditorWindow.cs
    end
    
    Note over Unity: Connection Lost (Editor restart/domain reload)
    UC->>UC: Detect connection loss
    UC->>UD: Trigger handleConnectionLost()
    UD->>UD: Restart discovery timer
    
    loop Every 1 second until Unity found
        UD->>Unity: Scan ports [8700, 8800, 8900, 9000, 9100, 8600]
        Unity-->>UD: No response (Unity not ready)
    end
    
    Unity->>Unity: Unity Editor starts
    UD->>Unity: Port scan detects Unity
    UD->>UC: Initiate reconnection
    UC->>Unity: Establish new TCP connection
    UC->>UC: Execute reconnectHandlers
    UC->>Unity: Send setClientName command
    Unity->>UI: Update client display
```

#### 2.7.4. SafeTimer Implementation for Process Cleanup

The system uses a custom `SafeTimer` class to prevent orphaned processes:

**Features:**
- **Automatic Cleanup**: Registers process exit handlers to clean up timers
- **Singleton Pattern**: Prevents multiple timer instances for the same operation
- **Development Monitoring**: Tracks active timer count for debugging
- **Graceful Shutdown**: Ensures timers are properly disposed on process termination

**Implementation Details:**
```typescript
// Automatic cleanup on process exit
process.on('exit', () => SafeTimer.cleanup());
process.on('SIGINT', () => SafeTimer.cleanup());
process.on('SIGTERM', () => SafeTimer.cleanup());
```

#### 2.7.5. Client Identification Flow

The system ensures proper client identification to prevent "Unknown Client" display issues:

1. **Initial State**: Unity Editor shows "No connected tools found" when no clients are connected.
2. **MCP Client Connection**: When an MCP client (Cursor, Claude, VSCode) connects:
   - Client sends `initialize` request with `clientInfo.name`
   - TypeScript server receives and stores the client name
   - Only then does TypeScript server connect to Unity
3. **Unity Connection**: TypeScript server immediately sends `setClientName` command
4. **UI Update**: Unity UI displays the correct client name from the first connection

This flow prevents the temporary "Unknown Client" display that would occur if the TypeScript server connected to Unity before receiving the client name.

### 2.8. SOLID Principles
- **Single Responsibility Principle (SRP)**: Each class has a well-defined responsibility.
    - `McpBridgeServer`: Handles raw TCP communication.
    - `McpServerController`: Manages the server's lifecycle and state across domain reloads.
    - `McpConfigRepository`: Handles file I/O for configuration.
    - `McpConfigService`: Implements the business logic for configuration.
    - `JsonRpcProcessor`: Deals exclusively with parsing and formatting JSON-RPC 2.0 messages.
    - **UI Layer Examples**:
        - `McpEditorModel`: Manages application state and business logic only.
        - `McpEditorWindowView`: Handles UI rendering only.
        - `McpEditorWindowEventHandler`: Manages Unity Editor events only.
        - `McpServerOperations`: Handles server operations only.
- **Open/Closed Principle (OCP)**: The system is open for extension but closed for modification. The Command Pattern is the prime example; new commands can be added without altering the core execution logic. The MVP + Helper pattern also demonstrates this principle - new functionality can be added by creating new helper classes without modifying existing components.

### 2.9. MVP + Helper Pattern for UI Architecture
The UI layer implements a sophisticated **MVP (Model-View-Presenter) + Helper Pattern** that evolved from a monolithic 1247-line class into a well-structured, maintainable architecture.

#### Pattern Components
- **Model (`McpEditorModel`)**: Contains all application state, configuration data, and business logic. Provides methods for state updates while maintaining encapsulation. Handles persistence through Unity's `SessionState` and `EditorPrefs`.
- **View (`McpEditorWindowView`)**: Pure UI rendering component with no business logic. Receives all necessary data through `McpEditorWindowViewData` transfer objects.
- **Presenter (`McpEditorWindow`)**: Coordinates between Model and View, handles Unity-specific lifecycle events, and delegates complex operations to specialized helper classes.
- **Helper Classes**: Specialized components that handle specific aspects of functionality:
  - Event management (`McpEditorWindowEventHandler`)
  - Server operations (`McpServerOperations`)
  - Configuration services (`McpConfigServiceFactory`)

#### Benefits of This Architecture
1. **Separation of Concerns**: Each component has a single, clear responsibility
2. **Testability**: Helper classes can be unit tested independently from Unity Editor context
3. **Maintainability**: Complex logic is broken down into manageable, focused components
4. **Extensibility**: New features can be added through new helper classes without modifying existing code
5. **Reduced Cognitive Load**: Developers can focus on one aspect of functionality at a time

#### Implementation Guidelines
- **State Management**: All state changes go through the Model layer
- **UI Updates**: View receives data through transfer objects, never directly accesses Model
- **Complex Operations**: Delegate to appropriate helper classes rather than implementing in Presenter
- **Event Handling**: Isolate all Unity Editor event management in dedicated EventHandler

### 2.10. Resilience to Domain Reloads
A significant challenge in the Unity Editor is the "domain reload," which resets the application's state. The architecture handles this gracefully:
- **`McpServerController`**: Uses `[InitializeOnLoad]` to hook into Editor lifecycle events.
- **`AssemblyReloadEvents`**: Before a reload, `OnBeforeAssemblyReload` is used to save the server's running state (port, status) into `SessionState`.
- **`SessionState`**: A Unity Editor feature that persists simple data across domain reloads.
- After a reload, `OnAfterAssemblyReload` reads the `SessionState` and automatically restarts the server if it was previously running, ensuring a seamless experience for the connected client.

## 3. Implemented Commands

The system currently implements 13 production-ready commands, each following the established Command Pattern architecture:

### 3.1. Core System Commands
- **`PingCommand`**: Connection health check and latency testing
- **`CompileCommand`**: Project compilation with detailed error reporting
- **`ClearConsoleCommand`**: Unity Console log clearing with confirmation
- **`SetClientNameCommand`**: Client identification and session management
- **`GetCommandDetailsCommand`**: Command introspection and metadata retrieval

### 3.2. Information Retrieval Commands
- **`GetLogsCommand`**: Console log retrieval with filtering and type selection
- **`GetHierarchyCommand`**: Scene hierarchy export with component information
- **`GetMenuItemsCommand`**: Unity menu item discovery and metadata
- **`GetProviderDetailsCommand`**: Unity Search provider information

### 3.3. GameObject and Scene Commands
- **`FindGameObjectsCommand`**: Advanced GameObject search with multiple criteria
- **`UnitySearchCommand`**: Unified search across assets, scenes, and project resources

### 3.4. Execution Commands
- **`RunTestsCommand`**: Test execution with NUnit XML export (security-controlled)
- **`ExecuteMenuItemCommand`**: MenuItem execution via reflection (security-controlled)

### 3.5. Security-Controlled Commands
Several commands are subject to security restrictions and can be disabled via settings:
- **Test Execution**: `RunTestsCommand` requires "Enable Tests Execution" setting
- **Menu Item Execution**: `ExecuteMenuItemCommand` requires "Allow Menu Item Execution" setting
- **Unknown Commands**: Blocked by default unless explicitly configured

## 4. Key Components (Directory Breakdown)

### `/Server`
This directory contains the core networking and lifecycle management components.
- **`McpBridgeServer.cs`**: The low-level TCP server. It listens on a specified port, accepts client connections, and handles the reading/writing of JSON data over the network stream. It operates on a background thread.
- **`McpServerController.cs`**: The high-level, static manager for the server. It controls the lifecycle (Start, Stop, Restart) of the `McpBridgeServer` instance. It is the central point for managing state across domain reloads.
- **`McpServerConfig.cs`**: A static class holding constants for server configuration (e.g., default port, buffer sizes).

### `/Security`
Contains the security infrastructure for command execution control.
- **`McpSecurityChecker.cs`**: Central security validation component that implements permission checking for command execution. Evaluates security attributes and settings to determine if a command should be allowed to execute.

### `/Api`
This is the heart of the command processing logic.
- **`/Commands`**: Contains the implementation of all supported commands.
    - **`/Core`**: The foundational classes for the command system.
        - **`IUnityCommand.cs`**: Defines the contract for all commands, including `CommandName`, `Description`, `ParameterSchema`, and the `ExecuteAsync` method.
        - **`AbstractUnityCommand.cs`**: The generic base class that simplifies command creation by handling the boilerplate of parameter deserialization and response creation.
        - **`UnityCommandRegistry.cs`**: Discovers all classes with the `[McpTool]` attribute and registers them in a dictionary, mapping a command name to its implementation.
        - **`McpToolAttribute.cs`**: A simple attribute used to mark a class for automatic registration as a command.
    - **Command-specific folders**: Each of the 13 implemented commands has its own folder containing:
        - `*Command.cs`: The main command implementation
        - `*Schema.cs`: Type-safe parameter definition
        - `*Response.cs`: Structured response format
        - Commands include: `/Compile`, `/RunTests`, `/GetLogs`, `/Ping`, `/ClearConsole`, `/FindGameObjects`, `/GetHierarchy`, `/GetMenuItems`, `/ExecuteMenuItem`, `/SetClientName`, `/UnitySearch`, `/GetProviderDetails`, `/GetCommandDetails`
- **`JsonRpcProcessor.cs`**: Responsible for parsing incoming JSON strings into `JsonRpcRequest` objects and serializing response objects back into JSON strings, adhering to the JSON-RPC 2.0 specification.
- **`UnityApiHandler.cs`**: The entry point for API calls. It receives the method name and parameters from the `JsonRpcProcessor` and uses the `UnityCommandRegistry` to execute the appropriate command. Integrates with `McpSecurityChecker` for permission validation.

### `/Core`
Contains core infrastructure components for session and state management.
- **`McpSessionManager.cs`**: Singleton session manager implemented as `ScriptableSingleton` that maintains client connection state, session metadata, and survives domain reloads. Provides centralized client identification and connection tracking.

### `/UI`
Contains the code for the user-facing Editor Window, implemented using the **MVP (Model-View-Presenter) + Helper Pattern**.

#### Core MVP Components
- **`McpEditorWindow.cs`**: The **Presenter** layer (503 lines). Acts as the coordinator between the Model and View, handling Unity-specific lifecycle events and user interactions. Delegates complex operations to specialized helper classes.
- **`McpEditorModel.cs`**: The **Model** layer (470 lines). Manages all application state, persistence, and business logic. Contains UI state, server configuration, and provides methods for state updates with proper encapsulation.
- **`McpEditorWindowView.cs`**: The **View** layer. Handles pure UI rendering logic, completely separated from business logic. Receives data through `McpEditorWindowViewData` and renders the interface.
- **`McpEditorWindowViewData.cs`**: Data transfer object that carries all necessary information from the Model to the View, ensuring clean separation of concerns.

#### Specialized Helper Classes
- **`McpEditorWindowEventHandler.cs`**: Manages Unity Editor events (194 lines). Handles `EditorApplication.update`, `McpCommunicationLogger.OnLogUpdated`, server connection events, and state change detection. Completely isolates event management logic from the main window.
- **`McpServerOperations.cs`**: Handles complex server operations (131 lines). Contains server validation, starting, and stopping logic. Supports both user-interactive and internal operation modes with comprehensive error handling.
- **`McpCommunicationLog.cs`**: Manages the in-memory and `SessionState`-backed log of requests and responses displayed in the "Developer Tools" section of the window.

#### Architectural Benefits
This MVP + Helper pattern provides:
- **Single Responsibility**: Each class has one clear, focused responsibility
- **Testability**: Helper classes can be unit tested independently
- **Maintainability**: Complex logic is separated into specialized, manageable components
- **Extensibility**: New features can be added by creating new helper classes without modifying existing code
- **Reduced Complexity**: The main Presenter went from 1247 lines to 503 lines (59% reduction) through proper responsibility distribution

### `/Config`
Manages the creation and modification of `mcp.json` configuration files.
- **`UnityMcpPathResolver.cs`**: A utility to find the correct path for configuration files for different editors (Cursor, VSCode, etc.).
- **`McpConfigRepository.cs`**: Handles the direct reading and writing of the `mcp.json` file.
- **`McpConfigService.cs`**: Contains the logic for auto-configuring the `mcp.json` file with the correct command, arguments, and environment variables based on the user's settings in the `McpEditorWindow`.

### `/Tools`
Contains higher-level utilities that wrap core Unity Editor functionality.
- **`/ConsoleUtility` & `/ConsoleLogFetcher`**: A set of classes, primarily `ConsoleLogRetriever`, that use reflection to access Unity's internal console log entries. This allows the `getlogs` command to retrieve logs with specific types and filters.
- **`/TestRunner`**: Contains the logic for executing Unity tests.
    - **`PlayModeTestExecuter.cs`**: A key class that handles the complexity of running PlayMode tests, which involves disabling domain reloads (`DomainReloadDisableScope`) to ensure the `async` task can complete successfully.
    - **`NUnitXmlResultExporter.cs`**: Formats test results into NUnit-compatible XML files.
- **`/Util`**: General-purpose utilities.
    - **`CompileController.cs`**: Wraps the `CompilationPipeline` API to provide a simple `async` interface for compiling the project.

### `/Utils`
Contains low-level, general-purpose helper classes.
- **`MainThreadSwitcher.cs`**: A crucial utility that provides an `awaitable` object to switch execution from a background thread (like the TCP server's) back to Unity's main thread. This is essential because most Unity APIs can only be called from the main thread.
- **`EditorDelay.cs`**: A custom, `async/await`-compatible implementation of a frame-based delay, useful for waiting a few frames for the Editor to reach a stable state, especially after domain reloads.
- **`McpLogger.cs`**: A simple, unified logging wrapper to prefix all package-related logs with `[uLoopMCP]`.

## 5. Key Workflows

### 5.1. Command Execution Flow with Security

```mermaid
sequenceDiagram
    box TypeScript CLIENT
    participant TS as TypeScript Client<br/>unity-client.ts
    end
    
    box Unity TCP SERVER
    participant MB as McpBridgeServer<br/>McpBridgeServer.cs
    participant JP as JsonRpcProcessor<br/>JsonRpcProcessor.cs
    participant UA as UnityApiHandler<br/>UnityApiHandler.cs
    participant SC as McpSecurityChecker<br/>McpSecurityChecker.cs
    participant UR as UnityCommandRegistry<br/>UnityCommandRegistry.cs
    participant AC as AbstractUnityCommand<br/>AbstractUnityCommand.cs
    participant CC as ConcreteCommand<br/>*Command.cs
    participant UT as Unity Tool<br/>(CompileController etc)
    end

    TS->>MB: JSON String
    MB->>JP: ProcessRequest(json)
    JP->>JP: Deserialize to JsonRpcRequest
    JP->>UA: ExecuteCommandAsync(name, params)
    UA->>SC: ValidateCommand(name, params)
    alt Security Check Passed
        SC-->>UA: Validation Success
        UA->>UR: GetCommand(name)
        UR-->>UA: IUnityCommand instance
        UA->>AC: ExecuteAsync(JToken)
        AC->>AC: Deserialize to Schema
        AC->>CC: ExecuteAsync(Schema)
        CC->>UT: Execute Unity API
        UT-->>CC: Result
        CC-->>AC: Response object
        AC-->>UA: Response
    else Security Check Failed
        SC-->>UA: Validation Failed
        UA-->>UA: Create Error Response
    end
    UA-->>JP: Response
    JP->>JP: Serialize to JSON
    JP-->>MB: JSON Response
    MB-->>TS: Send Response
```

### 5.2. UI Interaction Flow (MVP + Helper Pattern)
1.  **User Interaction**: User interacts with the Unity Editor window (button clicks, field changes, etc.).
2.  **Presenter Processing**: `McpEditorWindow` (Presenter) receives the Unity Editor event.
3.  **State Update**: Presenter calls appropriate method on `McpEditorModel` to update application state.
4.  **Complex Operations**: For complex operations (server start/stop, validation), Presenter delegates to specialized helper classes:
    - `McpServerOperations` for server-related operations
    - `McpEditorWindowEventHandler` for event management
    - `McpConfigServiceFactory` for configuration operations
5.  **View Data Preparation**: Model state is packaged into `McpEditorWindowViewData` transfer objects.
6.  **UI Rendering**: `McpEditorWindowView` receives the transfer objects and renders the interface.
7.  **Event Propagation**: `McpEditorWindowEventHandler` manages Unity Editor events and updates the Model accordingly.
8.  **Persistence**: Model automatically handles state persistence through Unity's `SessionState` and `EditorPrefs`.

This workflow ensures clean separation of concerns while maintaining responsiveness and proper state management throughout the application lifecycle.

### 5.3. TypeScript-Unity Connection Lifecycle

#### 5.3.1. Complete Connection Establishment Flow

```mermaid
sequenceDiagram
    box LLM CLIENT
    participant MCP as MCP Client
    end
    
    box TypeScript MCP SERVER
    participant TS as TypeScript Server<br/>server.ts
    end
    
    box TypeScript Unity CLIENT
    participant UCM as UnityConnectionManager<br/>unity-connection-manager.ts
    participant UD as UnityDiscovery<br/>unity-discovery.ts
    participant UC as UnityClient<br/>unity-client.ts
    end
    
    box Unity TCP SERVER
    participant Unity as Unity Editor
    participant MB as McpBridgeServer<br/>McpBridgeServer.cs
    participant SM as McpSessionManager<br/>McpSessionManager.cs
    participant UI as McpEditorWindow<br/>McpEditorWindow.cs
    end
    
    MCP->>TS: initialize (clientInfo.name)
    TS->>TS: Store client name
    TS->>UCM: Initialize connection
    UCM->>UD: Start discovery (1s polling)
    
    loop Unity Discovery
        UD->>Unity: Port scan [8700, 8800, 8900, 9000, 9100, 8600]
        Unity-->>UD: Port 8700 responds
    end
    
    UD->>UC: Connect to Unity
    UC->>Unity: TCP connection request
    Unity->>MB: Accept connection
    MB->>MB: Create ConnectedClient
    MB->>SM: RegisterClient(clientInfo)
    SM->>SM: Store session state
    
    UC->>Unity: Send setClientName command
    Unity->>SM: Update client name
    SM->>UI: Update client display
    UI->>UI: Show connected client
    
    UD->>UD: Stop discovery (success)
```

#### 5.3.2. Connection Loss Detection and Recovery

```mermaid
sequenceDiagram
    box TypeScript CLIENT
    participant UC as UnityClient<br/>unity-client.ts
    participant UD as UnityDiscovery<br/>unity-discovery.ts
    end
    
    box Unity TCP SERVER
    participant Unity as Unity Editor
    participant MB as McpBridgeServer<br/>McpBridgeServer.cs
    participant SM as McpSessionManager<br/>McpSessionManager.cs
    participant UI as McpEditorWindow<br/>McpEditorWindow.cs
    end
    
    Note over Unity: Connection Lost (Editor restart/domain reload)
    
    Unity->>MB: Connection terminated
    MB->>SM: Client disconnected
    SM->>UI: Update connection status
    UI->>UI: Show "No connected tools found"
    
    UC->>UC: Detect socket error/close
    UC->>UD: Trigger handleConnectionLost()
    UD->>UD: Restart discovery timer
    
    loop Every 1 second
        UD->>Unity: Port scan [8700, 8800, 8900, 9000, 9100, 8600]
        Unity-->>UD: No response (Unity not ready)
    end
    
    Note over Unity: Unity Editor restarts
    Unity->>Unity: Initialize McpBridgeServer
    
    UD->>Unity: Port scan detects Unity
    UD->>UC: Initiate reconnection
    UC->>Unity: Establish new TCP connection
    Unity->>MB: Accept reconnection
    MB->>SM: Register reconnected client
    
    UC->>UC: Execute reconnectHandlers
    UC->>Unity: Send setClientName command
    Unity->>SM: Update client name
    SM->>UI: Update client display
    UI->>UI: Show reconnected client
```

#### 5.3.3. Session Management with Domain Reload Resilience

```mermaid
sequenceDiagram
    box TypeScript Unity CLIENT
    participant UC as UnityClient<br/>unity-client.ts
    end
    
    box Unity TCP SERVER
    participant MB as McpBridgeServer<br/>McpBridgeServer.cs
    participant SM as McpSessionManager<br/>McpSessionManager.cs
    participant UI as McpEditorWindow<br/>McpEditorWindow.cs
    participant Unity as Unity Editor
    participant SC as McpServerController<br/>McpServerController.cs
    end
    
    UC->>MB: Connect + SetClientName
    MB->>SM: RegisterClient(clientInfo)
    SM->>SM: Store session state
    SM->>UI: Update client display
    UI->>UI: Show connected client
    
    Note over Unity: Domain Reload Triggered
    Unity->>SC: OnBeforeAssemblyReload
    SC->>SC: Save server state to SessionState
    SC->>MB: Stop server gracefully
    MB->>SM: Persist session data
    SM->>SM: Store client info to SessionState
    
    Note over Unity: Domain Reload Occurs
    Unity->>SC: OnAfterAssemblyReload
    SC->>SC: Read server state from SessionState
    SC->>MB: Restart server if was running
    MB->>SM: Restore session manager
    SM->>SM: Restore client info from SessionState
    SM->>UI: Update UI with restored state
    
    Note over UC: TypeScript detects connection loss
    UC->>UC: Trigger reconnection process
    UC->>MB: Reconnect to Unity
    MB->>SM: Update session with reconnected client
    SM->>UI: Update client display
```

#### 5.3.4. Multi-Client Session Management

```mermaid
sequenceDiagram
    box LLM CLIENTS
    participant C1 as Claude Client
    participant C2 as Cursor Client
    end
    
    box TypeScript MCP SERVER
    participant TS as TypeScript Server<br/>server.ts
    end
    
    box TypeScript Unity CLIENT
    participant UC as UnityClient<br/>unity-client.ts
    end
    
    box Unity TCP SERVER
    participant MB as McpBridgeServer<br/>McpBridgeServer.cs
    participant SM as McpSessionManager<br/>McpSessionManager.cs
    participant UI as McpEditorWindow<br/>McpEditorWindow.cs
    end
    
    C1->>TS: initialize (clientInfo.name = "Claude")
    TS->>UC: Setup connection with client name
    UC->>MB: Connect + SetClientName("Claude")
    MB->>SM: RegisterClient("Claude")
    SM->>UI: Show "Claude" connected
    
    C2->>TS: initialize (clientInfo.name = "Cursor")
    TS->>UC: Setup connection with client name
    UC->>MB: Connect + SetClientName("Cursor")
    MB->>SM: RegisterClient("Cursor")
    SM->>SM: Replace previous client info
    SM->>UI: Show "Cursor" connected
    
    Note over SM: Only latest client is displayed
    Note over SM: Session state tracks last connected client
```

### 5.4. Connection Resilience and Recovery Patterns

#### 5.4.1. Connection State Management

The system maintains connection state through multiple layers:

**TypeScript Side State Tracking:**
- `UnityClient._connected`: Boolean flag indicating active connection
- `UnityDiscovery.isRunning`: Controls discovery process lifecycle
- `reconnectHandlers`: Array of functions executed on reconnection

**Unity Side State Tracking:**
- `McpBridgeServer.connectedClients`: Concurrent dictionary of active connections
- `McpSessionManager`: Persistent session state across domain reloads
- `McpServerController`: Static server lifecycle management

#### 5.4.2. Recovery Mechanisms

**Graceful Degradation:**
- Commands continue to queue during connection loss
- UI displays appropriate connection status
- Background processes maintain state integrity

**Automatic Recovery:**
- 1-second polling interval for Unity discovery
- Exponential backoff for connection attempts
- State restoration through `reconnectHandlers`

**Error Handling:**
- Socket-level error detection and logging
- Timeout handling for connection attempts
- Graceful handling of Unity Editor crashes

#### 5.4.3. Port Management Strategy

The system uses a systematic port discovery approach:

**Port Range:** `[8700, 8800, 8900, 9000, 9100, 8600]`
**Discovery Strategy:**
1. Start with default port (8700)
2. Increment by 100 for additional instances
3. Fall back to 8600 as final attempt

**Port Conflict Resolution:**
- Automatic port selection based on availability
- Support for multiple Unity instances
- Environment variable override capability

### 5.5. Security Validation Flow

```mermaid
sequenceDiagram
    box Unity TCP SERVER
    participant UA as UnityApiHandler<br/>UnityApiHandler.cs
    participant SC as McpSecurityChecker<br/>McpSecurityChecker.cs
    participant Settings as Security Settings
    participant Command as Command Instance<br/>*Command.cs
    end
    
    UA->>SC: ValidateCommand(commandName)
    SC->>Settings: Check security policy
    alt Command is security-controlled
        Settings-->>SC: Security status
        alt Security disabled
            SC-->>UA: Validation Failed
        else Security enabled
            SC-->>UA: Validation Success
        end
    else Command is not security-controlled
        SC-->>UA: Validation Success
    end
    UA->>Command: Execute (if validated)
```

### 5.6. Implementation Notes

#### 5.6.1. TypeScript Implementation Details

**Key Classes Location:**
- `UnityClient`: `/Packages/src/TypeScriptServer~/src/unity-client.ts`
- `UnityDiscovery`: `/Packages/src/TypeScriptServer~/src/unity-discovery.ts`
- `UnityConnectionManager`: `/Packages/src/TypeScriptServer~/src/unity-connection-manager.ts`
- `SafeTimer`: `/Packages/src/TypeScriptServer~/src/safe-timer.ts`

**Critical Implementation Features:**
- **Singleton Pattern**: `UnityDiscovery` prevents multiple discovery instances
- **Event-Driven Architecture**: Socket events trigger state changes
- **Process Cleanup**: `SafeTimer` ensures no orphaned processes
- **Error Resilience**: Comprehensive error handling and recovery

#### 5.6.2. Unity C# Implementation Details

**Key Classes Location:**
- `McpBridgeServer`: `/Packages/src/Editor/Server/McpBridgeServer.cs`
- `McpServerController`: `/Packages/src/Editor/Server/McpServerController.cs`
- `McpSessionManager`: `/Packages/src/Editor/Core/McpSessionManager.cs`

**Critical Implementation Features:**
- **Thread Safety**: Concurrent collections for client management
- **Domain Reload Resilience**: `SessionState` persistence
- **Lifecycle Management**: `[InitializeOnLoad]` attribute for automatic startup
- **Client Isolation**: Individual thread handling for each client connection

---

# TypeScript Server Architecture

## 6. TypeScript Server Overview

The TypeScript server located in `Packages/src/TypeScriptServer~` acts as the intermediary between MCP-compatible clients (like Cursor, Claude, or VSCode) and the Unity Editor. It runs as a Node.js process, communicates with clients via standard I/O (stdio) using the Model Context Protocol (MCP), and relays tool requests to the Unity Editor via TCP socket connections.

### Primary Responsibilities
1. **MCP Server Implementation**: Implements the MCP server specification using `@modelcontextprotocol/sdk` to handle requests from clients (e.g., `tools/list`, `tools/call`)
2. **Dynamic Tool Management**: Fetches available tools from Unity Editor and dynamically creates corresponding "tools" to expose to MCP clients
3. **Unity Communication**: Manages persistent TCP connections to the `McpBridgeServer` running inside Unity Editor
4. **Tool Forwarding**: Translates `tools/call` requests from MCP clients into JSON-RPC requests and sends them to Unity server for execution
5. **Notification Handling**: Listens for `notifications/tools/list_changed` events from Unity to automatically refresh toolset when tools are added or removed

## 7. TypeScript Server Architecture Diagrams

### 7.1. TypeScript System Overview

```mermaid
graph TB
    subgraph "MCP Clients"
        Claude[Claude]
        Cursor[Cursor]
        VSCode[VSCode]
        Codeium[Codeium]
    end
    
    subgraph "TypeScript Server (Node.js Process)"
        MCP[UnityMcpServer<br/>MCP Protocol Handler<br/>server.ts]
        UCM[UnityConnectionManager<br/>Connection Lifecycle<br/>unity-connection-manager.ts]
        UTM[UnityToolManager<br/>Dynamic Tool Management<br/>unity-tool-manager.ts]
        MCC[McpClientCompatibility<br/>Client-Specific Behavior<br/>mcp-client-compatibility.ts]
        UEH[UnityEventHandler<br/>Event Processing<br/>unity-event-handler.ts]
        UC[UnityClient<br/>TCP Communication<br/>unity-client.ts]
        UD[UnityDiscovery<br/>Unity Instance Discovery<br/>unity-discovery.ts]
        CM[ConnectionManager<br/>Connection State<br/>connection-manager.ts]
        MH[MessageHandler<br/>JSON-RPC Processing<br/>message-handler.ts]
        Tools[DynamicUnityCommandTool<br/>Tool Instances<br/>dynamic-unity-command-tool.ts]
    end
    
    subgraph "Unity Editor"
        Bridge[McpBridgeServer<br/>TCP Server<br/>McpBridgeServer.cs]
    end
    
    Claude -.->|MCP Protocol<br/>stdio| MCP
    Cursor -.->|MCP Protocol<br/>stdio| MCP
    VSCode -.->|MCP Protocol<br/>stdio| MCP
    Codeium -.->|MCP Protocol<br/>stdio| MCP
    
    MCP --> UCM
    MCP --> UTM
    MCP --> MCC
    MCP --> UEH
    UCM --> UC
    UCM --> UD
    UTM --> UC
    UTM --> Tools
    UEH --> UC
    UEH --> UCM
    UC --> CM
    UC --> MH
    UC --> UD
    UD --> UC
    UC -->|TCP/JSON-RPC<br/>Port 8700+| Bridge
```

### 7.2. TypeScript Class Relationships

```mermaid
classDiagram
    class UnityMcpServer {
        -server: Server
        -unityClient: UnityClient
        -connectionManager: UnityConnectionManager
        -toolManager: UnityToolManager
        -clientCompatibility: McpClientCompatibility
        -eventHandler: UnityEventHandler
        +start()
        +setupHandlers()
        +handleInitialize()
        +handleListTools()
        +handleCallTool()
    }

    class UnityConnectionManager {
        -unityClient: UnityClient
        -unityDiscovery: UnityDiscovery
        -isDevelopment: boolean
        -isInitialized: boolean
        +getUnityDiscovery()
        +waitForUnityConnectionWithTimeout()
        +handleUnityDiscovered()
        +initialize()
        +setupReconnectionCallback()
        +isConnected()
        +disconnect()
    }

    class UnityToolManager {
        -unityClient: UnityClient
        -isDevelopment: boolean
        -dynamicTools: Map<string, DynamicUnityCommandTool>
        -isRefreshing: boolean
        -clientName: string
        +setClientName()
        +getDynamicTools()
        +getAllTools()
        +getTool()
        +hasTool()
        +initializeDynamicTools()
        +refreshDynamicToolsSafe()
        +fetchCommandDetailsFromUnity()
        +createDynamicToolsFromCommands()
        +getToolsFromUnity()
    }

    class McpClientCompatibility {
        -unityClient: UnityClient
        -clientName: string
        -isDevelopment: boolean
        +setClientName()
        +getClientName()
        +isListChangedUnsupported()
        +handleClientNameInitialization()
        +initializeClient()
        +logClientCompatibility()
    }

    class UnityEventHandler {
        -server: Server
        -unityClient: UnityClient
        -connectionManager: UnityConnectionManager
        -toolManager: UnityToolManager
        -lastNotificationTime: number
        +setupUnityEventListener()
        +sendToolsChangedNotification()
        +setupSignalHandlers()
        +gracefulShutdown()
    }

    class UnityClient {
        -socket: Socket
        -connectionManager: ConnectionManager
        -messageHandler: MessageHandler
        -unityDiscovery: UnityDiscovery
        -_connected: boolean
        +connect()
        +disconnect()
        +executeCommand()
        +ensureConnected()
        +isConnected()
        +getConnectionManager()
        +getMessageHandler()
    }

    class UnityDiscovery {
        <<singleton>>
        -static instance: UnityDiscovery
        -discoveryTimer: SafeTimer
        -isRunning: boolean
        -onUnityDiscovered: Function
        -onConnectionLost: Function
        +static getInstance()
        +startDiscovery()
        +stopDiscovery()
        +handleConnectionLost()
        +setCallbacks()
        -scanForUnity()
    }

    class ConnectionManager {
        -onReconnectedCallback: Function
        -onConnectionLostCallback: Function
        +setReconnectedCallback()
        +setConnectionLostCallback()
        +triggerReconnected()
        +triggerConnectionLost()
    }

    class MessageHandler {
        -notificationHandlers: Map<string, Function>
        -pendingRequests: Map<number, PendingRequest>
        +handleIncomingData()
        +createRequest()
        +registerPendingRequest()
        +clearPendingRequests()
        +registerNotificationHandler()
    }

    class BaseTool {
        <<abstract>>
        #context: ToolContext
        +handle()
        #validateArgs()*
        #execute()*
        #formatResponse()
    }

    class DynamicUnityCommandTool {
        +name: string
        +description: string
        +inputSchema: object
        +execute()
        -hasNoParameters()
        -generateInputSchema()
    }

    class ToolContext {
        +unityClient: UnityClient
        +clientName: string
        +isDevelopment: boolean
    }

    UnityMcpServer "1" --> "1" UnityConnectionManager : orchestrates
    UnityMcpServer "1" --> "1" UnityToolManager : manages
    UnityMcpServer "1" --> "1" McpClientCompatibility : handles
    UnityMcpServer "1" --> "1" UnityEventHandler : processes
    UnityMcpServer "1" --> "1" UnityClient : communicates
    UnityConnectionManager "1" --> "1" UnityClient : controls
    UnityConnectionManager "1" --> "1" UnityDiscovery : uses
    UnityToolManager "1" --> "1" UnityClient : executes
    UnityToolManager "1" --> "*" DynamicUnityCommandTool : creates
    McpClientCompatibility "1" --> "1" UnityClient : configures
    UnityEventHandler "1" --> "1" UnityClient : listens
    UnityEventHandler "1" --> "1" UnityConnectionManager : coordinates
    UnityEventHandler "1" --> "1" UnityToolManager : refreshes
    UnityClient "1" --> "1" ConnectionManager : delegates
    UnityClient "1" --> "1" MessageHandler : delegates
    UnityClient "1" --> "1" UnityDiscovery : uses
    DynamicUnityCommandTool --|> BaseTool : extends
    DynamicUnityCommandTool --> ToolContext : uses
    ToolContext --> UnityClient : references
```

### 7.3. TypeScript Tool Execution Sequence

```mermaid
sequenceDiagram
    participant MC as MCP Client<br/>(Claude/Cursor)
    participant US as UnityMcpServer<br/>server.ts
    participant UTM as UnityToolManager<br/>unity-tool-manager.ts
    participant DT as DynamicUnityCommandTool<br/>dynamic-unity-command-tool.ts
    participant UC as UnityClient<br/>unity-client.ts
    participant MH as MessageHandler<br/>message-handler.ts
    participant UE as Unity Editor<br/>McpBridgeServer.cs

    MC->>US: CallTool Request
    US->>UTM: getTool(toolName)
    UTM-->>US: DynamicUnityCommandTool
    US->>DT: execute(args)
    DT->>UC: executeCommand()
    UC->>MH: createRequest()
    UC->>UE: Send JSON-RPC
    UE-->>UC: JSON-RPC Response
    UC->>MH: handleIncomingData()
    MH-->>UC: Resolve Promise
    UC-->>DT: Command Result
    DT-->>US: Tool Response
    US-->>MC: CallTool Response
```

## 8. TypeScript Core Architectural Principles

### 8.1. Dynamic and Extensible Tooling
The server's core strength is its ability to dynamically adapt to tools (commands) available in Unity:

- **`UnityToolManager`**: Handles all dynamic tool management through dedicated methods:
  - `initializeDynamicTools()`: Orchestrates the tool initialization process
  - `fetchCommandDetailsFromUnity()`: Retrieves command metadata from Unity
  - `createDynamicToolsFromCommands()`: Creates tool instances from metadata
  - `refreshDynamicToolsSafe()`: Safely refreshes tools with duplicate prevention
- **`McpClientCompatibility`**: Manages client-specific requirements:
  - `handleClientNameInitialization()`: Manages client name synchronization
  - `isListChangedUnsupported()`: Detects clients that don't support list_changed notifications
- **`DynamicUnityCommandTool`**: Generic "tool" factory that takes schema information received from Unity (name, description, parameters) and constructs MCP-compliant tools on the fly

### 8.2. Decoupling and Single Responsibility
The architecture follows Martin Fowler's Extract Class pattern for clean separation of responsibilities:

- **`server.ts` (`UnityMcpServer`)**: Main application entry point, focused solely on MCP protocol handling and component orchestration
- **`unity-connection-manager.ts` (`UnityConnectionManager`)**: Manages Unity connection lifecycle, discovery, and reconnection logic
- **`unity-tool-manager.ts` (`UnityToolManager`)**: Handles all aspects of dynamic tool management, from fetching Unity commands to creating and refreshing tool instances
- **`mcp-client-compatibility.ts` (`McpClientCompatibility`)**: Manages client-specific behaviors and compatibility requirements
- **`unity-event-handler.ts` (`UnityEventHandler`)**: Handles event processing, notifications, signal handling, and graceful shutdown procedures
- **`unity-client.ts` (`UnityClient`)**: Manages TCP connection to Unity Editor, delegates to:
  - **`connection-manager.ts` (`ConnectionManager`)**: Handles connection state management
  - **`message-handler.ts` (`MessageHandler`)**: Processes JSON-RPC message parsing and routing
- **`unity-discovery.ts` (`UnityDiscovery`)**: Singleton service for Unity instance discovery with 1-second polling

### 8.3. Resilience and Robustness
The server is designed to be resilient to connection drops and process lifecycle events:

- **Connection Management**: `UnityConnectionManager` orchestrates connection lifecycle through `UnityDiscovery` (singleton pattern prevents multiple timers)
- **Graceful Shutdown**: `UnityEventHandler` handles all signal processing (`SIGINT`, `SIGTERM`, `SIGHUP`) and monitors `stdin` to ensure graceful shutdown
- **Client Compatibility**: `McpClientCompatibility` manages different client behaviors, ensuring proper initialization for clients that don't support list_changed notifications (Claude Code, Gemini, Windsurf, Codeium)
- **Safe Timers**: The `safe-timer.ts` utility provides `setTimeout` and `setInterval` wrappers that automatically clear themselves when the process exits
- **Delayed Unity Connection**: Server waits for MCP client to provide its name before connecting to Unity, preventing "Unknown Client" from appearing in Unity UI

### 8.4. Safe Logging
Because the server uses `stdio` for JSON-RPC communication, `console.log` cannot be used for debugging:
- **`log-to-file.ts`**: Provides safe, file-based logging mechanism. When `MCP_DEBUG` environment variable is set, all debug, info, warning, and error messages are written to timestamped log files in `~/.claude/uloopmcp-logs/`

## 9. TypeScript Key Components (File Breakdown)

### `src/server.ts`
Main entry point of the application, simplified through Martin Fowler's Extract Class refactoring:
- **`UnityMcpServer` class**:
    - Initializes the `@modelcontextprotocol/sdk` `Server`
    - Instantiates and orchestrates specialized manager classes
    - Handles `InitializeRequestSchema`, `ListToolsRequestSchema`, and `CallToolRequestSchema`
    - Delegates initialization to appropriate managers based on client compatibility

### `src/unity-connection-manager.ts`
Manages Unity connection lifecycle with focus on discovery and reconnection:
- **`UnityConnectionManager` class**:
    - Orchestrates Unity connection establishment through `UnityDiscovery`
    - Provides `waitForUnityConnectionWithTimeout()` for synchronous initialization
    - Handles connection callbacks and manages reconnection scenarios
    - Integrates with singleton `UnityDiscovery` service to prevent timer conflicts

### `src/unity-tool-manager.ts`
Handles all aspects of dynamic tool management and lifecycle:
- **`UnityToolManager` class**:
    - `initializeDynamicTools()`: Fetches Unity commands and creates corresponding tools
    - `refreshDynamicToolsSafe()`: Safely refreshes tools with duplicate prevention
    - `fetchCommandDetailsFromUnity()`: Retrieves command metadata from Unity
    - `createDynamicToolsFromCommands()`: Creates tool instances from Unity schemas
    - Manages the `dynamicTools` Map and provides tool access methods

### `src/mcp-client-compatibility.ts`
Manages client-specific compatibility and behavior differences:
- **`McpClientCompatibility` class**:
    - `isListChangedUnsupported()`: Detects clients that don't support list_changed notifications
    - `handleClientNameInitialization()`: Manages client name setup and environment variable fallbacks
    - `initializeClient()`: Orchestrates client-specific initialization procedures
    - Handles compatibility for Claude Code, Gemini, Windsurf, and Codeium clients

### `src/unity-event-handler.ts`
Manages event processing, notifications, and graceful shutdown:
- **`UnityEventHandler` class**:
    - `setupUnityEventListener()`: Configures Unity notification listeners
    - `sendToolsChangedNotification()`: Sends MCP list_changed notifications with duplicate prevention
    - `setupSignalHandlers()`: Configures process signal handlers for graceful shutdown
    - `gracefulShutdown()`: Handles cleanup and process termination

### `src/unity-client.ts`
Encapsulates all communication with Unity Editor:
- **`UnityClient` class**:
    - Manages `net.Socket` for TCP communication
    - `connect()` establishes connection, `ensureConnected()` provides resilient connection management
    - `executeCommand()` sends JSON-RPC requests to Unity and waits for responses
    - Handles incoming data, distinguishing between responses and asynchronous notifications

### `src/unity-discovery.ts`
Singleton service for Unity instance discovery:
- **`UnityDiscovery` class**:
    - Implements singleton pattern to prevent multiple discovery timers
    - Provides 1-second polling for Unity Editor instances
    - Scans ports [8700, 8800, 8900, 9000, 9100, 8600]
    - Handles connection callbacks and connection loss events

### `src/tools/dynamic-unity-command-tool.ts`
Factory for tools based on Unity commands:
- **`DynamicUnityCommandTool` class**:
    - `generateInputSchema()` translates C# schema definition into JSON Schema format
    - `execute()` method forwards tool calls to Unity via `UnityClient`
    - Extends `BaseTool` abstract class for consistent tool interface

### `src/utils/`
Contains helper utilities:
- **`log-to-file.ts`**: Safe, file-based logging functions (`debugToFile`, `infoToFile`, etc.)
- **`safe-timer.ts`**: `SafeTimer` class and `safeSetTimeout`/`safeSetInterval` functions for robust timer management

### `src/constants.ts`
Central file for all shared constants:
- MCP protocol constants
- Environment variables
- Default messages and timeout values
- Port ranges and discovery settings

## 10. TypeScript Key Workflows

### 10.1. Server Startup and Tool Initialization
1. `UnityMcpServer` instantiates specialized manager classes
2. `UnityMcpServer.start()` called
3. `UnityEventHandler.setupUnityEventListener()` configures notification listeners
4. `UnityConnectionManager.initialize()` starts connection discovery process
5. MCP server connects to `StdioServerTransport`, ready to serve requests
6. Server waits for `initialize` request from MCP client
7. Upon receiving `initialize` request:
   - Client name extracted from `clientInfo.name`
   - `McpClientCompatibility.setClientName()` stores client information
   - Based on client compatibility, either synchronous or asynchronous initialization used
8. For synchronous initialization (list_changed unsupported clients):
   - `UnityConnectionManager.waitForUnityConnectionWithTimeout()` waits for Unity
   - `UnityToolManager.getToolsFromUnity()` fetches and returns tools immediately
9. For asynchronous initialization (list_changed supported clients):
   - `UnityToolManager.initializeDynamicTools()` starts background initialization
   - Tools discovered and `UnityEventHandler.sendToolsChangedNotification()` notifies client

### 10.2. Handling a Tool Call
1. MCP client sends `tools/call` request via `stdin`
2. `UnityMcpServer`'s `CallToolRequestSchema` handler invoked
3. Calls `UnityToolManager.hasTool()` to check if requested tool exists
4. Calls `UnityToolManager.getTool()` to retrieve corresponding `DynamicUnityCommandTool` instance
5. Calls `execute()` method on tool instance
6. Tool's `execute()` method calls `this.context.unityClient.executeCommand()` with tool name and arguments
7. `UnityClient` sends JSON-RPC request to Unity over TCP
8. Unity executes command and sends response back
9. `UnityClient` receives response and resolves promise, returning result to tool
10. Tool formats result into MCP-compliant response and returns it
11. `UnityMcpServer` sends final response to client via `stdout`

## 11. TypeScript Development and Testing Infrastructure

### 11.1. Build System
- **esbuild**: Fast JavaScript bundler for production builds
- **TypeScript**: Type-safe JavaScript development
- **Node.js**: Runtime environment for server execution

### 11.2. Testing Framework
- **Jest**: JavaScript testing framework
- **Unit Tests**: Individual component testing
- **Integration Tests**: Cross-component interaction testing

### 11.3. Code Quality
- **ESLint**: JavaScript/TypeScript linting
- **Prettier**: Code formatting
- **Type Checking**: Strict TypeScript compilation

### 11.4. Debugging and Monitoring
- **File-based Logging**: Safe logging to `~/.claude/uloopmcp-logs/`
- **Debug Environment Variables**: `MCP_DEBUG` for detailed logging
- **Process Monitoring**: Signal handling and graceful shutdown
- **Connection Health**: Automatic reconnection and discovery
