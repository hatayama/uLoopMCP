#!/usr/bin/env node

import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
  InitializeRequestSchema,
  InitializeResult,
} from '@modelcontextprotocol/sdk/types.js';
import { UnityClient } from './unity-client.js';
import { VibeLogger } from './utils/vibe-logger.js';
import { UnityDiscovery } from './unity-discovery.js';
import { UnityConnectionManager } from './unity-connection-manager.js';
import { UnityToolManager } from './unity-tool-manager.js';
import { McpClientCompatibility } from './mcp-client-compatibility.js';
import { UnityEventHandler } from './unity-event-handler.js';
import { ToolResponse } from './types/tool-types.js';
import { NotificationReceiveServer } from './notification-receive-server.js';
import { InitializeNotificationServerUseCase } from './domain/use-cases/initialize-notification-server-use-case.js';
import {
  ENVIRONMENT,
  MCP_PROTOCOL_VERSION,
  MCP_SERVER_NAME,
  TOOLS_LIST_CHANGED_CAPABILITY,
} from './constants.js';
import packageJson from '../package.json' assert { type: 'json' };

/**
 * Unity MCP Server - Bridge between MCP protocol and Unity Editor
 *
 * Design document reference: Packages/src/TypeScriptServer~/ARCHITECTURE.md
 *
 * Related classes:
 * - UnityConnectionManager: Manages Unity connection and discovery
 * - UnityToolManager: Manages dynamic tool generation and lifecycle
 * - McpClientCompatibility: Handles client-specific compatibility
 * - UnityEventHandler: Manages events and graceful shutdown
 * - UnityClient: Handles the TCP connection to the Unity Editor
 * - DynamicUnityCommandTool: Dynamically creates tools based on Unity tools
 * - @modelcontextprotocol/sdk/server: The core MCP server implementation
 */
class UnityMcpServer {
  private server: Server;
  private unityClient: UnityClient;
  private readonly isDevelopment: boolean;
  private isInitialized: boolean = false;
  private unityDiscovery: UnityDiscovery;
  private connectionManager: UnityConnectionManager;
  private toolManager: UnityToolManager;
  private clientCompatibility: McpClientCompatibility;
  private eventHandler: UnityEventHandler;
  private notificationReceiveServer: NotificationReceiveServer;
  private initializeNotificationServerUseCase: InitializeNotificationServerUseCase;

  constructor() {
    // Simple environment variable check
    this.isDevelopment = process.env.NODE_ENV === ENVIRONMENT.NODE_ENV_DEVELOPMENT;

    VibeLogger.logInfo('mcp_server_starting', 'Unity MCP Server Starting');

    this.server = new Server(
      {
        name: MCP_SERVER_NAME,
        version: packageJson.version,
      },
      {
        capabilities: {
          tools: {
            listChanged: TOOLS_LIST_CHANGED_CAPABILITY,
          },
        },
      },
    );

    this.unityClient = UnityClient.getInstance();

    // Initialize Unity connection manager
    this.connectionManager = new UnityConnectionManager(this.unityClient);
    this.unityDiscovery = this.connectionManager.getUnityDiscovery();

    // Initialize Unity tool manager
    this.toolManager = new UnityToolManager(this.unityClient);
    // Phase 3.2: Inject connectionManager for RefreshToolsUseCase integration
    this.toolManager.setConnectionManager(this.connectionManager);

    // Initialize MCP client compatibility manager
    this.clientCompatibility = new McpClientCompatibility(this.unityClient);

    // Initialize Unity event handler
    this.eventHandler = new UnityEventHandler(
      this.server,
      this.unityClient,
      this.connectionManager,
    );

    // Initialize notification receive server (optional feature)
    this.notificationReceiveServer = new NotificationReceiveServer();

    // Initialize notification server UseCase
    this.initializeNotificationServerUseCase = new InitializeNotificationServerUseCase(
      this.notificationReceiveServer,
      this.unityClient,
    );

    // Setup reconnection callback for tool refresh
    this.connectionManager.setupReconnectionCallback(async () => {
      await this.toolManager.refreshDynamicToolsSafe(() => {
        this.eventHandler.sendToolsChangedNotification();
      });
    });

    this.setupHandlers();
    this.eventHandler.setupSignalHandlers();
  }

  /**
   * Initialize client synchronously (unified initialization for all clients)
   */
  private async initializeSyncClient(clientName: string): Promise<InitializeResult> {
    try {
      await this.clientCompatibility.initializeClient(clientName);
      this.toolManager.setClientName(clientName);
      await this.connectionManager.waitForUnityConnectionWithTimeout(10000);
      const tools = await this.toolManager.getToolsFromUnity();

      // Returning tools for client
      return {
        protocolVersion: MCP_PROTOCOL_VERSION,
        capabilities: {
          tools: {
            listChanged: TOOLS_LIST_CHANGED_CAPABILITY,
          },
        },
        serverInfo: {
          name: MCP_SERVER_NAME,
          version: packageJson.version,
        },
        tools,
      };
    } catch (error) {
      VibeLogger.logError(
        'mcp_unity_connection_timeout',
        'Unity connection timeout',
        {
          client_name: clientName,
          error_message: error instanceof Error ? error.message : String(error),
        },
        undefined,
        'Unity connection timed out - check Unity MCP bridge status',
      );
      return {
        protocolVersion: MCP_PROTOCOL_VERSION,
        capabilities: {
          tools: {
            listChanged: TOOLS_LIST_CHANGED_CAPABILITY,
          },
        },
        serverInfo: {
          name: MCP_SERVER_NAME,
          version: packageJson.version,
        },
        tools: [],
      };
    }
  }

  private setupHandlers(): void {
    // Handle initialize request to get client information
    this.server.setRequestHandler(InitializeRequestSchema, async (request) => {
      const clientInfo = request.params?.clientInfo;
      const clientName = clientInfo?.name || '';

      // Debug logging for client name detection
      VibeLogger.logInfo(
        'mcp_client_name_received',
        `MCP client name received: ${clientName}`,
        {
          client_name: clientName,
          client_info: clientInfo,
        },
        undefined,
        'This logs the client name received during MCP initialize request',
        'Analyze this to ensure claude-code is properly detected',
      );

      if (clientName) {
        this.clientCompatibility.setupClientCompatibility(clientName);
      }

      // Initialize Unity connection after receiving client name
      if (!this.isInitialized) {
        this.isInitialized = true;

        // All clients use synchronous initialization for consistency
        // This eliminates complexity from list_changed support detection
        return this.initializeSyncClient(clientName);
      }

      return {
        protocolVersion: MCP_PROTOCOL_VERSION,
        capabilities: {
          tools: {
            listChanged: TOOLS_LIST_CHANGED_CAPABILITY,
          },
        },
        serverInfo: {
          name: MCP_SERVER_NAME,
          version: packageJson.version,
        },
      };
    });

    // Provide tool list
    this.server.setRequestHandler(ListToolsRequestSchema, () => {
      const tools = this.toolManager.getAllTools();

      // Providing tools to client
      return { tools };
    });

    // Handle tool execution
    this.server.setRequestHandler(CallToolRequestSchema, async (request) => {
      const { name, arguments: args } = request.params;

      // Tool executed

      try {
        // Check if it's a dynamic Unity tool
        if (this.toolManager.hasTool(name)) {
          const dynamicTool = this.toolManager.getTool(name);
          if (!dynamicTool) {
            throw new Error(`Tool ${name} is not available`);
          }
          const result: ToolResponse = await dynamicTool.execute(args ?? {});
          // Convert ToolResponse to MCP-compatible format
          return {
            content: result.content,
            isError: result.isError,
          };
        }

        // All tools should be handled by dynamic tools
        throw new Error(`Unknown tool: ${name}`);
      } catch (error) {
        return {
          content: [
            {
              type: 'text',
              text: `Error executing ${name}: ${error instanceof Error ? error.message : 'Unknown error'}`,
            },
          ],
          isError: true,
        };
      }
    });
  }

  /**
   * Start the server
   */
  async start(): Promise<void> {
    // Initialize notification server using UseCase (optional feature for domain reload notifications)
    try {
      const result = await this.initializeNotificationServerUseCase.execute({
        clientName: 'server-startup',
      });

      if (result.success) {
        // Setup domain reload handler
        this.notificationReceiveServer.setDomainReloadHandler(() => {
          VibeLogger.logInfo(
            'domain_reload_notification_handled',
            'Domain reload notification received - triggering immediate reconnection',
            {},
            undefined,
            'Unity has completed domain reload, attempting immediate reconnection',
          );
          // Trigger immediate reconnection attempt
          void this.connectionManager.ensureConnected();
        });
      }
    } catch (error) {
      VibeLogger.logWarning(
        'notification_receive_server_startup_failed',
        'Notification receive server failed to start - falling back to polling',
        { error: error instanceof Error ? error.message : String(error) },
        undefined,
        'Domain reload notifications will use polling fallback',
      );
    }

    // Setup Unity event notification listener (will be used after Unity connection)
    this.eventHandler.setupUnityEventListener(async () => {
      await this.toolManager.refreshDynamicToolsSafe(() => {
        this.eventHandler.sendToolsChangedNotification();
      });
    });

    // Initialize connection manager with callback for tool initialization
    this.connectionManager.initialize(async () => {
      // If we have a client name, initialize tools immediately
      const clientName = this.clientCompatibility.getClientName();
      if (clientName) {
        this.toolManager.setClientName(clientName);

        // Set notification port in UnityClient before sending client name
        const notificationPort = this.notificationReceiveServer.getPort();
        if (notificationPort > 0) {
          this.unityClient.setNotificationPort(notificationPort);
          VibeLogger.logInfo(
            'notification_port_set_for_unity',
            'Set notification port for Unity client communication',
            { notificationPort, clientName },
            undefined,
            'This port will be sent to Unity for domain reload notifications',
          );
        }

        await this.toolManager.initializeDynamicTools();
        // Unity connection established and tools initialized

        // Send immediate tools changed notification for faster recovery
        this.eventHandler.sendToolsChangedNotification();
      } else {
        // Unity connection established, waiting for client name
      }
    });

    if (this.isDevelopment) {
      // Server starting with unified discovery service
    }

    // Connect to MCP transport first - wait for client name before connecting to Unity
    const transport = new StdioServerTransport();
    await this.server.connect(transport);
  }
}

// Start server
const server = new UnityMcpServer();

server.start().catch((error) => {
  VibeLogger.logError(
    'mcp_server_startup_fatal',
    'Unity MCP Server startup failed',
    {
      error_message: error instanceof Error ? error.message : String(error),
      stack_trace: error instanceof Error ? error.stack : 'No stack trace available',
      error_type: error instanceof Error ? error.constructor.name : typeof error,
    },
    undefined,
    'Fatal server startup error - check Unity MCP bridge configuration',
  );
  process.exit(1);
});
