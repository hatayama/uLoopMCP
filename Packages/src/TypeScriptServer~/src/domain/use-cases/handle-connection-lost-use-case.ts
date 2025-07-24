/**
 * Handle Connection Lost UseCase
 *
 * Design document reference:
 * - .kiro/specs/typescript-server-ddd-refactoring/design.md#HandleConnectionLostUseCase
 *
 * Related classes:
 * - IConnectionService (application/interfaces/connection-service.ts)
 * - IToolService (application/interfaces/tool-service.ts)
 * - IDiscoveryService (application/interfaces/discovery-service.ts)
 * - ServiceLocator (infrastructure/service-locator.ts)
 */

import { UseCase } from '../base-interfaces.js';
import { HandleConnectionRequest } from '../models/requests.js';
import { HandleConnectionResponse } from '../models/responses.js';
import { VibeLogger } from '../../utils/vibe-logger.js';
import { IConnectionService } from '../../application/interfaces/connection-service.js';
import { IToolManagementService } from '../../application/interfaces/tool-management-service.js';
import { IDiscoveryService } from '../../application/interfaces/discovery-service.js';

/**
 * UseCase for handling Unity connection lost scenarios
 *
 * Responsibilities:
 * - Orchestrate the complete connection lost recovery workflow
 * - Manage Unity discovery restart for reconnection
 * - Handle tool refresh after reconnection
 * - Coordinate temporal cohesion of recovery process
 * - Provide resilient recovery from connection failures
 *
 * Workflow:
 * 1. Log connection lost event with context
 * 2. Restart Unity discovery for reconnection attempts
 * 3. Clear existing tool state as Unity tools may be outdated
 * 4. Setup reconnection monitoring
 * 5. Return connection status for monitoring
 */
export class HandleConnectionLostUseCase
  implements UseCase<HandleConnectionRequest, HandleConnectionResponse>
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
   * Execute the connection lost recovery workflow
   *
   * @param request Connection handling request
   * @returns Connection handling response
   */
  async execute(request: HandleConnectionRequest): Promise<HandleConnectionResponse> {
    const correlationId = VibeLogger.generateCorrelationId();

    VibeLogger.logWarning(
      'handle_connection_lost_use_case_start',
      'Starting connection lost recovery workflow',
      {
        port: request.port,
        timeout: request.timeout,
        current_connection_status: this.connectionService.isConnected(),
      },
      correlationId,
      'UseCase orchestrating Unity connection lost recovery for domain reload resilience',
    );

    try {
      // Step 1: Log connection lost context
      this.logConnectionLostContext(correlationId);

      // Step 2: Clear outdated tool state
      this.clearOutdatedToolState(correlationId);

      // Step 3: Restart Unity discovery for reconnection
      this.restartUnityDiscovery(correlationId);

      // Step 4: Setup reconnection monitoring
      const connectionStatus = await this.setupReconnectionMonitoring(request, correlationId);

      VibeLogger.logInfo(
        'handle_connection_lost_use_case_success',
        'Connection lost recovery workflow initiated successfully',
        {
          connection_restored: connectionStatus.connected,
          port: connectionStatus.port,
          recovery_time: connectionStatus.connectionTime,
        },
        correlationId,
        'Connection lost recovery completed - Unity discovery restarted and tools cleared',
      );

      return connectionStatus;
    } catch (error) {
      return this.handleRecoveryError(error, request, correlationId);
    }
  }

  /**
   * Log connection lost context for debugging
   *
   * @param correlationId Correlation ID for logging
   */
  private logConnectionLostContext(correlationId: string): void {
    const currentToolsCount = this.toolManagementService.getToolsCount();
    const isDiscoveryRunning = this.discoveryService.getIsDiscovering();

    VibeLogger.logWarning(
      'connection_lost_context',
      'Unity connection lost - analyzing current state',
      {
        tools_count_before_loss: currentToolsCount,
        is_discovery_running: isDiscoveryRunning,
        connection_manager_connected: this.connectionService.isConnected(),
      },
      correlationId,
      'Connection lost context - tools may become stale until reconnection',
    );
  }

  /**
   * Clear outdated tool state after connection loss
   *
   * @param correlationId Correlation ID for logging
   */
  private clearOutdatedToolState(correlationId: string): void {
    const toolsCountBefore = this.toolManagementService.getToolsCount();

    VibeLogger.logInfo(
      'connection_lost_clearing_tools',
      'Clearing potentially outdated Unity tools after connection loss',
      { tools_count_before: toolsCountBefore },
      correlationId,
      'Clearing tool state to prevent stale tool usage after connection loss',
    );

    // Clear dynamic tools map as Unity state is now unknown
    const dynamicTools = this.toolManagementService.getDynamicTools();
    dynamicTools.clear();

    VibeLogger.logInfo(
      'connection_lost_tools_cleared',
      'Outdated Unity tools cleared successfully',
      {
        tools_count_before: toolsCountBefore,
        tools_count_after: this.toolManagementService.getToolsCount(),
      },
      correlationId,
      'Tool state cleared - fresh tools will be loaded on reconnection',
    );
  }

  /**
   * Restart Unity discovery for reconnection attempts
   *
   * @param correlationId Correlation ID for logging
   */
  private restartUnityDiscovery(correlationId: string): void {
    VibeLogger.logInfo(
      'connection_lost_restarting_discovery',
      'Restarting Unity discovery for reconnection attempts',
      {
        was_discovering: this.discoveryService.getIsDiscovering(),
      },
      correlationId,
      'Restarting discovery service to attempt Unity reconnection',
    );

    // Restart discovery if not already running (singleton pattern prevents duplicates)
    this.discoveryService.start();

    VibeLogger.logInfo(
      'connection_lost_discovery_restarted',
      'Unity discovery restarted successfully',
      {
        is_now_discovering: this.discoveryService.getIsDiscovering(),
      },
      correlationId,
      'Discovery service restarted - actively searching for Unity reconnection',
    );
  }

  /**
   * Setup reconnection monitoring and attempt immediate reconnection
   *
   * @param request Connection handling request with timeout settings
   * @param correlationId Correlation ID for logging
   * @returns Connection status after recovery attempt
   */
  private async setupReconnectionMonitoring(
    request: HandleConnectionRequest,
    correlationId: string,
  ): Promise<HandleConnectionResponse> {
    const timeout = request.timeout || 5000; // Default 5 second timeout for recovery
    const connectionTime = new Date().toISOString();

    VibeLogger.logInfo(
      'connection_lost_monitoring_setup',
      'Setting up reconnection monitoring',
      {
        timeout_ms: timeout,
        port: request.port,
      },
      correlationId,
      'Monitoring Unity reconnection with timeout for connection recovery',
    );

    try {
      // Attempt to wait for reconnection with shorter timeout than normal initialization
      await this.connectionService.waitForUnityConnectionWithTimeout(timeout);

      if (this.connectionService.isConnected()) {
        VibeLogger.logInfo(
          'connection_lost_recovery_success',
          'Unity connection successfully restored',
          {
            port: request.port || 8700,
            recovery_time_ms: timeout,
            connection_time: connectionTime,
          },
          correlationId,
          'Connection recovery successful - Unity reconnected and ready for tool refresh',
        );

        return {
          connected: true,
          port: request.port || 8700,
          connectionTime: connectionTime,
        };
      }
    } catch (error) {
      VibeLogger.logWarning(
        'connection_lost_recovery_timeout',
        'Unity reconnection timeout - continuing with background discovery',
        {
          timeout_ms: timeout,
          error_message: error instanceof Error ? error.message : String(error),
        },
        correlationId,
        'Immediate reconnection failed - discovery continues in background',
      );
    }

    // Return disconnected status if immediate reconnection failed
    return {
      connected: false,
      port: request.port || 8700,
      connectionTime: connectionTime,
    };
  }

  /**
   * Handle connection recovery errors
   *
   * @param error Error that occurred
   * @param request Original request
   * @param correlationId Correlation ID for logging
   * @returns HandleConnectionResponse with error state
   */
  private handleRecoveryError(
    error: unknown,
    request: HandleConnectionRequest,
    correlationId: string,
  ): HandleConnectionResponse {
    const errorMessage = error instanceof Error ? error.message : 'Unknown error';

    VibeLogger.logError(
      'handle_connection_lost_use_case_error',
      'Connection lost recovery workflow failed',
      {
        port: request.port,
        timeout: request.timeout,
        error_message: errorMessage,
        error_type: error instanceof Error ? error.constructor.name : typeof error,
      },
      correlationId,
      'UseCase recovery workflow failed - returning disconnected state',
    );

    // Return disconnected response - recovery should be resilient
    return {
      connected: false,
      port: request.port || 8700,
      connectionTime: new Date().toISOString(),
    };
  }
}

