// Debug logging removed
import { DEFAULT_CLIENT_NAME } from './constants.js';
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
  private clientName: string = DEFAULT_CLIENT_NAME;

  constructor() {
    // UnityClient dependency removed - setClientName now handled by ClientInitializationUseCase
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
}
