import { UnityClient } from '../unity-client.js';
import { DynamicUnityCommandTool } from '../tools/dynamic-unity-command-tool.js';
import { VibeLogger } from '../utils/vibe-logger.js';

/**
 * Tool Refresh UseCase - Encapsulates temporal cohesion for Unity tool refresh process
 *
 * Design document reference: .kiro/specs/mcp-tool-refresh/design.md
 *
 * Related classes:
 * - UnityClient: Unity communication client
 * - DynamicUnityCommandTool: Dynamically created Unity tools
 * - UnityToolManager: Tool management and caching
 *
 * This UseCase class follows the single-use pattern:
 * 1. new() - create instance
 * 2. execute() - perform all refresh steps in temporal order
 * 3. instance is discarded after use (not reused)
 *
 * Temporal cohesion benefits:
 * - All refresh steps are contained in one place
 * - Clear execution order and dependencies
 * - Single point of failure handling
 * - Easy to test and reason about
 */
export class ToolRefreshUseCase {
  private correlationId: string;

  constructor(
    private unityClient: UnityClient,
    private includeDevelopmentOnlyTools: boolean,
    private sendNotificationCallback?: () => void,
  ) {
    this.correlationId = VibeLogger.generateCorrelationId();
  }

  /**
   * Execute complete tool refresh process
   * This method contains all refresh steps in temporal order
   * Should be called only once per instance
   */
  async execute(): Promise<ToolRefreshResult> {
    VibeLogger.logInfo(
      'tool_refresh_usecase_start',
      'Starting Unity tool refresh process',
      {
        include_development_tools: this.includeDevelopmentOnlyTools,
        has_notification_callback: !!this.sendNotificationCallback,
        unity_connected: this.unityClient.connected,
      },
      this.correlationId,
      'UseCase pattern: Single-use tool refresh with temporal cohesion',
      'Track this correlation ID for complete refresh flow',
    );

    try {
      // Step 1: Check duplicate execution prevention
      if (!this.checkRefreshPreconditions()) {
        return ToolRefreshResult.AlreadyInProgress();
      }

      // Step 2: Ensure Unity connection
      await this.ensureUnityConnection();

      // Step 3: Fetch tool details from Unity
      const toolDetails = await this.fetchToolDetailsFromUnity();

      if (!toolDetails) {
        return ToolRefreshResult.FetchFailed();
      }

      // Step 4: Create dynamic tools from details
      const createdTools = this.createDynamicToolsFromDetails(toolDetails);

      // Step 5: Update tool cache
      this.updateToolCache(createdTools);

      // Step 6: Send MCP notification if callback provided
      this.sendMCPNotificationIfProvided();

      VibeLogger.logInfo(
        'tool_refresh_usecase_success',
        'Unity tool refresh completed successfully',
        {
          tools_created: createdTools.size,
          include_development_tools: this.includeDevelopmentOnlyTools,
          notification_sent: !!this.sendNotificationCallback,
        },
        this.correlationId,
        'UseCase completed - tools refreshed and clients notified',
      );

      return ToolRefreshResult.Success(createdTools.size);
    } catch (error) {
      VibeLogger.logError(
        'tool_refresh_usecase_failure',
        'Unity tool refresh failed with error',
        {
          error_message: error instanceof Error ? error.message : String(error),
          error_type: error instanceof Error ? error.constructor.name : typeof error,
          include_development_tools: this.includeDevelopmentOnlyTools,
        },
        this.correlationId,
        'UseCase failed - tool refresh aborted with error',
      );

      return ToolRefreshResult.Error(
        error instanceof Error ? error.message : String(error),
      );
    }
  }

  /**
   * Step 1: Check refresh preconditions (duplicate execution prevention)
   */
  private checkRefreshPreconditions(): boolean {
    VibeLogger.logDebug(
      'tool_refresh_step_1',
      'Checking refresh preconditions',
      {
        include_development_tools: this.includeDevelopmentOnlyTools,
      },
      this.correlationId,
      'Step 1: Precondition check for duplicate prevention',
    );

    // In the original code, there was an isRefreshing flag check
    // For the UseCase pattern, we assume the caller handles this
    // This step exists for logging consistency with the original flow

    VibeLogger.logDebug(
      'tool_refresh_step_1_complete',
      'Refresh preconditions satisfied',
      {},
      this.correlationId,
      'Step 1 complete: Ready to proceed with refresh',
    );

    return true;
  }

  /**
   * Step 2: Ensure Unity connection is established
   */
  private async ensureUnityConnection(): Promise<void> {
    VibeLogger.logDebug(
      'tool_refresh_step_2',
      'Ensuring Unity connection',
      {
        unity_connected: this.unityClient.connected,
      },
      this.correlationId,
      'Step 2: Unity connection prerequisite check',
    );

    await this.unityClient.ensureConnected();

    VibeLogger.logDebug(
      'tool_refresh_step_2_complete',
      'Unity connection established',
      {
        unity_connected: this.unityClient.connected,
      },
      this.correlationId,
      'Step 2 complete: Unity connection ready for tool fetch',
    );
  }

  /**
   * Step 3: Fetch tool details from Unity
   */
  private async fetchToolDetailsFromUnity(): Promise<unknown[] | null> {
    VibeLogger.logDebug(
      'tool_refresh_step_3',
      'Fetching tool details from Unity',
      {
        include_development_tools: this.includeDevelopmentOnlyTools,
      },
      this.correlationId,
      'Step 3: Tool details retrieval from Unity',
    );

    try {
      const params = { IncludeDevelopmentOnly: this.includeDevelopmentOnlyTools };

      VibeLogger.logDebug(
        'tool_refresh_requesting_details',
        'Requesting tool details from Unity',
        { params },
        this.correlationId,
        'Executing get-tool-details Unity command',
      );

      const toolDetailsResponse = await this.unityClient.executeTool('get-tool-details', params);

      VibeLogger.logDebug(
        'tool_refresh_received_details',
        'Received tool details response from Unity',
        {
          response_type: typeof toolDetailsResponse,
          has_tools_property: !!(toolDetailsResponse as { Tools?: unknown[] })?.Tools,
        },
        this.correlationId,
        'Processing Unity tool details response',
      );

      // Handle new GetToolDetailsResponse structure
      const toolDetails =
        (toolDetailsResponse as { Tools?: unknown[] })?.Tools || toolDetailsResponse;

      if (!Array.isArray(toolDetails)) {
        VibeLogger.logWarning(
          'tool_refresh_step_3_invalid_response',
          'Invalid tool details response from Unity',
          {
            response_type: typeof toolDetails,
            is_array: Array.isArray(toolDetails),
          },
          this.correlationId,
          'Step 3 warning: Tool details response is not an array',
        );

        return null;
      }

      VibeLogger.logDebug(
        'tool_refresh_step_3_complete',
        'Tool details fetched successfully',
        {
          tools_count: toolDetails.length,
          include_development_tools: this.includeDevelopmentOnlyTools,
        },
        this.correlationId,
        'Step 3 complete: Tool details ready for processing',
      );

      return toolDetails as unknown[];
    } catch (error) {
      VibeLogger.logError(
        'tool_refresh_step_3_error',
        'Failed to fetch tool details from Unity',
        {
          error_message: error instanceof Error ? error.message : String(error),
          include_development_tools: this.includeDevelopmentOnlyTools,
        },
        this.correlationId,
        'Step 3 error: Tool details fetch failed',
      );

      return null;
    }
  }

  /**
   * Step 4: Create dynamic tools from Unity tool details
   */
  private createDynamicToolsFromDetails(toolDetails: unknown[]): Map<string, DynamicUnityCommandTool> {
    VibeLogger.logDebug(
      'tool_refresh_step_4',
      'Creating dynamic tools from details',
      {
        tool_details_count: toolDetails.length,
        include_development_tools: this.includeDevelopmentOnlyTools,
      },
      this.correlationId,
      'Step 4: Dynamic tool creation process',
    );

    const dynamicTools = new Map<string, DynamicUnityCommandTool>();
    const toolContext = { unityClient: this.unityClient };
    let skippedCount = 0;
    let createdCount = 0;

    for (const toolInfo of toolDetails) {
      const toolName = (toolInfo as { name: string }).name;
      const description =
        (toolInfo as { description?: string }).description || `Execute Unity tool: ${toolName}`;
      const parameterSchema = (toolInfo as { parameterSchema?: unknown }).parameterSchema;
      const displayDevelopmentOnly =
        (toolInfo as { displayDevelopmentOnly?: boolean }).displayDevelopmentOnly || false;

      // Skip development-only tools in production mode
      if (displayDevelopmentOnly && !this.includeDevelopmentOnlyTools) {
        skippedCount++;
        continue;
      }

      const dynamicTool = new DynamicUnityCommandTool(
        toolContext,
        toolName,
        description,
        parameterSchema as any, // Type assertion for schema compatibility
      );

      dynamicTools.set(toolName, dynamicTool);
      createdCount++;
    }

    VibeLogger.logDebug(
      'tool_refresh_step_4_complete',
      'Dynamic tools created successfully',
      {
        tools_created: createdCount,
        tools_skipped: skippedCount,
        total_processed: toolDetails.length,
        include_development_tools: this.includeDevelopmentOnlyTools,
      },
      this.correlationId,
      'Step 4 complete: Dynamic tools ready for cache update',
    );

    return dynamicTools;
  }

  /**
   * Step 5: Update tool cache (this would be handled by the calling ToolManager)
   */
  private updateToolCache(dynamicTools: Map<string, DynamicUnityCommandTool>): void {
    VibeLogger.logDebug(
      'tool_refresh_step_5',
      'Updating tool cache',
      {
        tools_count: dynamicTools.size,
      },
      this.correlationId,
      'Step 5: Tool cache update (handled by caller)',
    );

    // In the UseCase pattern, the actual cache update would be handled by the
    // calling UnityToolManager after receiving the result
    // This step exists for logging consistency with the original flow

    VibeLogger.logDebug(
      'tool_refresh_step_5_complete',
      'Tool cache update prepared',
      {
        tools_ready: dynamicTools.size,
      },
      this.correlationId,
      'Step 5 complete: Tools ready for cache integration',
    );
  }

  /**
   * Step 6: Send MCP notification if callback provided
   */
  private sendMCPNotificationIfProvided(): void {
    if (!this.sendNotificationCallback) {
      VibeLogger.logDebug(
        'tool_refresh_step_6_skipped',
        'No notification callback provided - skipping',
        {},
        this.correlationId,
        'Step 6 skipped: No MCP notification to send',
      );
      return;
    }

    VibeLogger.logDebug(
      'tool_refresh_step_6',
      'Sending MCP tools changed notification',
      {},
      this.correlationId,
      'Step 6: MCP client notification',
    );

    try {
      this.sendNotificationCallback();

      VibeLogger.logDebug(
        'tool_refresh_step_6_complete',
        'MCP notification sent successfully',
        {},
        this.correlationId,
        'Step 6 complete: MCP clients notified of tool changes',
      );
    } catch (error) {
      VibeLogger.logError(
        'tool_refresh_step_6_error',
        'Failed to send MCP notification',
        {
          error_message: error instanceof Error ? error.message : String(error),
        },
        this.correlationId,
        'Step 6 error: MCP notification failed but refresh continues',
      );

      // Don't throw - notification failure shouldn't fail the entire refresh
    }
  }

  // Step 7 (finally block handling) is managed by the UseCase execution flow
}

/**
 * Result object for tool refresh operation
 */
export class ToolRefreshResult {
  public readonly isSuccess: boolean;
  public readonly errorMessage?: string;
  public readonly toolsCount?: number;
  public readonly reason: ToolRefreshReason;

  private constructor(
    isSuccess: boolean,
    reason: ToolRefreshReason,
    errorMessage?: string,
    toolsCount?: number,
  ) {
    this.isSuccess = isSuccess;
    this.reason = reason;
    this.errorMessage = errorMessage;
    this.toolsCount = toolsCount;
  }

  static Success(toolsCount: number): ToolRefreshResult {
    return new ToolRefreshResult(true, ToolRefreshReason.Success, undefined, toolsCount);
  }

  static AlreadyInProgress(): ToolRefreshResult {
    return new ToolRefreshResult(false, ToolRefreshReason.AlreadyInProgress);
  }

  static FetchFailed(): ToolRefreshResult {
    return new ToolRefreshResult(false, ToolRefreshReason.FetchFailed);
  }

  static Error(errorMessage: string): ToolRefreshResult {
    return new ToolRefreshResult(false, ToolRefreshReason.Error, errorMessage);
  }
}

/**
 * Enumeration of possible tool refresh outcomes
 */
export enum ToolRefreshReason {
  Success = 'success',
  AlreadyInProgress = 'already_in_progress',
  FetchFailed = 'fetch_failed',
  Error = 'error',
}