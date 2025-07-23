import { InitializeResult } from '@modelcontextprotocol/sdk/types.js';
import { UnityClient } from '../unity-client.js';
import { UnityToolManager } from '../unity-tool-manager.js';
import { UnityConnectionManager } from '../unity-connection-manager.js';
import { UnityPushNotificationManager } from '../unity-push-notification-manager.js';
import { VibeLogger } from '../utils/vibe-logger.js';
import {
  MCP_PROTOCOL_VERSION,
  MCP_SERVER_NAME,
  TOOLS_LIST_CHANGED_CAPABILITY,
} from '../constants.js';
import packageJson from '../../package.json' assert { type: 'json' };

/**
 * Client Initialization UseCase - Encapsulates temporal cohesion for client initialization process
 *
 * Design document reference: .kiro/specs/mcp-tool-recognition-fix/design.md
 *
 * Related classes:
 * - UnityClient: Manages Unity communication
 * - UnityToolManager: Manages Unity tools
 * - UnityConnectionManager: Manages Unity connection state
 * - UnityPushNotificationManager: Manages push notifications
 *
 * This UseCase class follows the single-use pattern:
 * 1. new() - create instance
 * 2. execute() - perform all initialization steps in temporal order
 * 3. instance is discarded after use (not reused)
 *
 * Temporal cohesion benefits:
 * - All initialization steps are contained in one place
 * - Clear execution order and dependencies
 * - Single point of failure handling
 * - Easy to test and reason about
 */
export class ClientInitializationUseCase {
  private correlationId: string;

  constructor(
    private unityClient: UnityClient,
    private toolManager: UnityToolManager,
    private connectionManager: UnityConnectionManager,
    private pushNotificationManager: UnityPushNotificationManager,
    private clientInfo: LLMToolInfo,
  ) {
    this.correlationId = `init_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
  }

  /**
   * Execute complete client initialization process
   * This method contains all initialization steps in temporal order
   * Should be called only once per instance
   */
  async execute(): Promise<InitializeResult> {
    const startTime = Date.now();

    VibeLogger.logInfo(
      'client_initialization_usecase_start',
      `Starting client initialization for ${this.clientInfo.name}`,
      {
        client_name: this.clientInfo.name,
        client_version: this.clientInfo.version,
      },
      this.correlationId,
      'UseCase pattern: Single-use initialization with temporal cohesion',
      'Track this correlation ID for complete initialization flow',
    );

    try {
      // Step 1: Wait for Unity connection
      await this.waitForUnityConnection();

      // Step 2: Register client with Unity (single setClientName call)
      await this.registerClientWithUnity();

      // Step 3: Configure push notification endpoint
      this.configurePushNotificationEndpoint();

      // Step 4: Initialize tool manager with client name
      this.initializeToolManager();

      // Step 5: Retrieve Unity tools
      const tools = await this.retrieveUnityTools();

      // Step 6: Build and return final response
      const result = this.buildInitializeResponseWithTools(tools);

      const executionTime = Date.now() - startTime;

      VibeLogger.logInfo(
        'client_initialization_usecase_success',
        `Client initialization completed successfully for ${this.clientInfo.name}`,
        {
          client_name: this.clientInfo.name,
          tools_count: tools.length,
          execution_time_ms: executionTime,
        },
        this.correlationId,
        'UseCase completed - instance should be discarded now',
      );

      return result;
    } catch (error) {
      const executionTime = Date.now() - startTime;
      const isUnityConnectionTimeout = error instanceof Error && error.message.includes('timeout');

      VibeLogger.logError(
        'client_initialization_usecase_failure',
        `Client initialization failed for ${this.clientInfo.name}`,
        {
          client_name: this.clientInfo.name,
          error_message: error instanceof Error ? error.message : JSON.stringify(error),
          execution_time_ms: executionTime,
          is_unity_connection_issue: isUnityConnectionTimeout,
          troubleshooting_hint: isUnityConnectionTimeout
            ? 'Unity MCP Server is likely stopped or not running. Please check Unity Editor and start the server.'
            : 'Unknown initialization error',
        },
        this.correlationId,
        isUnityConnectionTimeout
          ? 'UseCase failed - Unity Server appears to be stopped. Check Unity Editor MCP Server status.'
          : 'UseCase failed - returning minimal response',
      );

      // Return minimal response on failure
      return this.buildInitializeResponse();
    }
  }

  /**
   * Step 1: Wait for Unity connection establishment
   */
  private async waitForUnityConnection(): Promise<void> {
    VibeLogger.logDebug(
      'client_initialization_step_1',
      'Waiting for Unity connection',
      {
        client_name: this.clientInfo.name,
        timeout_ms: 10000,
      },
      this.correlationId,
      'Step 1: Unity connection prerequisite - checking if Unity MCP Server is running',
    );

    try {
      await this.connectionManager.waitForUnityConnectionWithTimeout(10000);

      VibeLogger.logDebug(
        'client_initialization_step_1_complete',
        'Unity connection established',
        { client_name: this.clientInfo.name },
        this.correlationId,
        'Step 1 complete: Ready for client registration',
      );
    } catch (error) {
      VibeLogger.logError(
        'client_initialization_step_1_failed',
        'Unity connection failed - server appears to be stopped',
        {
          client_name: this.clientInfo.name,
          error_message: error instanceof Error ? error.message : JSON.stringify(error),
          timeout_ms: 10000,
        },
        this.correlationId,
        'Unity MCP Server is not running. Please start Unity Editor and run the MCP server.',
      );

      // Re-throw to be handled by main catch block
      throw error;
    }
  }

  /**
   * Step 2: Register client with Unity (SINGLE setClientName call)
   */
  private async registerClientWithUnity(): Promise<void> {
    VibeLogger.logDebug(
      'client_initialization_step_2',
      'Registering client with Unity',
      { client_name: this.clientInfo.name },
      this.correlationId,
      'Step 2: Single setClientName call - no duplicates',
    );

    // This is the ONLY setClientName call in the entire initialization process
    await this.unityClient.setClientName(this.clientInfo.name);

    VibeLogger.logDebug(
      'client_initialization_step_2_complete',
      'Client registration completed',
      { client_name: this.clientInfo.name },
      this.correlationId,
      'Step 2 complete: Client registered with Unity',
    );
  }

  /**
   * Step 3: Configure push notification endpoint
   */
  private configurePushNotificationEndpoint(): void {
    VibeLogger.logDebug(
      'client_initialization_step_3',
      'Configuring push notification endpoint',
      { client_name: this.clientInfo.name },
      this.correlationId,
      'Step 3: Push notification setup',
    );

    const pushEndpoint = this.pushNotificationManager.getCurrentEndpoint();
    if (pushEndpoint) {
      this.unityClient.setPushNotificationEndpoint(pushEndpoint);

      VibeLogger.logDebug(
        'client_initialization_step_3_complete',
        'Push notification endpoint configured',
        {
          client_name: this.clientInfo.name,
          push_endpoint: pushEndpoint,
        },
        this.correlationId,
        'Step 3 complete: Push notifications ready',
      );
    } else {
      VibeLogger.logWarning(
        'client_initialization_step_3_no_endpoint',
        'No push notification endpoint available',
        { client_name: this.clientInfo.name },
        this.correlationId,
        'Step 3 warning: Push notifications not available',
      );
    }
  }

  /**
   * Step 4: Initialize tool manager with client name
   */
  private initializeToolManager(): void {
    VibeLogger.logDebug(
      'client_initialization_step_4',
      'Initializing tool manager',
      { client_name: this.clientInfo.name },
      this.correlationId,
      'Step 4: Tool manager setup',
    );

    this.toolManager.setClientName(this.clientInfo.name);

    VibeLogger.logDebug(
      'client_initialization_step_4_complete',
      'Tool manager initialized',
      { client_name: this.clientInfo.name },
      this.correlationId,
      'Step 4 complete: Tool manager ready',
    );
  }

  /**
   * Step 5: Retrieve Unity tools
   */
  private async retrieveUnityTools(): Promise<unknown[]> {
    VibeLogger.logDebug(
      'client_initialization_step_5',
      'Retrieving Unity tools',
      { client_name: this.clientInfo.name },
      this.correlationId,
      'Step 5: Tool retrieval from Unity',
    );

    const tools = await this.toolManager.getToolsFromUnity();

    VibeLogger.logDebug(
      'client_initialization_step_5_complete',
      'Unity tools retrieved',
      {
        client_name: this.clientInfo.name,
        tools_count: tools.length,
      },
      this.correlationId,
      'Step 5 complete: Tools ready for response',
    );

    return tools;
  }

  /**
   * Build minimal initialize response (fallback)
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
        version: (packageJson as { version: string }).version,
      },
    };
  }

  /**
   * Build initialize response with tools (success case)
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
        version: (packageJson as { version: string }).version,
      },
      tools,
    };
  }
}

export interface LLMToolInfo {
  name: string;
  version: string;
}
