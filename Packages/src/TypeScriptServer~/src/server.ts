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
import { ToolResponse } from './types/tool-types.js';
import {
  UnityPushNotificationReceiveServer,
  UnityConnectedEvent,
  UnityDisconnectedEvent,
  PushNotificationEvent,
} from './unity-push-notification-receive-server.js';
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
  private pushNotificationServer: UnityPushNotificationReceiveServer;
  private initializationHandler: ClientInitializationHandler;

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

    // Initialize MCP client compatibility manager
    this.clientCompatibility = new McpClientCompatibility(this.unityClient);

    // Initialize Unity event handler
    this.eventHandler = new UnityEventHandler(
      this.server,
      this.unityClient,
      this.connectionManager,
    );

    // Initialize Push notification receive server
    this.pushNotificationServer = new UnityPushNotificationReceiveServer();
    this.setupPushNotificationHandlers();

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
      await this.toolManager.refreshDynamicToolsSafe(() => {
        this.eventHandler.sendToolsChangedNotification();
      });
    });

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

  private setupPushNotificationHandlers(): void {
    this.pushNotificationServer.on('unity_connected', (event: UnityConnectedEvent) => {
      VibeLogger.logInfo(
        'unity_push_client_connected',
        'Unity Push client connected',
        { clientId: event.clientId, endpoint: event.endpoint },
        undefined,
        'Unity successfully connected to push notification server',
      );
    });

    this.pushNotificationServer.on('unity_disconnected', (event: UnityDisconnectedEvent) => {
      VibeLogger.logInfo(
        'unity_push_client_disconnected',
        'Unity Push client disconnected',
        { clientId: event.clientId, reason: event.reason },
        undefined,
        'Unity disconnected from push notification server',
      );

      // Synchronize Unity client disconnection state
      this.unityClient.disconnect();
      
      VibeLogger.logInfo(
        'push_unity_disconnection_synced',
        'Unity disconnection state synchronized',
        {
          clientId: event.clientId,
          reason: event.reason,
          unity_connected: this.unityClient.connected,
        },
        undefined,
        'Unity disconnection state updated via push notification',
      );
    });

    this.pushNotificationServer.on('connection_established', async (event: PushNotificationEvent) => {
      VibeLogger.logInfo('push_connection_established', 'Push connection established', {
        clientId: event.clientId,
        notification: event.notification,
      });

      // Ensure Unity client connection state is synchronized with push notification
      try {
        await this.unityClient.ensureConnected();
        
        // Notify connection manager about Unity connection
        this.connectionManager.handleUnityDiscovered();

        VibeLogger.logInfo(
          'push_unity_connection_synced',
          'Unity connection state synchronized with push notification',
          {
            clientId: event.clientId,
            unity_connected: this.unityClient.connected,
          },
          undefined,
          'Unity connection state updated via push notification',
        );

        // Refresh tools and notify clients about availability
        await this.toolManager.refreshDynamicToolsSafe(() => {
          this.eventHandler.sendToolsChangedNotification();
        });
      } catch (error) {
        VibeLogger.logError(
          'push_unity_connection_sync_failed',
          'Failed to synchronize Unity connection state',
          {
            clientId: event.clientId,
            error: error instanceof Error ? error.message : String(error),
          },
          undefined,
          'Unity connection synchronization failed',
        );
      }
    });

    this.pushNotificationServer.on('domain_reload_start', (event: PushNotificationEvent) => {
      VibeLogger.logInfo(
        'push_domain_reload_start',
        'Unity domain reload started',
        { clientId: event.clientId, notification: event.notification },
        undefined,
        'Unity is performing domain reload - connection may be temporarily lost',
      );
    });

    this.pushNotificationServer.on('domain_reload_recovered', (event: PushNotificationEvent) => {
      VibeLogger.logInfo(
        'push_domain_reload_recovered',
        'Unity domain reload recovered',
        { clientId: event.clientId, notification: event.notification },
        undefined,
        'Unity has recovered from domain reload',
      );

      void this.toolManager.refreshDynamicToolsSafe(() => {
        this.eventHandler.sendToolsChangedNotification();
      });
    });

    this.pushNotificationServer.on('tools_changed', (event: PushNotificationEvent) => {
      VibeLogger.logInfo('push_tools_changed', 'Unity tools changed notification received', {
        clientId: event.clientId,
        notification: event.notification,
      });

      void this.toolManager.refreshDynamicToolsSafe(() => {
        this.eventHandler.sendToolsChangedNotification();
      });
    });
  }

  /**
   * Start the server
   */
  async start(): Promise<void> {
    // Start Push notification receive server first
    try {
      const pushServerPort = await this.pushNotificationServer.start();
      VibeLogger.logInfo(
        'push_server_started',
        'Push notification receive server started',
        { port: pushServerPort },
        undefined,
        'TypeScript Push notification server is ready to receive Unity connections',
      );
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
