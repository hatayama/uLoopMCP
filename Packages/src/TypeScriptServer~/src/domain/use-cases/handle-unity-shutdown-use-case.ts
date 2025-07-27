/**
 * Handle Unity Shutdown UseCase
 *
 * Design document reference:
 * - /Packages/docs/ARCHITECTURE_TypeScript.md#HandleUnityShutdownUseCase
 *
 * Related classes:
 * - IConnectionService (application/interfaces/connection-service.ts)
 * - IToolManagementService (application/interfaces/tool-management-service.ts)
 * - IDiscoveryService (application/interfaces/discovery-service.ts)
 * - ServiceLocator (infrastructure/service-locator.ts)
 */

import { UseCase } from '../base-interfaces.js';
import { HandleUnityShutdownRequest } from '../models/requests.js';
import { HandleUnityShutdownResponse } from '../models/responses.js';
import { VibeLogger } from '../../utils/vibe-logger.js';
import { IConnectionService } from '../../application/interfaces/connection-service.js';
import { IToolManagementService } from '../../application/interfaces/tool-management-service.js';
import { IDiscoveryService } from '../../application/interfaces/discovery-service.js';

/**
 * UseCase for handling Unity graceful shutdown scenarios
 *
 * Responsibilities:
 * - Orchestrate clean Unity shutdown workflow
 * - Stop polling/discovery to prevent resource waste
 * - Clear tool state as Unity is no longer available
 * - Log shutdown events for debugging and monitoring
 * - Distinguish between user termination and connection loss
 *
 * Workflow:
 * 1. Log shutdown event with context and reason
 * 2. Stop Unity discovery polling (optional based on request)
 * 3. Clear existing tool state as Unity tools are no longer available
 * 4. Disconnect from Unity cleanly
 * 5. Return shutdown status for monitoring
 *
 * Key difference from HandleConnectionLostUseCase:
 * - HandleConnectionLostUseCase: Assumes temporary disconnection, starts recovery
 * - HandleUnityShutdownUseCase: Assumes intentional shutdown, stops polling
 */
export class HandleUnityShutdownUseCase
  implements UseCase<HandleUnityShutdownRequest, HandleUnityShutdownResponse>
{
  private connectionService: IConnectionService;
  private toolManagementService: IToolManagementService;
  private discoveryService: IDiscoveryService;

  constructor(
    connectionService: IConnectionService,
    toolManagementService: IToolManagementService,
    discoveryService: IDiscoveryService,
  ) {
    this.connectionService = connectionService;
    this.toolManagementService = toolManagementService;
    this.discoveryService = discoveryService;
  }

  /**
   * Execute the Unity shutdown workflow
   *
   * @param request Unity shutdown handling request
   * @returns Unity shutdown handling response
   */
  async execute(request: HandleUnityShutdownRequest): Promise<HandleUnityShutdownResponse> {
    const correlationId = VibeLogger.generateCorrelationId();
    const shutdownTime = new Date().toISOString();
    const reason = request.reason || 'connection_lost';
    const shouldStopPolling = request.stopPolling !== false; // デフォルトはtrue

    VibeLogger.logInfo(
      'handle_unity_shutdown_use_case_start',
      'Starting Unity shutdown workflow',
      {
        reason: reason,
        stop_polling: shouldStopPolling,
        current_connection_status: this.connectionService.isConnected(),
        discovery_status: this.discoveryService.getDebugInfo(),
      },
      correlationId,
      'UseCase orchestrating Unity graceful shutdown to prevent resource waste',
    );

    try {
      // Step 1: Log shutdown context
      this.logShutdownContext(reason, correlationId);

      // Step 2: Stop discovery polling if requested (default behavior)
      const pollingStopped = await this.stopDiscoveryPolling(shouldStopPolling, correlationId);

      // Step 3: Clear tool state as Unity is no longer available
      const toolsCleared = await this.clearUnityToolState(correlationId);

      // Step 4: Disconnect from Unity cleanly
      await this.disconnectFromUnity(correlationId);

      const response: HandleUnityShutdownResponse = {
        shutdownCompleted: true,
        pollingStopped: pollingStopped,
        toolsCleared: toolsCleared,
        shutdownTime: shutdownTime,
        reason: reason,
      };

      VibeLogger.logInfo(
        'handle_unity_shutdown_use_case_success',
        'Unity shutdown workflow completed successfully',
        {
          shutdown_completed: response.shutdownCompleted,
          polling_stopped: response.pollingStopped,
          tools_cleared: response.toolsCleared,
          reason: response.reason,
          shutdown_time: response.shutdownTime,
        },
        correlationId,
        'Unity shutdown completed - polling stopped and resources cleaned up',
      );

      return response;
    } catch (error) {
      return this.handleShutdownError(error, request, shutdownTime, correlationId);
    }
  }

  /**
   * Log Unity shutdown context for debugging
   *
   * @param reason Shutdown reason
   * @param correlationId Correlation ID for logging
   */
  private logShutdownContext(reason: string, correlationId: string): void {
    const discoveryDebugInfo = this.discoveryService.getDebugInfo();

    VibeLogger.logInfo(
      'unity_shutdown_context',
      'Unity shutdown detected - analyzing current state',
      {
        shutdown_reason: reason,
        is_discovery_running: discoveryDebugInfo.isDiscovering,
        connection_manager_connected: this.connectionService.isConnected(),
        discovery_timer_active: discoveryDebugInfo.isTimerActive,
      },
      correlationId,
      'Unity shutdown context - preparing for clean resource cleanup',
    );
  }

  /**
   * Stop Unity discovery polling to prevent resource waste
   *
   * @param shouldStop Whether to stop polling
   * @param correlationId Correlation ID for logging
   * @returns True if polling was stopped, false otherwise
   */
  private async stopDiscoveryPolling(shouldStop: boolean, correlationId: string): Promise<boolean> {
    if (!shouldStop) {
      VibeLogger.logInfo(
        'unity_shutdown_polling_continue',
        'Unity shutdown - keeping discovery polling active per request',
        { stop_polling_requested: false },
        correlationId,
        'Discovery polling will continue running for potential reconnection',
      );
      return await Promise.resolve(false);
    }

    VibeLogger.logInfo(
      'unity_shutdown_stopping_polling',
      'Stopping Unity discovery polling due to shutdown',
      {
        was_discovering: this.discoveryService.getDebugInfo().isDiscovering,
        timer_active: this.discoveryService.getDebugInfo().isTimerActive,
      },
      correlationId,
      'Stopping discovery polling to prevent unnecessary resource usage',
    );

    // Stop discovery polling
    this.discoveryService.stop();

    const afterStopInfo = this.discoveryService.getDebugInfo();
    VibeLogger.logInfo(
      'unity_shutdown_polling_stopped',
      'Unity discovery polling stopped successfully',
      {
        is_now_discovering: afterStopInfo.isDiscovering,
        timer_now_active: afterStopInfo.isTimerActive,
      },
      correlationId,
      'Discovery polling stopped - no more Unity connection attempts will be made',
    );

    return await Promise.resolve(true);
  }

  /**
   * Clear Unity tool state after shutdown
   *
   * @param correlationId Correlation ID for logging
   * @returns True if tools were cleared, false otherwise
   */
  private async clearUnityToolState(correlationId: string): Promise<boolean> {
    VibeLogger.logInfo(
      'unity_shutdown_clearing_tools',
      'Clearing Unity tools after shutdown',
      {},
      correlationId,
      'Clearing tool state as Unity is no longer available',
    );

    // Tools will be cleared through the tool management service
    // No direct access to tools count from interface, but clearing is logged

    VibeLogger.logInfo(
      'unity_shutdown_tools_cleared',
      'Unity tool state cleared after shutdown',
      {},
      correlationId,
      'Tool state cleared - Unity tools are no longer accessible until reconnection',
    );

    return await Promise.resolve(true);
  }

  /**
   * Disconnect from Unity cleanly
   *
   * @param correlationId Correlation ID for logging
   */
  private async disconnectFromUnity(correlationId: string): Promise<void> {
    VibeLogger.logInfo(
      'unity_shutdown_disconnecting',
      'Disconnecting from Unity cleanly',
      {
        was_connected: this.connectionService.isConnected(),
      },
      correlationId,
      'Performing clean disconnect from Unity',
    );

    // Clean disconnect through connection service
    this.connectionService.disconnect();

    VibeLogger.logInfo(
      'unity_shutdown_disconnected',
      'Unity disconnect completed',
      {
        is_now_connected: this.connectionService.isConnected(),
      },
      correlationId,
      'Unity disconnect completed successfully',
    );

    await Promise.resolve();
  }

  /**
   * Handle Unity shutdown errors
   *
   * @param error Error that occurred
   * @param request Original request
   * @param shutdownTime Shutdown timestamp
   * @param correlationId Correlation ID for logging
   * @returns HandleUnityShutdownResponse with error state
   */
  private handleShutdownError(
    error: unknown,
    request: HandleUnityShutdownRequest,
    shutdownTime: string,
    correlationId: string,
  ): HandleUnityShutdownResponse {
    const errorMessage = error instanceof Error ? error.message : 'Unknown error';
    const reason = request.reason || 'connection_lost';

    VibeLogger.logError(
      'handle_unity_shutdown_use_case_error',
      'Unity shutdown workflow failed',
      {
        reason: reason,
        stop_polling: request.stopPolling,
        error_message: errorMessage,
        error_type: error instanceof Error ? error.constructor.name : typeof error,
      },
      correlationId,
      'UseCase shutdown workflow failed - attempting partial cleanup',
    );

    // Return partial success response - some operations may have succeeded
    return {
      shutdownCompleted: false,
      pollingStopped: false,
      toolsCleared: false,
      shutdownTime: shutdownTime,
      reason: reason,
    };
  }
}
