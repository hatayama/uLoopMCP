import * as net from 'net';
import { UNITY_CONNECTION } from './constants.js';
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
   * Start Unity discovery polling with unified connection management
   */
  start(): void {
    if (this.discoveryInterval) {
      return; // Already running
    }

    if (this.isDiscovering) {
      return; // Already discovering
    }

    // Immediate discovery attempt
    void this.unifiedDiscoveryAndConnectionCheck();

    // Set up periodic unified discovery and connection checking
    const DISCOVERY_INTERVAL_MS = 1000; // 1 second discovery interval
    this.discoveryInterval = setInterval(() => {
      void this.unifiedDiscoveryAndConnectionCheck();
    }, DISCOVERY_INTERVAL_MS);

    // Track active timer count for debugging
    UnityDiscovery.activeTimerCount++;
  }

  /**
   * Stop Unity discovery polling
   */
  stop(): void {
    if (this.discoveryInterval) {
      clearInterval(this.discoveryInterval);
      this.discoveryInterval = null;
      this.isDiscovering = false;

      // Track active timer count for debugging
      UnityDiscovery.activeTimerCount = Math.max(0, UnityDiscovery.activeTimerCount - 1);
    }
  }

  /**
   * Unified discovery and connection checking using ConnectionRecoveryUseCase pattern
   * Handles both Unity discovery and connection health monitoring
   */
  private async unifiedDiscoveryAndConnectionCheck(): Promise<void> {
    const correlationId = VibeLogger.generateCorrelationId();

    if (this.isDiscovering) {
      VibeLogger.logDebug(
        'unity_discovery_skip_in_progress',
        'Discovery already in progress - skipping',
        { is_discovering: true },
        correlationId,
      );
      return;
    }

    this.isDiscovering = true;

    VibeLogger.logInfo(
      'unity_discovery_cycle_start',
      'Starting unified discovery and connection check cycle using UseCase pattern',
      {
        unity_connected: this.unityClient.connected,
        discovery_interval_ms: 1000,
        active_timer_count: UnityDiscovery.activeTimerCount,
      },
      correlationId,
      'Using ConnectionRecoveryUseCase for temporal cohesion in connection recovery.',
    );

    try {
      // Import UseCase dynamically to avoid circular dependencies
      const { ConnectionRecoveryUseCase } = await import('./usecases/connection-recovery-use-case.js');
      
      // Create UseCase instance (single-use pattern)
      const useCase = new ConnectionRecoveryUseCase(this.unityClient, this.onDiscoveredCallback || undefined);
      
      // Execute all recovery steps with temporal cohesion
      const result = await useCase.execute();
      
      // UseCase instance is automatically discarded after this point
      
      if (result.isSuccess && result.reason === 'already_healthy') {
        // Connection was healthy - stop discovery
        this.stop();
        return;
      }
      
      if (result.isSuccess && result.reason === 'success') {
        // Recovery succeeded - continue with normal operation
        return;
      }
      
      // Recovery failed or Unity not found - continue polling
      VibeLogger.logDebug(
        'unity_discovery_recovery_incomplete',
        'Connection recovery incomplete - continuing discovery polling',
        {
          recovery_reason: result.reason,
          will_retry: true,
        },
        correlationId,
        'Recovery UseCase completed but connection not established - will retry on next cycle',
      );
    } finally {
      VibeLogger.logDebug(
        'unity_discovery_cycle_end',
        'Discovery cycle completed',
        { is_discovering: false },
        correlationId,
      );
      this.isDiscovering = false;
    }
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
      VibeLogger.logDebug(
        'unity_discovery_port_check',
        'Checking Unity availability on port',
        {
          target_port: port,
          unity_endpoint: `${UNITY_CONNECTION.DEFAULT_HOST}:${port}`,
          process_id: process.pid,
        },
        correlationId,
        'Attempting to connect to Unity MCP server on specified port',
        'If successful, Unity should accept connection and register client',
      );

      if (await this.isUnityAvailable(port)) {
        VibeLogger.logInfo(
          'unity_discovery_success',
          'Unity discovered and connection established',
          {
            discovered_port: port,
            unity_endpoint: `${UNITY_CONNECTION.DEFAULT_HOST}:${port}`,
            unity_host: UNITY_CONNECTION.DEFAULT_HOST,
            process_id: process.pid,
            discovery_method: 'port_scan',
          },
          correlationId,
          'Unity MCP server found and connection established successfully.',
          'Monitor for tools/list_changed notifications after this discovery. Check Unity logs for client registration.',
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
    // Restart discovery if not already running
    if (!this.discoveryInterval) {
      this.start();
    }

    // Trigger callback
    if (this.onConnectionLostCallback) {
      this.onConnectionLostCallback();
    }
  }

  /**
   * Check if Unity is available on specific port
   */
  private async isUnityAvailable(port: number): Promise<boolean> {
    return new Promise((resolve) => {
      const socket = new net.Socket();
      const timeout = 500; // Shorter timeout for faster discovery
      const correlationId = VibeLogger.generateCorrelationId();

      const timer = setTimeout(() => {
        VibeLogger.logDebug(
          'unity_availability_check_timeout',
          'Unity availability check timed out',
          {
            target_port: port,
            unity_endpoint: `${UNITY_CONNECTION.DEFAULT_HOST}:${port}`,
            timeout_ms: timeout,
            process_id: process.pid,
          },
          correlationId,
          'Unity availability check timeout - Unity may not be running on this port',
        );
        socket.destroy();
        resolve(false);
      }, timeout);

      socket.connect(port, UNITY_CONNECTION.DEFAULT_HOST, () => {
        VibeLogger.logDebug(
          'unity_availability_check_success',
          'Unity availability check successful',
          {
            target_port: port,
            unity_endpoint: `${UNITY_CONNECTION.DEFAULT_HOST}:${port}`,
            socket_local_address: socket.localAddress,
            socket_local_port: socket.localPort,
            process_id: process.pid,
          },
          correlationId,
          'Unity MCP server is available and accepting connections',
          'Unity should show incoming connection in its logs',
        );
        clearTimeout(timer);
        socket.destroy();
        resolve(true);
      });

      socket.on('error', (error) => {
        VibeLogger.logDebug(
          'unity_availability_check_error',
          'Unity availability check failed',
          {
            target_port: port,
            unity_endpoint: `${UNITY_CONNECTION.DEFAULT_HOST}:${port}`,
            error_message: error.message,
            error_code: (error as NodeJS.ErrnoException).code,
            process_id: process.pid,
          },
          correlationId,
          'Unity availability check failed - expected when Unity is not running',
        );
        clearTimeout(timer);
        resolve(false);
      });
    });
  }

  /**
   * Log current timer status for debugging (development mode only)
   */
  private logTimerStatus(): void {
    if (!this.isDevelopment) {
      return;
    }

    // Warning if multiple timers are detected
    if (UnityDiscovery.activeTimerCount > 1) {
      // WARNING: Multiple timers detected
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
      intervalMs: 1000, // Discovery interval in ms
      hasSingleton: UnityDiscovery.instance !== null,
    };
  }
}
