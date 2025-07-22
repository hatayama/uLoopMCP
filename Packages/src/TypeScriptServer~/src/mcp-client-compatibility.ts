import { UnityClient } from './unity-client.js';
// Debug logging removed
import { LIST_CHANGED_UNSUPPORTED_CLIENTS, DEFAULT_CLIENT_NAME } from './constants.js';
import { VibeLogger } from './utils/vibe-logger.js';

/**
 * Client configuration interface
 */
export interface ClientConfig {
  name: string;
  supportsListChanged: boolean;
  initializationDelay: number;
  toolListStrategy: 'immediate' | 'on-request' | 'notification';
}

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
 * - Client-specific configuration management
 * - MCP_CLIENT_NAME environment variable compatibility
 * - Client name restoration on reconnection
 * - Dynamic client configuration for new clients
 */
export class McpClientCompatibility {
  private static readonly CLIENT_CONFIGS: ClientConfig[] = [
    {
      name: 'Claude Code',
      supportsListChanged: false,
      initializationDelay: 0,
      toolListStrategy: 'on-request',
    },
    {
      name: 'Cursor',
      supportsListChanged: true,
      initializationDelay: 100,
      toolListStrategy: 'notification',
    },
    {
      name: 'VSCode',
      supportsListChanged: true,
      initializationDelay: 50,
      toolListStrategy: 'notification',
    },
  ];

  private unityClient: UnityClient;
  private clientName: string = DEFAULT_CLIENT_NAME;
  private currentClientConfig: ClientConfig | null = null;

  constructor(unityClient: UnityClient) {
    this.unityClient = unityClient;
  }

  /**
   * Set client name and load client configuration
   */
  setClientName(clientName: string): void {
    this.clientName = clientName;
    this.currentClientConfig = this.findClientConfig(clientName);

    VibeLogger.logInfo(
      'mcp_client_config_loaded',
      `Client configuration loaded for ${clientName}`,
      {
        client_name: clientName,
        config: this.currentClientConfig,
        supports_list_changed: this.currentClientConfig?.supportsListChanged ?? false,
        tool_list_strategy: this.currentClientConfig?.toolListStrategy ?? 'on-request',
      },
      undefined,
      'Client-specific configuration loaded for MCP client',
      'Monitor this to ensure client configurations are working correctly',
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
   * Get client configuration
   */
  getClientConfig(): ClientConfig | null {
    return this.currentClientConfig;
  }

  /**
   * Get tool list strategy for current client
   */
  getToolListStrategy(): string {
    return this.currentClientConfig?.toolListStrategy || 'on-request';
  }

  /**
   * Get initialization delay for current client
   */
  getInitializationDelay(): number {
    return this.currentClientConfig?.initializationDelay || 0;
  }

  /**
   * Find client configuration by name with fallback for unknown clients
   */
  private findClientConfig(clientName: string): ClientConfig | null {
    const normalizedClientName = clientName.toLowerCase();
    const foundConfig = McpClientCompatibility.CLIENT_CONFIGS.find((config) =>
      normalizedClientName.includes(config.name.toLowerCase()),
    );

    if (!foundConfig && clientName !== 'Unknown') {
      // Unknown client detected - apply conservative fallback
      VibeLogger.logInfo(
        'mcp_unknown_client_detected',
        `Unknown MCP client detected: ${clientName}`,
        {
          client_name: clientName,
          normalized_name: normalizedClientName,
          available_configs: McpClientCompatibility.CLIENT_CONFIGS.map((c) => c.name),
        },
        undefined,
        'Unknown client will use conservative fallback configuration',
        'Consider adding specific configuration for this client if it becomes commonly used',
      );

      // Return conservative fallback configuration for unknown clients
      return {
        name: clientName,
        supportsListChanged: false, // Conservative: assume no list_changed support
        initializationDelay: 0,
        toolListStrategy: 'on-request',
      };
    }

    return foundConfig;
  }

  /**
   * Check if client supports list_changed based on configuration
   */
  isListChangedSupportedByConfig(clientName: string): boolean {
    const config = this.findClientConfig(clientName);
    return config?.supportsListChanged ?? false;
  }

  /**
   * Mark client as list_changed unsupported (for fallback scenarios)
   */
  markListChangedUnsupported(): void {
    if (this.currentClientConfig) {
      // Note: This doesn't modify the static configuration, just logs the fallback
      VibeLogger.logInfo(
        'mcp_client_list_changed_fallback',
        `Client ${this.clientName} fell back to list_changed unsupported mode`,
        {
          client_name: this.clientName,
          original_config: this.currentClientConfig,
        },
        undefined,
        'Client communication failed, falling back to unsupported mode',
      );
    }
  }

  /**
   * Log client compatibility information
   */
  logClientCompatibility(clientName: string): void {
    const isSupported = this.isListChangedSupported(clientName);
    const config = this.getClientConfig();

    VibeLogger.logInfo(
      'mcp_client_compatibility_info',
      `Client compatibility information for ${clientName}`,
      {
        client_name: clientName,
        list_changed_supported: isSupported,
        config: config,
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
