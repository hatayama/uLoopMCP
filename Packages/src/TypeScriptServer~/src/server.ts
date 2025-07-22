#!/usr/bin/env node

import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
  InitializeRequestSchema,
} from '@modelcontextprotocol/sdk/types.js';
import { UnityClient } from './unity-client.js';
import { VibeLogger } from './utils/vibe-logger.js';
import { UnityDiscovery } from './unity-discovery.js';
import { UnityConnectionManager } from './unity-connection-manager.js';
import { UnityToolManager } from './unity-tool-manager.js';
import { McpClientCompatibility } from './mcp-client-compatibility.js';
import { UnityEventHandler } from './unity-event-handler.js';
import { ClientInitializationHandler } from './client-initialization-handler.js';
import { UnityPushNotificationManager } from './unity-push-notification-manager.js';
import { ToolResponse } from './types/tool-types.js';
import { UnityPushNotificationReceiveServer } from './unity-push-notification-receive-server.js';
import { ENVIRONMENT, MCP_SERVER_NAME, TOOLS_LIST_CHANGED_CAPABILITY } from './constants.js';
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
  private pushNotificationManager: UnityPushNotificationManager;
  private initializationHandler: ClientInitializationHandler;

  constructor() {
    this.initializeEnvironment();
    this.initializeMcpServer();
    this.initializeUnityComponents();
    this.initializePushNotificationSystem();
    this.initializeCrossDependencies();
    this.setupAllHandlers();
  }

  /**
   * Initialize environment configuration
   */
  private initializeEnvironment(): void {
    this.isDevelopment = process.env.NODE_ENV === ENVIRONMENT.NODE_ENV_DEVELOPMENT;
    VibeLogger.logInfo('mcp_server_starting', 'Unity MCP Server Starting');
  }

  /**
   * Initialize core MCP server
   */
  private initializeMcpServer(): void {
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
  }

  /**
   * Initialize Unity-related components
   */
  private initializeUnityComponents(): void {
    this.unityClient = UnityClient.getInstance();

    // Initialize Unity connection manager
    this.connectionManager = new UnityConnectionManager(this.unityClient);
    this.unityDiscovery = this.connectionManager.getUnityDiscovery();

    // Initialize Unity tool manager
    this.toolManager = new UnityToolManager(this.unityClient);

    // Initialize MCP client compatibility manager
    this.clientCompatibility = new McpClientCompatibility(this.unityClient);

    // Initialize Unity event handler
    this.eventHandler = new UnityEventHandler(
      this.server,
      this.unityClient,
      this.connectionManager,
    );
  }

  /**
   * Initialize push notification system
   */
  private initializePushNotificationSystem(): void {
    const pushNotificationServer = new UnityPushNotificationReceiveServer();
    
    this.pushNotificationManager = new UnityPushNotificationManager(
      pushNotificationServer,
      this.unityClient,
      this.connectionManager,
      this.toolManager,
      this.eventHandler,
    );
  }

  /**
   * Initialize cross-component dependencies and callbacks
   */
  private initializeCrossDependencies(): void {
    // Initialize Client initialization handler
    this.initializationHandler = new ClientInitializationHandler(
      this.unityClient,
      this.toolManager,
      this.clientCompatibility,
      this.connectionManager,
    );

    // Set up cross-dependencies
    this.toolManager.setClientInitializationHandler(this.initializationHandler);

    // Setup reconnection callback for tool refresh
    this.connectionManager.setupReconnectionCallback(async () => {
      await this.refreshToolsAndNotifyClients();
    });
  }

  /**
   * Setup all event handlers and signal handlers
   */
  private setupAllHandlers(): void {
    this.setupHandlers();
    this.eventHandler.setupSignalHandlers();
  }

  private setupHandlers(): void {
    // Handle initialize request using ClientInitializationHandler
    this.server.setRequestHandler(InitializeRequestSchema, async (request) => {
      return await this.initializationHandler.handleInitialize(request);
    });

    // Provide tool list based on Unity connection state
    this.server.setRequestHandler(ListToolsRequestSchema, async () => {
      const tools = await this.toolManager.getAvailableTools();
      const clientInfo = this.initializationHandler.getClientInfo();

      VibeLogger.logInfo(
        'mcp_tools_list_requested',
        `Tools list requested by ${clientInfo?.name || 'Unknown'}`,
        {
          client_name: clientInfo?.name || 'Unknown',
          tools_count: tools.length,
          unity_connected: this.unityClient.connected,
        },
        undefined,
        'MCP tools/list request processed with Unity connection state check',
      );

      return { tools };
    });

    // Handle tool execution with Unity connection state validation
    this.server.setRequestHandler(CallToolRequestSchema, async (request) => {
      const { name, arguments: args } = request.params;
      const clientInfo = this.initializationHandler.getClientInfo();

      VibeLogger.logInfo(
        'mcp_tool_execution_requested',
        `Tool execution requested: ${name}`,
        {
          client_name: clientInfo?.name || 'Unknown',
          tool_name: name,
          unity_connected: this.unityClient.connected,
          has_args: !!args,
        },
        undefined,
        'MCP tool execution request received',
      );

      try {
        // Check Unity connection first
        if (!this.unityClient.connected) {
          const errorMessage = `Tool ${name} cannot be executed: Unity Editor is not connected. Please ensure Unity Editor is running and the uLoopMCP plugin is active.`;

          VibeLogger.logError(
            'mcp_tool_execution_no_unity',
            'Tool execution failed - Unity not connected',
            {
              client_name: clientInfo?.name || 'Unknown',
              tool_name: name,
              unity_connected: false,
            },
            undefined,
            'Unity connection required for tool execution',
          );

          return {
            content: [
              {
                type: 'text',
                text: errorMessage,
              },
            ],
            isError: true,
          };
        }

        // Check if it's a dynamic Unity tool
        if (this.toolManager.hasTool(name)) {
          const dynamicTool = this.toolManager.getTool(name);
          if (!dynamicTool) {
            throw new Error(`Tool ${name} is not available`);
          }
          const result: ToolResponse = await dynamicTool.execute(args ?? {});

          VibeLogger.logInfo(
            'mcp_tool_execution_success',
            `Tool execution completed: ${name}`,
            {
              client_name: clientInfo?.name || 'Unknown',
              tool_name: name,
              is_error: result.isError,
            },
            undefined,
            'MCP tool execution completed',
          );

          // Convert ToolResponse to MCP-compatible format
          return {
            content: result.content,
            isError: result.isError,
          };
        }

        // All tools should be handled by dynamic tools
        throw new Error(`Unknown tool: ${name}`);
      } catch (error) {
        const errorMessage = error instanceof Error ? error.message : 'Unknown error';

        VibeLogger.logError(
          'mcp_tool_execution_error',
          `Tool execution error: ${name}`,
          {
            client_name: clientInfo?.name || 'Unknown',
            tool_name: name,
            error_message: errorMessage,
            unity_connected: this.unityClient.connected,
          },
          undefined,
          'MCP tool execution failed with error',
        );

        return {
          content: [
            {
              type: 'text',
              text: `Error executing ${name}: ${errorMessage}`,
            },
          ],
          isError: true,
        };
      }
    });
  }

  /**
   * Refresh dynamic tools and notify MCP clients
   */
  private async refreshToolsAndNotifyClients(): Promise<void> {
    await this.toolManager.refreshDynamicToolsSafe(() => {
      this.eventHandler.sendToolsChangedNotification();
    });
  }

  /**
   * Start the server
   */
  async start(): Promise<void> {
    // Start Push notification receive server first
    try {
      await this.pushNotificationManager.startPushNotificationServer();
    } catch (error) {
      VibeLogger.logError(
        'push_server_start_failed',
        'Failed to start push notification receive server',
        { error: error instanceof Error ? error.message : String(error) },
        undefined,
        'Push notification system will not be available',
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
      // Notify initialization handler about Unity connection
      this.initializationHandler.handleUnityConnection();

      // If we have a client name, initialize tools immediately
      const clientName = this.clientCompatibility.getClientName();
      if (clientName) {
        this.toolManager.setClientName(clientName);
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
