/**
 * Refresh Tools UseCase
 *
 * Design document reference:
 * - .kiro/specs/typescript-server-ddd-refactoring/design.md#RefreshToolsUseCase
 *
 * Related classes:
 * - IConnectionService (application/interfaces/connection-service.ts)
 * - IToolService (application/interfaces/tool-service.ts)
 * - ServiceLocator (infrastructure/service-locator.ts)
 */

import { UseCase } from '../base-interfaces.js';
import { RefreshToolsRequest } from '../models/requests.js';
import { RefreshToolsResponse } from '../models/responses.js';
import { UnityToolManager } from '../../unity-tool-manager.js';
import { UnityConnectionManager } from '../../unity-connection-manager.js';
import { ConnectionError } from '../errors.js';
import { VibeLogger } from '../../utils/vibe-logger.js';

/**
 * UseCase for refreshing Unity tools
 *
 * Responsibilities:
 * - Orchestrate the complete tool refresh workflow
 * - Handle Unity reconnection scenarios (domain reload recovery)
 * - Manage temporal cohesion of tool refresh process
 * - Coordinate tool refresh with notification sending
 *
 * Workflow:
 * 1. Ensure Unity connection is established
 * 2. Initialize/refresh dynamic tools from Unity
 * 3. Return refreshed tools list
 * 4. Support notification callback for MCP client updates
 */
export class RefreshToolsUseCase implements UseCase<RefreshToolsRequest, RefreshToolsResponse> {
  private connectionManager: UnityConnectionManager;
  private toolManager: UnityToolManager;

  constructor(connectionManager: UnityConnectionManager, toolManager: UnityToolManager) {
    this.connectionManager = connectionManager;
    this.toolManager = toolManager;
  }

  /**
   * Execute the tool refresh workflow
   *
   * @param request Tool refresh request
   * @returns Tool refresh response
   */
  async execute(request: RefreshToolsRequest): Promise<RefreshToolsResponse> {
    const correlationId = VibeLogger.generateCorrelationId();

    VibeLogger.logInfo(
      'refresh_tools_use_case_start',
      'Starting tool refresh workflow',
      { include_development: request.includeDevelopmentOnly },
      correlationId,
      'UseCase orchestrating tool refresh workflow for domain reload recovery',
    );

    try {
      // Step 1: Ensure Unity connection is established (critical for domain reload recovery)
      await this.ensureUnityConnection(correlationId);

      // Step 2: Initialize/refresh dynamic tools from Unity
      await this.refreshToolsFromUnity(correlationId);

      // Step 3: Get refreshed tools list
      const refreshedTools = this.toolManager.getAllTools();

      const response: RefreshToolsResponse = {
        tools: refreshedTools,
        refreshedAt: new Date().toISOString(),
      };

      VibeLogger.logInfo(
        'refresh_tools_use_case_success',
        'Tool refresh workflow completed successfully',
        {
          tool_count: refreshedTools.length,
          refreshed_at: response.refreshedAt,
        },
        correlationId,
        'Tool refresh completed successfully - Unity tools updated after domain reload',
      );

      return response;
    } catch (error) {
      return this.handleRefreshError(error, request, correlationId);
    }
  }

  /**
   * Ensure Unity connection is established
   *
   * @param correlationId Correlation ID for logging
   * @throws ConnectionError if connection cannot be established
   */
  private async ensureUnityConnection(correlationId: string): Promise<void> {
    if (!this.connectionManager.isConnected()) {
      VibeLogger.logWarning(
        'refresh_tools_unity_not_connected',
        'Unity not connected during tool refresh, attempting to establish connection',
        { connected: false },
        correlationId,
        'Unity connection required for tool refresh after domain reload',
      );

      try {
        await this.connectionManager.waitForUnityConnectionWithTimeout(10000);
      } catch (error) {
        throw new ConnectionError(
          `Cannot refresh tools: Unity connection failed - ${error instanceof Error ? error.message : 'Unknown error'}`,
          { original_error: error },
        );
      }
    }

    VibeLogger.logDebug(
      'refresh_tools_connection_verified',
      'Unity connection verified for tool refresh',
      { connected: true },
      correlationId,
      'Connection ready for tool refresh after domain reload',
    );
  }

  /**
   * Refresh tools from Unity by re-initializing dynamic tools
   *
   * @param correlationId Correlation ID for logging
   */
  private async refreshToolsFromUnity(correlationId: string): Promise<void> {
    try {
      VibeLogger.logDebug(
        'refresh_tools_initializing',
        'Re-initializing dynamic tools from Unity',
        {},
        correlationId,
        'Fetching latest tool definitions from Unity after domain reload',
      );

      // Re-initialize tools (this will fetch latest tool definitions from Unity)
      await this.toolManager.initializeDynamicTools();

      VibeLogger.logInfo(
        'refresh_tools_initialized',
        'Dynamic tools re-initialized successfully from Unity',
        { tool_count: this.toolManager.getToolsCount() },
        correlationId,
        'Tool definitions updated from Unity after domain reload',
      );
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : String(error);
      VibeLogger.logError(
        'refresh_tools_initialization_failed',
        'Failed to re-initialize dynamic tools from Unity',
        { error_message: errorMessage },
        correlationId,
        'Tool refresh failed during Unity communication',
      );

      throw new ConnectionError(`Tool refresh failed: ${errorMessage}`, { original_error: error });
    }
  }

  /**
   * Handle tool refresh errors
   *
   * @param error Error that occurred
   * @param request Original request
   * @param correlationId Correlation ID for logging
   * @returns RefreshToolsResponse with error state
   */
  private handleRefreshError(
    error: unknown,
    request: RefreshToolsRequest,
    correlationId: string,
  ): RefreshToolsResponse {
    const errorMessage = error instanceof Error ? error.message : 'Unknown error';

    VibeLogger.logError(
      'refresh_tools_use_case_error',
      'Tool refresh workflow failed',
      {
        include_development: request.includeDevelopmentOnly,
        error_message: errorMessage,
        error_type: error instanceof Error ? error.constructor.name : typeof error,
      },
      correlationId,
      'UseCase workflow failed - returning empty tools list to prevent client errors',
    );

    // Return empty tools list instead of throwing - tool refresh should be resilient
    return {
      tools: [],
      refreshedAt: new Date().toISOString(),
    };
  }
}

/**
 * Factory function for creating RefreshToolsUseCase instances
 *
 * @returns New RefreshToolsUseCase instance with injected dependencies
 */
export function createRefreshToolsUseCase(): RefreshToolsUseCase {
  // Phase 3.2: Create temporary factory that will be replaced in Phase 4
  // For now, we need external injection of dependencies
  // This will be properly implemented when ServiceLocator is fully configured

  throw new Error(
    'RefreshToolsUseCase factory needs to be initialized with concrete dependencies in Phase 3.2',
  );
}
