import { InitializeRequest, InitializeResult } from '@modelcontextprotocol/sdk/types.js';
import { UnityClient } from './unity-client.js';
import { UnityToolManager } from './unity-tool-manager.js';
import { McpClientCompatibility } from './mcp-client-compatibility.js';
import { UnityConnectionManager } from './unity-connection-manager.js';
import { UnityEventHandler } from './unity-event-handler.js';
import { UnityPushNotificationManager } from './unity-push-notification-manager.js';
import {
  ClientInitializationUseCase,
  LLMToolInfo,
} from './usecases/client-initialization-use-case.js';
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
    private pushNotificationManager: UnityPushNotificationManager,
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
   * Note: setClientName is now handled by ClientInitializationUseCase - no duplicate calls
   */
  handleUnityConnection(): void {
    this.isUnityConnected = true;

    VibeLogger.logInfo(
      'unity_connection_established_handler',
      'Unity connection established - client registration handled by UseCase',
      {
        isUnityConnected: this.isUnityConnected,
        clientName: this.clientInfo?.name,
      },
      undefined,
      'Unity connection ready - setClientName handled by ClientInitializationUseCase',
    );
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
   * Perform initialization using ClientInitializationUseCase
   * UseCase pattern: create instance -> execute -> discard
   */
  private async performInitialization(): Promise<InitializeResult> {
    if (!this.clientInfo) {
      return this.buildInitializeResponse();
    }

    // Create UseCase instance (single-use pattern)
    const useCase = new ClientInitializationUseCase(
      this.unityClient,
      this.toolManager,
      this.connectionManager,
      this.pushNotificationManager,
      this.clientInfo,
    );

    // Execute all initialization steps with temporal cohesion
    return await useCase.execute();

    // UseCase instance is automatically discarded after this point
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
