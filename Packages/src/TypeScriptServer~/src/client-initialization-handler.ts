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
      },
      undefined,
      'This logs the client name received during MCP initialize request',
      'Analyze this to ensure claude-code is properly detected',
    );

    if (this.clientInfo.name) {
      this.clientCompatibility.setClientName(this.clientInfo.name);
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
   * Perform synchronous initialization for all clients
   * All clients now use synchronous initialization for consistency and reliability
   */
  private async performInitialization(): Promise<InitializeResult> {
    if (!this.clientInfo) {
      return this.buildInitializeResponse();
    }

    // All clients use synchronous initialization
    return await this.performSynchronousInitialization();
  }

  /**
   * Synchronous initialization for all clients
   * Waits for Unity connection and returns tools directly in initialize response
   */
  private async performSynchronousInitialization(): Promise<InitializeResult> {
    if (!this.clientInfo) {
      return this.buildInitializeResponse();
    }

    try {
      await this.clientCompatibility.initializeClient(this.clientInfo.name);
      this.toolManager.setClientName(this.clientInfo.name);
      
      // Wait for Unity connection first
      await this.connectionManager.waitForUnityConnectionWithTimeout(10000);
      
      // Now get tools after connection is established
      const tools = await this.toolManager.getToolsFromUnity();

      VibeLogger.logInfo(
        'mcp_sync_init_success',
        'Synchronous initialization completed successfully',
        {
          client_name: this.clientInfo.name,
          tools_count: tools.length,
        },
        undefined,
        'Synchronous initialization completed with Unity tools',
      );

      return this.buildInitializeResponseWithTools(tools);
    } catch (error) {
      VibeLogger.logError(
        'mcp_unity_connection_timeout',
        'Unity connection and tools timeout during synchronous initialization',
        {
          client_name: this.clientInfo.name,
          error_message: error instanceof Error ? error.message : String(error),
        },
        undefined,
        'Unity connection or tools retrieval timed out - check Unity MCP bridge status',
      );
      return this.buildInitializeResponse();
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
