import { InitializeRequest, InitializeResult } from '@modelcontextprotocol/sdk/types.js';
import { UnityClient } from './unity-client.js';
import { UnityToolManager } from './unity-tool-manager.js';
import { McpClientCompatibility } from './mcp-client-compatibility.js';
import { UnityConnectionManager } from './unity-connection-manager.js';
import { UnityEventHandler } from './unity-event-handler.js';
import { VibeLogger } from './utils/vibe-logger.js';
import {
  MCP_PROTOCOL_VERSION,
  MCP_SERVER_NAME,
  TOOLS_LIST_CHANGED_CAPABILITY,
} from './constants.js';
import packageJson from '../package.json' assert { type: 'json' };

/**
 * Client Initialization Handler - Manages MCP initialization process and client-specific setup
 *
 * Design document reference: .kiro/specs/mcp-tool-recognition-fix/design.md
 *
 * Related classes:
 * - McpClientCompatibility: Provides client-specific configuration
 * - UnityToolManager: Manages Unity tool availability
 * - UnityConnectionManager: Manages Unity connection state
 */
export class ClientInitializationHandler {
  private clientInfo: LLMToolInfo | null = null;
  private isUnityConnected: boolean = false;
  private isInitialized: boolean = false;

  constructor(
    private unityClient: UnityClient,
    private toolManager: UnityToolManager,
    private clientCompatibility: McpClientCompatibility,
    private connectionManager: UnityConnectionManager,
    private unityEventHandler: UnityEventHandler,
  ) {}

  /**
   * Handle MCP initialize request and extract client information
   */
  async handleInitialize(request: InitializeRequest): Promise<InitializeResult> {
    this.clientInfo = this.extractClientInfo(request);

    VibeLogger.logInfo(
      'mcp_client_name_received',
      `MCP client name received: ${this.clientInfo.name}`,
      {
        client_name: this.clientInfo.name,
        client_version: this.clientInfo.version,
        client_info: request.params?.clientInfo,
        is_list_changed_unsupported: this.clientCompatibility.isListChangedUnsupported(
          this.clientInfo.name,
        ),
      },
      undefined,
      'This logs the client name received during MCP initialize request',
      'Analyze this to ensure claude-code is properly detected',
    );

    if (this.clientInfo.name) {
      this.clientCompatibility.setClientName(this.clientInfo.name);
      this.clientCompatibility.logClientCompatibility(this.clientInfo.name);
    }

    if (!this.isInitialized) {
      this.isInitialized = true;
      return await this.performInitialization();
    }

    return this.buildInitializeResponse();
  }

  /**
   * Handle Unity connection establishment
   */
  handleUnityConnection(): void {
    this.isUnityConnected = true;
    this.notifyToolsAvailable();

    // Send client name and push notification endpoint to Unity
    this.unityClient.setClientName().catch((error) => {
      VibeLogger.logError(
        'unity_setclientname_failed',
        'Failed to send client name to Unity',
        { error: error instanceof Error ? error.message : String(error) },
        undefined,
        'Push notification endpoint may not be available to Unity',
      );
    });
  }

  /**
   * Handle Unity disconnection
   */
  handleUnityDisconnection(): void {
    this.isUnityConnected = false;
  }

  /**
   * Get current client information
   */
  getClientInfo(): LLMToolInfo | null {
    return this.clientInfo;
  }

  /**
   * Check if Unity is connected
   */
  isUnityConnectionEstablished(): boolean {
    return this.isUnityConnected;
  }

  /**
   * Perform initialization based on client type
   */
  private async performInitialization(): Promise<InitializeResult> {
    if (!this.clientInfo) {
      return this.buildInitializeResponse();
    }

    if (this.clientCompatibility.isListChangedUnsupported(this.clientInfo.name)) {
      // list_changed unsupported client: synchronous initialization
      return await this.performSynchronousInitialization();
    } else {
      // list_changed supported client: asynchronous initialization
      return this.performAsynchronousInitialization();
    }
  }

  /**
   * Synchronous initialization for list_changed unsupported clients (Claude Code)
   */
  private async performSynchronousInitialization(): Promise<InitializeResult> {
    if (!this.clientInfo) {
      return this.buildInitializeResponse();
    }

    try {
      await this.clientCompatibility.initializeClient(this.clientInfo.name);
      this.toolManager.setClientName(this.clientInfo.name);
      await this.connectionManager.waitForUnityConnectionWithTimeout(10000);
      const tools = await this.toolManager.getToolsFromUnity();

      VibeLogger.logInfo(
        'mcp_sync_init_success',
        'Synchronous initialization completed successfully',
        {
          client_name: this.clientInfo.name,
          tools_count: tools.length,
        },
        undefined,
        'Claude Code synchronous initialization completed with Unity tools',
      );

      return this.buildInitializeResponseWithTools(tools);
    } catch (error) {
      VibeLogger.logError(
        'mcp_unity_connection_timeout',
        'Unity connection timeout during synchronous initialization',
        {
          client_name: this.clientInfo.name,
          error_message: error instanceof Error ? error.message : String(error),
        },
        undefined,
        'Unity connection timed out - check Unity MCP bridge status',
      );
      return this.buildInitializeResponse();
    }
  }

  /**
   * Asynchronous initialization for list_changed supported clients (Cursor, VSCode)
   */
  private performAsynchronousInitialization(): InitializeResult {
    if (!this.clientInfo) {
      return this.buildInitializeResponse();
    }

    // Check if Unity is already connected before starting async initialization
    if (this.unityClient.connected) {
      VibeLogger.logInfo(
        'mcp_unity_already_connected',
        'Unity is already connected at initialization start',
        { client_name: this.clientInfo.name },
        undefined,
        'Sending immediate connection notification',
      );
      this.handleUnityConnection();
    }

    // Start Unity connection initialization in background
    void this.clientCompatibility.initializeClient(this.clientInfo.name);
    this.toolManager.setClientName(this.clientInfo.name);
    void this.toolManager
      .initializeDynamicTools()
      .then(() => {
        VibeLogger.logInfo(
          'mcp_async_init_success',
          'Asynchronous initialization completed successfully',
          { client_name: this.clientInfo.name },
          undefined,
          'Unity connection established successfully for list_changed supported client',
        );
        // Unity connection will trigger list_changed notification (if not already handled above)
        if (!this.isUnityConnected) {
          this.handleUnityConnection();
        }
      })
      .catch((error) => {
        VibeLogger.logError(
          'mcp_unity_connection_init_failed',
          'Unity connection initialization failed',
          {
            client_name: this.clientInfo.name,
            error_message: error instanceof Error ? error.message : String(error),
          },
          undefined,
          'Unity connection could not be established - check Unity MCP bridge',
        );
      });

    return this.buildInitializeResponse();
  }

  /**
   * Notify tools available based on client capabilities
   */
  private notifyToolsAvailable(): void {
    if (!this.clientInfo) {
      return;
    }

    if (this.clientCompatibility.isListChangedUnsupported(this.clientInfo.name)) {
      // list_changed non-supported client: do nothing (handled by next tools/list request)
      VibeLogger.logInfo(
        'mcp_tools_available_wait',
        `Client ${this.clientInfo.name} does not support list_changed, waiting for next tools/list request`,
        { client_name: this.clientInfo.name },
        undefined,
        'Tools will be available on next tools/list request for this client',
      );
    } else {
      // list_changed supported client: send notification
      VibeLogger.logInfo(
        'mcp_tools_available_notify',
        `Sending list_changed notification to ${this.clientInfo.name}`,
        { client_name: this.clientInfo.name },
        undefined,
        'Tools available notification sent to list_changed supported client',
      );
      this.unityEventHandler.sendToolsChangedNotification();
    }
  }

  /**
   * Extract client information from initialize request
   */
  private extractClientInfo(request: InitializeRequest): LLMToolInfo {
    const clientInfo = request.params?.clientInfo;
    const clientName = clientInfo?.name || 'Unknown';

    // Log client detection for debugging
    VibeLogger.logInfo(
      'mcp_client_detection',
      'Client detection from initialize request',
      {
        raw_client_info: clientInfo,
        detected_name: clientName,
        detected_version: clientInfo?.version || '0.0.0',
        has_client_info: !!clientInfo,
      },
      undefined,
      'Raw client information extracted from MCP initialize request',
      'Check this if client detection is not working correctly',
    );

    return {
      name: clientName,
      version: clientInfo?.version || '0.0.0',
    };
  }

  /**
   * Build standard initialize response
   */
  private buildInitializeResponse(): InitializeResult {
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
  }

  /**
   * Build initialize response with tools (for synchronous initialization)
   */
  private buildInitializeResponseWithTools(tools: unknown[]): InitializeResult {
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
  }
}

export interface LLMToolInfo {
  name: string;
  version: string;
}
