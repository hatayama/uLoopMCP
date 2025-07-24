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
import { ToolResponse } from './types/tool-types.js';
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
          is_list_changed_unsupported:
            this.clientCompatibility.isListChangedUnsupported(clientName),
        },
        undefined,
        'This logs the client name received during MCP initialize request',
        'Analyze this to ensure claude-code is properly detected',
      );

      if (clientName) {
        this.clientCompatibility.setClientName(clientName);
        this.clientCompatibility.logClientCompatibility(clientName);
        // Client name received - no logging needed for normal operation
      }

      // Initialize Unity connection after receiving client name
      if (!this.isInitialized) {
        this.isInitialized = true;

        if (this.clientCompatibility.isListChangedUnsupported(clientName)) {
          // list_changed unsupported client: wait for Unity connection
          // Sync initialization for list_changed unsupported client

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
        } else {
          // list_changed supported client: asynchronous approach
          // Async initialization for list_changed supported client

          // Start Unity connection initialization in background
          void this.clientCompatibility.initializeClient(clientName);
          this.toolManager.setClientName(clientName);
          void this.toolManager
            .initializeDynamicTools()
            .then(() => {
              // Unity connection established successfully
            })
            .catch((error) => {
              VibeLogger.logError(
                'mcp_unity_connection_init_failed',
                'Unity connection initialization failed',
                { error_message: error instanceof Error ? error.message : String(error) },
                undefined,
                'Unity connection could not be established - check Unity MCP bridge',
              );
              // Start Unity discovery to retry connection (singleton pattern prevents duplicates)
              this.unityDiscovery.start();
            });
        }
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
