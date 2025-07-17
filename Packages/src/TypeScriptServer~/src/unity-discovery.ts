import * as net from 'net';
import { UNITY_CONNECTION, POLLING } from './constants.js';
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
    this.discoveryInterval = setInterval(() => {
      void this.unifiedDiscoveryAndConnectionCheck();
    }, POLLING.INTERVAL_MS);

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
   * Unified discovery and connection checking
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
      'Starting unified discovery and connection check cycle',
      {
        unity_connected: this.unityClient.connected,
        polling_interval_ms: POLLING.INTERVAL_MS,
        active_timer_count: UnityDiscovery.activeTimerCount,
      },
      correlationId,
      'This cycle checks connection health and attempts Unity discovery if needed.',
    );

    try {
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
          return;
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

      // Discover Unity by checking port range
      await this.discoverUnityOnPorts();
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
   * Discover Unity by checking default port range
   */
  private async discoverUnityOnPorts(): Promise<void> {
    const correlationId = VibeLogger.generateCorrelationId();
    
    const unityTcpPort: string | undefined = process.env.UNITY_TCP_PORT;
    if (!unityTcpPort) {
      throw new Error('UNITY_TCP_PORT environment variable is required but not set');
    }
    
    const basePort: number = parseInt(unityTcpPort, 10);
    if (isNaN(basePort) || basePort <= 0 || basePort > 65535) {
      throw new Error(`UNITY_TCP_PORT must be a valid port number (1-65535), got: ${unityTcpPort}`);
    }
    // Expanded port range for better Unity discovery
    const portRange = [
      basePort,
      basePort + 100,
      basePort + 200,
      basePort + 300,
      basePort + 400,
      basePort - 100, // Also check lower ports
    ];

    VibeLogger.logInfo(
      'unity_discovery_port_scan_start',
      'Starting Unity port discovery scan',
      {
        base_port: basePort,
        port_range: portRange,
        total_ports: portRange.length,
      },
      correlationId,
      'Scanning multiple ports to find Unity MCP server.',
    );

    for (const port of portRange) {
      try {
        if (await this.isUnityAvailable(port)) {
          VibeLogger.logInfo(
            'unity_discovery_success',
            'Unity discovered and connection established',
            {
              discovered_port: port,
              base_port: basePort,
              port_offset: port - basePort,
            },
            correlationId,
            'Unity MCP server found and connection established successfully.',
            'Monitor for tools/list_changed notifications after this discovery.',
          );

          // Update client port and establish connection
          this.unityClient.updatePort(port);

          // Since isUnityAvailable() confirmed TCP connectivity,
          // we can safely establish the connection without redundant checks
          try {
            await this.unityClient.connect();

            if (this.onDiscoveredCallback) {
              await this.onDiscoveredCallback(port);
            }
          } catch (error) {
            VibeLogger.logError(
              'unity_discovery_connection_failed',
              'Failed to establish connection after discovery',
              { port, error: error instanceof Error ? error.message : String(error) },
              correlationId,
              'Connection attempt failed despite successful port scan.',
              'Check Unity server status and network connectivity.',
            );
            continue; // Try next port
          }
          return;
        }
      } catch (error) {
        // Silent polling - expected failures when Unity is not running on this port
        // Continue checking other ports
      }
    }

    VibeLogger.logWarning(
      'unity_discovery_no_unity_found',
      'No Unity server found on any port in range',
      {
        base_port: basePort,
        ports_checked: portRange,
        total_attempts: portRange.length,
      },
      correlationId,
      'Unity MCP server not found on any of the checked ports. Unity may not be running or using a different port.',
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

      const timer = setTimeout(() => {
        socket.destroy();
        resolve(false);
      }, timeout);

      socket.connect(port, UNITY_CONNECTION.DEFAULT_HOST, () => {
        clearTimeout(timer);
        socket.destroy();
        resolve(true);
      });

      socket.on('error', () => {
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
      intervalMs: POLLING.INTERVAL_MS,
      hasSingleton: UnityDiscovery.instance !== null,
    };
  }
}
