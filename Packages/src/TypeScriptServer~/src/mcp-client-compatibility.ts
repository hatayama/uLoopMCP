import { UnityClient } from './unity-client.js';
// Debug logging removed
import { LIST_CHANGED_UNSUPPORTED_CLIENTS, DEFAULT_CLIENT_NAME } from './constants.js';
import { VibeLogger } from './utils/vibe-logger.js';

/**
 * MCP Client Compatibility Manager - Manages client-specific compatibility and list_changed handling
 *
 * Design document reference: .kiro/specs/mcp-tool-recognition-fix/design.md
 *
 * Related classes:
 * - UnityClient: Manages communication with Unity Editor
 * - UnityMcpServer: Main server class that uses this manager
 * - ClientInitializationHandler: Uses this manager for client-specific initialization
 *
 * Key features:
 * - Client name management and initialization
 * - list_changed support/unsupported detection
 * - Client-specific compatibility handling
 *
 * Implementation strategy:
 * - Uses constants.ts LIST_CHANGED_UNSUPPORTED_CLIENTS array for compatibility detection
 * - Handles Unity client name registration and reconnection scenarios
 * - Provides logging for debugging compatibility issues
 */
export class McpClientCompatibility {
  private unityClient: UnityClient;
  private clientName: string = DEFAULT_CLIENT_NAME;

  constructor(unityClient: UnityClient) {
    this.unityClient = unityClient;
  }

  /**
   * Set client name
   */
  setClientName(clientName: string): void {
    this.clientName = clientName;

    VibeLogger.logInfo(
      'mcp_client_name_set',
      `MCP client name set: ${clientName}`,
      { client_name: clientName },
      undefined,
      'Client name set for MCP session',
    );
  }

  /**
   * Get client name
   */
  getClientName(): string {
    return this.clientName;
  }

  /**
   * Check if client doesn't support list_changed notifications
   */
  isListChangedUnsupported(clientName: string): boolean {
    if (!clientName) {
      return false;
    }

    const normalizedName = clientName.toLowerCase();
    return LIST_CHANGED_UNSUPPORTED_CLIENTS.some((unsupported) =>
      normalizedName.includes(unsupported),
    );
  }

  /**
   * Handle client name initialization and setup
   */
  async handleClientNameInitialization(): Promise<void> {
    // Client name handling:
    // 1. Primary: clientInfo.name from MCP protocol initialize request
    // 2. Fallback: MCP_CLIENT_NAME environment variable (for backward compatibility)
    // 3. Default: Empty string (Unity will show "No Client" in UI)
    // Note: MCP_CLIENT_NAME is deprecated but kept for compatibility with older setups
    if (!this.clientName) {
      const fallbackName = process.env.MCP_CLIENT_NAME;
      if (fallbackName) {
        this.clientName = fallbackName;
        await this.unityClient.setClientName(fallbackName);
        // Fallback client name set to Unity
      } else {
        // No client name set, waiting for initialize request
      }
    } else {
      // Send the already set client name to Unity
      await this.unityClient.setClientName(this.clientName);
      // Client name already set, sending to Unity
    }

    // Register reconnect handler to re-send client name after reconnection
    this.unityClient.onReconnect(() => {
      // Reconnected - resending client name
      void this.unityClient.setClientName(this.clientName);
    });
  }

  /**
   * Initialize client with name
   */
  async initializeClient(clientName: string): Promise<void> {
    this.setClientName(clientName);
    await this.handleClientNameInitialization();
  }

  /**
   * Check if client supports list_changed notifications
   */
  isListChangedSupported(clientName: string): boolean {
    return !this.isListChangedUnsupported(clientName);
  }

  /**
   * Log compatibility information for debugging
   */
  logClientCompatibilityInfo(clientName: string): void {
    const isSupported = this.isListChangedSupported(clientName);

    VibeLogger.logInfo(
      'mcp_client_compatibility_info',
      `Client compatibility information for ${clientName}`,
      {
        client_name: clientName,
        list_changed_supported: isSupported,
        initialization_strategy: isSupported ? 'asynchronous' : 'synchronous',
      },
      undefined,
      `${clientName} will use ${isSupported ? 'asynchronous' : 'synchronous'} initialization`,
    );

    if (!isSupported) {
      // Client will use synchronous initialization
    } else {
      // Client will use asynchronous initialization
    }
  }
}
