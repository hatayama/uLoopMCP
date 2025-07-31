import { POLLING } from './constants.js';
import { VibeLogger } from './utils/vibe-logger.js';
import { UnityClient } from './unity-client.js';

/**
 * Unity Discovery Service with Unified Connection Management
 * Handles both Unity discovery and connection polling in a single timer
 * to prevent multiple concurrent timers and improve stability.
 *
 * Design document reference: Packages/src/TypeScriptServer~/ARCHITECTURE.md
 *
 * Related classes:
 * - UnityClient: TCP client that this service manages discovery for
 * - UnityConnectionManager: Higher-level connection manager that uses this service
 * - UnityMcpServer: Main server that initiates discovery
 */
export class UnityDiscovery {
  private discoveryInterval: NodeJS.Timeout | null = null;
  private unityClient: UnityClient;
  private onDiscoveredCallback: ((port: number) => Promise<void>) | null = null;
  private onConnectionLostCallback: (() => void) | null = null;
  private isDiscovering: boolean = false;
  private isDevelopment: boolean = false;
  private discoveryAttemptCount: number = 0;

  // Singleton pattern to prevent multiple instances
  private static instance: UnityDiscovery | null = null;
  private static activeTimerCount: number = 0; // Track active timers for debugging

  private constructor(unityClient: UnityClient) {
    this.unityClient = unityClient;
    this.isDevelopment = process.env.NODE_ENV === 'development';
  }

  /**
   * Get singleton instance of UnityDiscovery
   */
  static getInstance(unityClient: UnityClient): UnityDiscovery {
    if (!UnityDiscovery.instance) {
      UnityDiscovery.instance = new UnityDiscovery(unityClient);
    }
    return UnityDiscovery.instance;
  }

  /**
   * Set callback for when Unity is discovered
   */
  setOnDiscoveredCallback(callback: (port: number) => Promise<void>): void {
    this.onDiscoveredCallback = callback;
  }

  /**
   * Set callback for when connection is lost
   */
  setOnConnectionLostCallback(callback: () => void): void {
    this.onConnectionLostCallback = callback;
  }

  /**
   * Check if discovery is currently running
   */
  getIsDiscovering(): boolean {
    return this.isDiscovering;
  }

  /**
   * Get current polling interval based on attempt count
   */
  private getCurrentPollingInterval(): number {
    const currentInterval =
      this.discoveryAttemptCount <= POLLING.INITIAL_ATTEMPTS
        ? POLLING.INITIAL_INTERVAL_MS
        : POLLING.EXTENDED_INTERVAL_MS;

    // Log when switching from initial to extended polling
    if (this.discoveryAttemptCount === POLLING.INITIAL_ATTEMPTS + 1) {
      VibeLogger.logInfo(
        'unity_discovery_polling_switch',
        'Switching from initial to extended polling interval',
        {
          previous_interval_ms: POLLING.INITIAL_INTERVAL_MS,
          new_interval_ms: POLLING.EXTENDED_INTERVAL_MS,
          discovery_attempt_count: this.discoveryAttemptCount,
          switch_threshold: POLLING.INITIAL_ATTEMPTS,
        },
        undefined,
        'Polling interval changed to reduce CPU usage after initial attempts',
        'Monitor if this reduces system load while maintaining connection reliability',
      );
    }

    return currentInterval;
  }

  /**
   * Schedule next discovery attempt with adaptive interval
   */
  private scheduleNextDiscovery(): void {
    const currentInterval = this.getCurrentPollingInterval();
    const isInitialPolling = this.discoveryAttemptCount <= POLLING.INITIAL_ATTEMPTS;

    VibeLogger.logDebug(
      'unity_discovery_scheduling_next',
      'Scheduling next discovery attempt',
      {
        next_interval_ms: currentInterval,
        discovery_attempt_count: this.discoveryAttemptCount,
        is_initial_polling: isInitialPolling,
        polling_type: isInitialPolling ? 'initial_fast' : 'extended_slow',
      },
      undefined,
      `Next discovery scheduled in ${currentInterval}ms`,
    );

    this.discoveryInterval = setTimeout(() => {
      void this.unifiedDiscoveryAndConnectionCheck();
      // Schedule next attempt recursively
      if (this.discoveryInterval) {
        this.scheduleNextDiscovery();
      }
    }, currentInterval);
  }

  /**
   * Start Unity discovery polling with unified connection management
   */
  start(): void {
    if (this.discoveryInterval) {
      return; // Already running
    }

    if (this.isDiscovering) {
      return; // Already discovering
    }

    // Reset attempt count when starting
    this.discoveryAttemptCount = 0;

    // Immediate discovery attempt
    void this.unifiedDiscoveryAndConnectionCheck();

    // Set up adaptive polling
    this.scheduleNextDiscovery();

    // Track active timer count for debugging
    UnityDiscovery.activeTimerCount++;
  }

  /**
   * Stop Unity discovery polling
   */
  stop(): void {
    if (this.discoveryInterval) {
      clearTimeout(this.discoveryInterval);
      this.discoveryInterval = null;
    }

    // Always reset discovering flag regardless of interval state
    this.isDiscovering = false;

    // Reset attempt count when stopping
    this.discoveryAttemptCount = 0;

    // Track active timer count for debugging
    UnityDiscovery.activeTimerCount = Math.max(0, UnityDiscovery.activeTimerCount - 1);
  }

  /**
   * Force reset discovery state (for debugging and recovery)
   */
  forceResetDiscoveryState(): void {
    this.isDiscovering = false;
    VibeLogger.logWarning(
      'unity_discovery_state_force_reset',
      'Discovery state forcibly reset',
      { was_discovering: true },
      undefined,
      'Manual recovery from stuck discovery state',
    );
  }

  /**
   * Unified discovery and connection checking
   * Handles both Unity discovery and connection health monitoring
   */
  private async unifiedDiscoveryAndConnectionCheck(): Promise<void> {
    const correlationId = VibeLogger.generateCorrelationId();

    // Double-check with lock to prevent race conditions
    if (this.isDiscovering) {
      VibeLogger.logDebug(
        'unity_discovery_skip_in_progress',
        'Discovery already in progress - skipping',
        { is_discovering: true, active_timer_count: UnityDiscovery.activeTimerCount },
        correlationId,
        'Another discovery cycle is already running - this is normal behavior.',
      );
      return;
    }

    // Initialize discovery cycle
    this.logDiscoveryCycleStart(correlationId);

    try {
      // Check connection health if already connected
      const shouldContinueDiscovery = await this.handleConnectionHealthCheck(correlationId);
      if (!shouldContinueDiscovery) {
        return;
      }

      // Execute Unity discovery with timeout protection
      await this.executeUnityDiscovery(correlationId);
    } catch (error) {
      VibeLogger.logError(
        'unity_discovery_cycle_error',
        'Discovery cycle encountered error',
        {
          error_message: error instanceof Error ? error.message : String(error),
          is_discovering: this.isDiscovering,
          correlation_id: correlationId,
        },
        correlationId,
        'Discovery cycle failed - forcing state reset to prevent hang',
      );
    } finally {
      this.finalizeCycleWithCleanup(correlationId);
    }
  }

  /**
   * Log discovery cycle initialization
   */
  private logDiscoveryCycleStart(correlationId: string): void {
    // Atomic flag setting with additional logging for debugging
    this.isDiscovering = true;

    // Increment attempt count for adaptive polling
    this.discoveryAttemptCount++;

    const currentInterval = this.getCurrentPollingInterval();

    VibeLogger.logDebug(
      'unity_discovery_state_set',
      'Discovery state set to true',
      { is_discovering: true, correlation_id: correlationId },
      correlationId,
      'Discovery state flag set - starting cycle',
    );

    VibeLogger.logInfo(
      'unity_discovery_cycle_start',
      'Starting unified discovery and connection check cycle',
      {
        unity_connected: this.unityClient.connected,
        polling_interval_ms: currentInterval,
        discovery_attempt_count: this.discoveryAttemptCount,
        is_initial_polling: this.discoveryAttemptCount <= POLLING.INITIAL_ATTEMPTS,
        active_timer_count: UnityDiscovery.activeTimerCount,
      },
      correlationId,
      'This cycle checks connection health and attempts Unity discovery if needed.',
    );
  }

  /**
   * Handle connection health check and determine if discovery should continue
   */
  private async handleConnectionHealthCheck(correlationId: string): Promise<boolean> {
    // If already connected, check connection health (lightweight)
    if (this.unityClient.connected) {
      const isConnectionHealthy = await this.checkConnectionHealth();

      if (isConnectionHealthy) {
        // Connection is healthy - stop discovery
        VibeLogger.logInfo(
          'unity_discovery_connection_healthy',
          'Connection is healthy - stopping discovery',
          { connection_healthy: true },
          correlationId,
        );
        this.stop();
        return false; // Don't continue discovery
      } else {
        // Connection might be temporarily unhealthy
        // Don't immediately assume it's lost - give it time to recover
        VibeLogger.logWarning(
          'unity_discovery_connection_unhealthy',
          'Connection appears unhealthy - continuing discovery without assuming loss',
          { connection_healthy: false },
          correlationId,
          'Connection health check failed. Will continue discovery but not assume complete loss.',
          'Connection may recover on next cycle. Monitor for persistent issues.',
        );

        // Don't trigger connection lost callback immediately
        // Let discovery continue and see if connection recovers
      }
    }
    return true; // Continue discovery
  }

  /**
   * Execute Unity discovery with timeout protection
   */
  private async executeUnityDiscovery(_correlationId: string): Promise<void> {
    // Discover Unity by checking port range with timeout protection
    await Promise.race([
      this.discoverUnityOnPorts(),
      new Promise<void>((_, reject) =>
        setTimeout(() => reject(new Error('Unity discovery timeout - 5 seconds')), 5000),
      ),
    ]);
  }

  /**
   * Finalize discovery cycle with cleanup and logging
   */
  private finalizeCycleWithCleanup(correlationId: string): void {
    VibeLogger.logDebug(
      'unity_discovery_cycle_end',
      'Discovery cycle completed - resetting state',
      {
        is_discovering_before: this.isDiscovering,
        is_discovering_after: false,
        correlation_id: correlationId,
      },
      correlationId,
      'Discovery cycle finished and state reset to prevent hang',
    );
    // Force state reset regardless of outcome
    this.isDiscovering = false;
  }

  /**
   * Check if the current connection is healthy with timeout protection
   */
  private async checkConnectionHealth(): Promise<boolean> {
    try {
      // Add an additional timeout layer to prevent hanging
      const healthCheck = await Promise.race([
        this.unityClient.testConnection(),
        new Promise<boolean>((_, reject) =>
          setTimeout(() => reject(new Error('Connection health check timeout')), 1000),
        ),
      ]);
      return healthCheck;
    } catch (error) {
      // Return false on any error or timeout
      return false;
    }
  }

  /**
   * Discover Unity by checking specified port
   */
  private async discoverUnityOnPorts(): Promise<void> {
    const correlationId = VibeLogger.generateCorrelationId();

    const unityTcpPort: string | undefined = process.env.UNITY_TCP_PORT;
    if (!unityTcpPort) {
      throw new Error('UNITY_TCP_PORT environment variable is required but not set');
    }

    const port: number = parseInt(unityTcpPort, 10);
    if (isNaN(port) || port <= 0 || port > 65535) {
      throw new Error(`UNITY_TCP_PORT must be a valid port number (1-65535), got: ${unityTcpPort}`);
    }

    VibeLogger.logInfo(
      'unity_discovery_port_scan_start',
      'Starting Unity port discovery scan',
      {
        target_port: port,
      },
      correlationId,
      'Checking specified port for Unity MCP server.',
    );

    try {
      if (await UnityClient.isUnityAvailable(port)) {
        VibeLogger.logInfo(
          'unity_discovery_success',
          'Unity discovered and connection established',
          {
            discovered_port: port,
          },
          correlationId,
          'Unity MCP server found and connection established successfully.',
          'Monitor for tools/list_changed notifications after this discovery.',
        );

        // Test warning log with stack trace for debugging
        VibeLogger.logWarning(
          'unity_discovery_test_warning',
          'Test warning log to verify stack trace functionality',
          {
            discovered_port: port,
            test_purpose: 'stack_trace_verification',
          },
          correlationId,
          'This is a test warning to verify that stack traces are properly displayed with source maps.',
          'Check that TypeScript file locations are shown correctly in the stack trace output.',
        );

        // Update client port - connection management is UnityClient's responsibility
        this.unityClient.updatePort(port);

        // Notify discovery callback - let higher-level components handle connection
        if (this.onDiscoveredCallback) {
          await this.onDiscoveredCallback(port);
        }
        return;
      }
    } catch (error) {
      VibeLogger.logDebug(
        'unity_discovery_port_check_failed',
        'Unity availability check failed for specified port',
        {
          target_port: port,
          error_message: error instanceof Error ? error.message : String(error),
          error_type: error instanceof Error ? error.constructor.name : typeof error,
        },
        correlationId,
        'Expected failure when Unity is not running on this port. Will continue polling.',
        'This is normal during Unity startup or when Unity is not running.',
      );
    }

    VibeLogger.logWarning(
      'unity_discovery_no_unity_found',
      'No Unity server found on specified port',
      {
        target_port: port,
      },
      correlationId,
      'Unity MCP server not found on the specified port. Unity may not be running or using a different port.',
      'Check Unity console for MCP server status and verify port configuration.',
    );
  }

  /**
   * Force immediate Unity discovery for connection recovery
   */
  async forceDiscovery(): Promise<boolean> {
    if (this.unityClient.connected) {
      return true;
    }

    // Use the unified discovery method
    await this.unifiedDiscoveryAndConnectionCheck();

    return this.unityClient.connected;
  }

  /**
   * Handle connection lost event (called by UnityClient)
   */
  handleConnectionLost(): void {
    const correlationId = VibeLogger.generateCorrelationId();

    VibeLogger.logInfo(
      'unity_discovery_connection_lost_handler',
      'Handling connection lost event',
      {
        was_discovering: this.isDiscovering,
        has_discovery_interval: this.discoveryInterval !== null,
        active_timer_count: UnityDiscovery.activeTimerCount,
      },
      correlationId,
      'Connection lost event received - preparing for recovery',
    );

    // Force reset discovery state to ensure clean restart
    this.isDiscovering = false;
    this.discoveryAttemptCount = 0;

    // Add delay before restarting discovery to allow Unity to fully shut down
    setTimeout(() => {
      VibeLogger.logInfo(
        'unity_discovery_restart_after_connection_lost',
        'Restarting discovery after connection lost delay',
        {
          has_discovery_interval: this.discoveryInterval !== null,
        },
        correlationId,
        'Starting discovery with delay to allow Unity server restart',
      );

      // Restart discovery if not already running
      if (!this.discoveryInterval) {
        this.start();
      }
    }, 2000); // 2秒待ってからディスカバリー再開

    // Trigger callback immediately
    if (this.onConnectionLostCallback) {
      this.onConnectionLostCallback();
    }
  }

  /**
   * Get debugging information about current timer state
   */
  getDebugInfo(): object {
    return {
      isTimerActive: this.discoveryInterval !== null,
      isDiscovering: this.isDiscovering,
      activeTimerCount: UnityDiscovery.activeTimerCount,
      isConnected: this.unityClient.connected,
      intervalMs: this.getCurrentPollingInterval(),
      discoveryAttemptCount: this.discoveryAttemptCount,
      isInitialPolling: this.discoveryAttemptCount <= POLLING.INITIAL_ATTEMPTS,
      hasSingleton: UnityDiscovery.instance !== null,
    };
  }
}
