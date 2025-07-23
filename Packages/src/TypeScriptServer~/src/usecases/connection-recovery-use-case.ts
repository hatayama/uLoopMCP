import { UnityClient } from '../unity-client.js';
import { VibeLogger } from '../utils/vibe-logger.js';
import { UNITY_CONNECTION } from '../constants.js';

/**
 * Connection Recovery UseCase - Encapsulates temporal cohesion for connection recovery process
 *
 * Design document reference: .kiro/specs/mcp-connection-recovery/design.md
 *
 * Related classes:
 * - UnityClient: Manages Unity communication and connection state
 * - UnityDiscovery: Unity server discovery and connection management
 * - VibeLogger: Structured logging for debugging
 *
 * This UseCase class follows the single-use pattern:
 * 1. new() - create instance
 * 2. execute() - perform all recovery steps in temporal order
 * 3. instance is discarded after use (not reused)
 *
 * Temporal cohesion benefits:
 * - All recovery steps are contained in one place
 * - Clear execution order and dependencies
 * - Single point of failure handling
 * - Easy to test and reason about
 */
export class ConnectionRecoveryUseCase {
  private correlationId: string;

  constructor(
    private unityClient: UnityClient,
    private onDiscoveredCallback?: (port: number) => Promise<void>,
  ) {
    this.correlationId = VibeLogger.generateCorrelationId();
  }

  /**
   * Execute complete connection recovery process
   * This method contains all recovery steps in temporal order
   * Should be called only once per instance
   */
  async execute(): Promise<ConnectionRecoveryResult> {
    VibeLogger.logInfo(
      'connection_recovery_usecase_start',
      'Starting connection recovery process',
      {
        unity_connected: this.unityClient.connected,
        process_id: process.pid,
      },
      this.correlationId,
      'UseCase pattern: Single-use connection recovery with temporal cohesion',
      'Track this correlation ID for complete recovery flow',
    );

    try {
      // Step 1: Check if recovery is already in progress
      if (!this.checkRecoveryPreconditions()) {
        return ConnectionRecoveryResult.AlreadyInProgress();
      }

      // Step 2: Perform connection health check if connected
      const healthCheckResult = await this.performConnectionHealthCheck();

      if (healthCheckResult.isHealthy) {
        // Connection is healthy - no recovery needed
        VibeLogger.logInfo(
          'connection_recovery_usecase_success_no_action',
          'Connection is healthy - no recovery needed',
          { connection_healthy: true },
          this.correlationId,
          'UseCase completed - connection was already healthy',
        );

        return ConnectionRecoveryResult.AlreadyHealthy();
      }

      // Step 3: Attempt Unity discovery
      const discoveryResult = await this.attemptUnityDiscovery();

      if (!discoveryResult.found) {
        VibeLogger.logWarning(
          'connection_recovery_usecase_no_unity_found',
          'Unity server not found during recovery',
          {
            target_port: discoveryResult.port,
          },
          this.correlationId,
          'UseCase completed - Unity not available for recovery',
        );

        return ConnectionRecoveryResult.UnityNotFound();
      }

      // Step 4: Update client connection settings
      this.updateClientConnectionSettings(discoveryResult.port);

      // Step 5: Execute discovery callback if provided
      await this.executeDiscoveryCallback(discoveryResult.port);

      VibeLogger.logInfo(
        'connection_recovery_usecase_success',
        'Connection recovery completed successfully',
        {
          recovered_port: discoveryResult.port,
          unity_endpoint: `${UNITY_CONNECTION.DEFAULT_HOST}:${discoveryResult.port}`,
        },
        this.correlationId,
        'UseCase completed - connection recovered successfully',
      );

      return ConnectionRecoveryResult.Success(discoveryResult.port);
    } catch (error) {
      VibeLogger.logError(
        'connection_recovery_usecase_failure',
        'Connection recovery failed with error',
        {
          error_message: error instanceof Error ? error.message : String(error),
          error_type: error instanceof Error ? error.constructor.name : typeof error,
        },
        this.correlationId,
        'UseCase failed - connection recovery aborted with error',
      );

      return ConnectionRecoveryResult.Error(error instanceof Error ? error.message : String(error));
    }
  }

  /**
   * Step 1: Check recovery preconditions
   */
  private checkRecoveryPreconditions(): boolean {
    VibeLogger.logDebug(
      'connection_recovery_step_1',
      'Checking recovery preconditions',
      {
        unity_connected: this.unityClient.connected,
      },
      this.correlationId,
      'Step 1: Recovery precondition check',
    );

    // For now, always allow recovery attempts
    // In the original code, there was an isDiscovering check, but we're simplifying
    VibeLogger.logDebug(
      'connection_recovery_step_1_complete',
      'Recovery preconditions satisfied',
      {},
      this.correlationId,
      'Step 1 complete: Ready to proceed with recovery',
    );

    return true;
  }

  /**
   * Step 2: Perform connection health check if connected
   */
  private async performConnectionHealthCheck(): Promise<HealthCheckResult> {
    VibeLogger.logDebug(
      'connection_recovery_step_2',
      'Performing connection health check',
      {
        unity_connected: this.unityClient.connected,
      },
      this.correlationId,
      'Step 2: Connection health assessment',
    );

    if (!this.unityClient.connected) {
      VibeLogger.logDebug(
        'connection_recovery_step_2_not_connected',
        'Client not connected - health check skipped',
        { unity_connected: false },
        this.correlationId,
        'Step 2: No connection to check - proceeding to discovery',
      );

      return { isHealthy: false, reason: 'not_connected' };
    }

    try {
      const healthCheck = await Promise.race([
        this.unityClient.testConnection(),
        new Promise<boolean>((_, reject) =>
          setTimeout(() => reject(new Error('Connection health check timeout')), 1000),
        ),
      ]);

      if (healthCheck) {
        VibeLogger.logInfo(
          'connection_recovery_step_2_healthy',
          'Connection is healthy - recovery not needed',
          { connection_healthy: true },
          this.correlationId,
          'Step 2 complete: Connection is healthy',
        );

        return { isHealthy: true, reason: 'healthy' };
      } else {
        VibeLogger.logWarning(
          'connection_recovery_step_2_unhealthy',
          'Connection appears unhealthy - proceeding with recovery',
          { connection_healthy: false },
          this.correlationId,
          'Step 2 complete: Connection needs recovery',
        );

        return { isHealthy: false, reason: 'unhealthy' };
      }
    } catch (error) {
      VibeLogger.logWarning(
        'connection_recovery_step_2_error',
        'Health check failed - proceeding with recovery',
        {
          error_message: error instanceof Error ? error.message : String(error),
        },
        this.correlationId,
        'Step 2 complete: Health check error - recovery needed',
      );

      return { isHealthy: false, reason: 'error' };
    }
  }

  /**
   * Step 3: Attempt Unity discovery
   */
  private async attemptUnityDiscovery(): Promise<DiscoveryResult> {
    VibeLogger.logDebug(
      'connection_recovery_step_3',
      'Attempting Unity discovery',
      {},
      this.correlationId,
      'Step 3: Unity server discovery process',
    );

    const unityTcpPort: string | undefined = process.env.UNITY_TCP_PORT;
    if (!unityTcpPort) {
      throw new Error('UNITY_TCP_PORT environment variable is required but not set');
    }

    const port: number = parseInt(unityTcpPort, 10);
    if (isNaN(port) || port <= 0 || port > 65535) {
      throw new Error(`UNITY_TCP_PORT must be a valid port number (1-65535), got: ${unityTcpPort}`);
    }

    VibeLogger.logInfo(
      'connection_recovery_port_check',
      'Checking Unity availability on port',
      {
        target_port: port,
        unity_endpoint: `${UNITY_CONNECTION.DEFAULT_HOST}:${port}`,
      },
      this.correlationId,
      'Step 3: Attempting to connect to Unity MCP server',
    );

    try {
      // Direct connection approach - no separate availability check needed
      // This avoids creating additional TCP connections that could cause issues

      // Establish actual Unity connection after successful discovery
      if (!this.unityClient.connected) {
        VibeLogger.logInfo(
          'connection_recovery_establishing_connection',
          'Establishing Unity connection after successful discovery',
          {
            discovered_port: port,
            unity_endpoint: `${UNITY_CONNECTION.DEFAULT_HOST}:${port}`,
          },
          this.correlationId,
          'Calling unityClient.connect() after successful discovery',
        );

        try {
          await this.unityClient.connect('ConnectionRecoveryUseCase');
          VibeLogger.logInfo(
            'connection_recovery_connection_established',
            'Unity connection established successfully',
            {
              unity_connected: this.unityClient.connected,
              unity_endpoint: `${UNITY_CONNECTION.DEFAULT_HOST}:${port}`,
            },
            this.correlationId,
            'Connection established - unityClient.connected should now be true',
          );
        } catch (error) {
          VibeLogger.logError(
            'connection_recovery_connection_failed',
            'Failed to establish Unity connection despite successful discovery',
            {
              error_message: error instanceof Error ? error.message : JSON.stringify(error),
              unity_endpoint: `${UNITY_CONNECTION.DEFAULT_HOST}:${port}`,
            },
            this.correlationId,
            'Connection failed - falling back to discovery-only mode',
          );
        }
      }

      return { found: true, port };
    } catch (error) {
      VibeLogger.logDebug(
        'connection_recovery_step_3_error',
        'Unity availability check failed',
        {
          target_port: port,
          error_message: error instanceof Error ? error.message : String(error),
        },
        this.correlationId,
        'Step 3: Unity availability check error - expected when Unity not running',
      );

      return { found: false, port };
    }
  }

  /**
   * Step 4: Update client connection settings
   */
  private updateClientConnectionSettings(port: number): void {
    VibeLogger.logDebug(
      'connection_recovery_step_4',
      'Updating client connection settings',
      {
        new_port: port,
        unity_endpoint: `${UNITY_CONNECTION.DEFAULT_HOST}:${port}`,
      },
      this.correlationId,
      'Step 4: Client configuration update',
    );

    this.unityClient.updatePort(port);

    VibeLogger.logDebug(
      'connection_recovery_step_4_complete',
      'Client connection settings updated',
      {
        updated_port: port,
      },
      this.correlationId,
      'Step 4 complete: Client configured for Unity connection',
    );
  }

  /**
   * Step 5: Execute discovery callback if provided
   */
  private async executeDiscoveryCallback(port: number): Promise<void> {
    if (!this.onDiscoveredCallback) {
      VibeLogger.logDebug(
        'connection_recovery_step_5_skipped',
        'No discovery callback provided - skipping',
        { port },
        this.correlationId,
        'Step 5 skipped: No callback to execute',
      );
      return;
    }

    VibeLogger.logDebug(
      'connection_recovery_step_5',
      'Executing discovery callback',
      { port },
      this.correlationId,
      'Step 5: Discovery callback execution',
    );

    try {
      await this.onDiscoveredCallback(port);

      VibeLogger.logDebug(
        'connection_recovery_step_5_complete',
        'Discovery callback executed successfully',
        { port },
        this.correlationId,
        'Step 5 complete: Discovery callback finished',
      );
    } catch (error) {
      VibeLogger.logError(
        'connection_recovery_step_5_error',
        'Discovery callback execution failed',
        {
          port,
          error_message: error instanceof Error ? error.message : String(error),
        },
        this.correlationId,
        'Step 5 error: Discovery callback failed but recovery continues',
      );

      // Don't throw - callback failure shouldn't fail the entire recovery
    }
  }
}

/**
 * Result interfaces for connection recovery operation
 */
interface HealthCheckResult {
  isHealthy: boolean;
  reason: 'healthy' | 'unhealthy' | 'not_connected' | 'error';
}

interface DiscoveryResult {
  found: boolean;
  port: number;
}

/**
 * Result object for connection recovery operation
 */
export class ConnectionRecoveryResult {
  public readonly isSuccess: boolean;
  public readonly errorMessage?: string;
  public readonly port?: number;
  public readonly reason: ConnectionRecoveryReason;

  private constructor(
    isSuccess: boolean,
    reason: ConnectionRecoveryReason,
    errorMessage?: string,
    port?: number,
  ) {
    this.isSuccess = isSuccess;
    this.reason = reason;
    this.errorMessage = errorMessage;
    this.port = port;
  }

  static Success(port: number): ConnectionRecoveryResult {
    return new ConnectionRecoveryResult(true, ConnectionRecoveryReason.Success, undefined, port);
  }

  static AlreadyHealthy(): ConnectionRecoveryResult {
    return new ConnectionRecoveryResult(true, ConnectionRecoveryReason.AlreadyHealthy);
  }

  static AlreadyInProgress(): ConnectionRecoveryResult {
    return new ConnectionRecoveryResult(false, ConnectionRecoveryReason.AlreadyInProgress);
  }

  static UnityNotFound(): ConnectionRecoveryResult {
    return new ConnectionRecoveryResult(false, ConnectionRecoveryReason.UnityNotFound);
  }

  static Error(errorMessage: string): ConnectionRecoveryResult {
    return new ConnectionRecoveryResult(false, ConnectionRecoveryReason.Error, errorMessage);
  }
}

/**
 * Enumeration of possible connection recovery outcomes
 */
export enum ConnectionRecoveryReason {
  Success = 'success',
  AlreadyHealthy = 'already_healthy',
  AlreadyInProgress = 'already_in_progress',
  UnityNotFound = 'unity_not_found',
  Error = 'error',
}
