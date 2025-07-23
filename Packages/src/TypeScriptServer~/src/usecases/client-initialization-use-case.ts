import { InitializeResult } from '@modelcontextprotocol/sdk/types.js';
import { UnityClient } from '../unity-client.js';
import { UnityToolManager } from '../unity-tool-manager.js';
import { UnityConnectionManager } from '../unity-connection-manager.js';
import { UnityPushNotificationManager } from '../unity-push-notification-manager.js';
import { VibeLogger } from '../utils/vibe-logger.js';
import { UnityConnectionUtil } from '../utils/unity-connection-util.js';
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
      // Step 1: Discover and connect to Unity directly
      VibeLogger.logInfo(
        'client_initialization_entering_step_1',
        'ENTERING STEP 1: Discover and connect to Unity',
        { client_name: this.clientInfo.name },
        this.correlationId,
      );
      await this.discoverAndConnectToUnity();
      VibeLogger.logInfo(
        'client_initialization_step_1_success',
        'STEP 1 COMPLETED: Unity connection established',
        { client_name: this.clientInfo.name },
        this.correlationId,
      );

      // Step 2: Register client with Unity (single setClientName call)
      VibeLogger.logInfo(
        'client_initialization_entering_step_2',
        'ENTERING STEP 2: Register client with Unity',
        { client_name: this.clientInfo.name },
        this.correlationId,
      );
      await this.registerClientWithUnity();
      VibeLogger.logInfo(
        'client_initialization_step_2_success',
        'STEP 2 COMPLETED: Client registered with Unity',
        { client_name: this.clientInfo.name },
        this.correlationId,
      );

      // Step 3: Configure push notification endpoint
      VibeLogger.logInfo(
        'client_initialization_entering_step_3',
        'ENTERING STEP 3: Configure push notification endpoint',
        { client_name: this.clientInfo.name },
        this.correlationId,
      );
      this.configurePushNotificationEndpoint();
      VibeLogger.logInfo(
        'client_initialization_step_3_success',
        'STEP 3 COMPLETED: Push notification configured',
        { client_name: this.clientInfo.name },
        this.correlationId,
      );

      // Step 4: Initialize tool manager with client name
      VibeLogger.logInfo(
        'client_initialization_entering_step_4',
        'ENTERING STEP 4: Initialize tool manager',
        { client_name: this.clientInfo.name },
        this.correlationId,
      );
      this.initializeToolManager();
      VibeLogger.logInfo(
        'client_initialization_step_4_success',
        'STEP 4 COMPLETED: Tool manager initialized',
        { client_name: this.clientInfo.name },
        this.correlationId,
      );

      // Step 5: Retrieve Unity tools
      VibeLogger.logInfo(
        'client_initialization_entering_step_5',
        'ENTERING STEP 5: Retrieve Unity tools',
        { client_name: this.clientInfo.name },
        this.correlationId,
      );
      const tools = await this.retrieveUnityTools();
      VibeLogger.logInfo(
        'client_initialization_step_5_success',
        'STEP 5 COMPLETED: Unity tools retrieved',
        { client_name: this.clientInfo.name, tools_count: tools.length },
        this.correlationId,
      );

      // Step 6: Build and return final response
      VibeLogger.logInfo(
        'client_initialization_entering_step_6',
        'ENTERING STEP 6: Build final response',
        { client_name: this.clientInfo.name },
        this.correlationId,
      );
      const result = this.buildInitializeResponseWithTools(tools);
      VibeLogger.logInfo(
        'client_initialization_step_6_success',
        'STEP 6 COMPLETED: Final response built',
        { client_name: this.clientInfo.name },
        this.correlationId,
      );

      const executionTime = Date.now() - startTime;

      // Step 7: Start Unity discovery polling after successful initialization
      VibeLogger.logInfo(
        'client_initialization_entering_step_7',
        'ENTERING STEP 7: Start Unity discovery polling',
        { client_name: this.clientInfo.name },
        this.correlationId,
      );
      this.connectionManager.startDiscoveryPolling();
      VibeLogger.logInfo(
        'client_initialization_step_7_success',
        'STEP 7 COMPLETED: Unity discovery polling started',
        { client_name: this.clientInfo.name },
        this.correlationId,
      );

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
   * Step 1: Discover and connect to Unity directly (no circular dependencies)
   */
  private async discoverAndConnectToUnity(): Promise<void> {
    VibeLogger.logDebug(
      'client_initialization_step_1',
      'Discovering and connecting to Unity directly',
      {
        client_name: this.clientInfo.name,
        target_port: 8700,
      },
      this.correlationId,
      'Step 1: Direct Unity discovery and connection (no circular dependencies)',
    );

    try {
      // Get Unity TCP port from environment
      const unityTcpPort = process.env.UNITY_TCP_PORT;
      if (!unityTcpPort) {
        throw new Error('UNITY_TCP_PORT environment variable is required but not set');
      }

      const port = parseInt(unityTcpPort, 10);
      if (isNaN(port) || port <= 0 || port > 65535) {
        throw new Error(
          `UNITY_TCP_PORT must be a valid port number (1-65535), got: ${unityTcpPort}`,
        );
      }

      VibeLogger.logInfo(
        'client_initialization_step_1_discover_and_connect',
        'STEP 1.1: Using utility to discover and connect to Unity',
        {
          client_name: this.clientInfo.name,
          target_port: port,
        },
        this.correlationId,
      );

      // Use utility function for clean discovery and connection
      const result = await UnityConnectionUtil.discoverAndConnect(
        this.unityClient,
        port,
        this.correlationId,
        'client_initialization',
      );

      if (!result.success) {
        throw new Error('Unity server not found or connection failed');
      }

      VibeLogger.logInfo(
        'client_initialization_step_1_connection_success',
        'STEP 1.2: Unity connection established successfully',
        {
          client_name: this.clientInfo.name,
          connected_port: result.port,
          unity_connected: this.unityClient.connected,
        },
        this.correlationId,
      );

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
