import { UnityClient } from './unity-client.js';
import { DynamicUnityCommandTool } from './tools/dynamic-unity-command-tool.js';
import { Tool } from '@modelcontextprotocol/sdk/types.js';
import { ENVIRONMENT } from './constants.js';
import { RefreshToolsUseCase } from './domain/use-cases/refresh-tools-use-case.js';
import { VibeLogger } from './utils/vibe-logger.js';
import { IToolService } from './application/interfaces/tool-service.js';
import { UnityConnectionManager } from './unity-connection-manager.js';

// Import UnityParameterSchema type from the tool file
type UnityParameterSchema = { [key: string]: unknown };

/**
 * Unity Tool Manager - Manages dynamic tool generation and management
 *
 * Design document reference: Packages/src/TypeScriptServer~/ARCHITECTURE.md
 *
 * Related classes:
 * - UnityClient: Manages communication with Unity Editor
 * - DynamicUnityCommandTool: Implementation of dynamic tools
 * - UnityMcpServer: Main server class that uses this manager
 *
 * Key features:
 * - Dynamic tool generation from Unity tools
 * - Tool refresh management
 * - Tool details fetching and parsing
 * - Development mode support
 */
export class UnityToolManager implements IToolService {
  private unityClient: UnityClient;
  private readonly isDevelopment: boolean;
  private readonly dynamicTools: Map<string, DynamicUnityCommandTool> = new Map();
  private isRefreshing: boolean = false;
  private clientName: string = '';
  private connectionManager?: UnityConnectionManager; // Will be injected for UseCase

  constructor(unityClient: UnityClient) {
    this.unityClient = unityClient;
    this.isDevelopment = process.env.NODE_ENV === ENVIRONMENT.NODE_ENV_DEVELOPMENT;
  }

  /**
   * Set connection manager for UseCase integration (Phase 3.2)
   */
  setConnectionManager(connectionManager: UnityConnectionManager): void {
    this.connectionManager = connectionManager;
  }

  /**
   * Set client name for Unity communication
   */
  setClientName(clientName: string): void {
    this.clientName = clientName;
  }

  /**
   * Get dynamic tools map
   */
  getDynamicTools(): Map<string, DynamicUnityCommandTool> {
    return this.dynamicTools;
  }

  /**
   * Get tools from Unity
   */
  async getToolsFromUnity(): Promise<Tool[]> {
    if (!this.unityClient.connected) {
      return [];
    }

    try {
      const toolDetails = await this.unityClient.fetchToolDetailsFromUnity(this.isDevelopment);

      if (!toolDetails) {
        return [];
      }

      this.createDynamicToolsFromTools(toolDetails);

      // Convert dynamic tools to Tool array
      const tools: Tool[] = [];
      for (const [toolName, dynamicTool] of this.dynamicTools) {
        tools.push({
          name: toolName,
          description: dynamicTool.description,
          inputSchema: this.convertToMcpSchema(dynamicTool.inputSchema),
        });
      }

      return tools;
    } catch (error) {
      // Failed to get tools from Unity
      return [];
    }
  }

  /**
   * Initialize dynamic Unity tools
   */
  async initializeDynamicTools(): Promise<void> {
    try {
      await this.unityClient.ensureConnected();

      const toolDetails = await this.unityClient.fetchToolDetailsFromUnity(this.isDevelopment);
      if (!toolDetails) {
        return;
      }

      this.createDynamicToolsFromTools(toolDetails);

      // Tool details processed successfully
    } catch (error) {
      // Failed to initialize dynamic tools
      // Continue without dynamic tools
    }
  }

  /**
   * Create dynamic tools from Unity tool details
   */
  private createDynamicToolsFromTools(toolDetails: unknown[]): void {
    // Create dynamic tools for each Unity tool
    this.dynamicTools.clear();
    const toolContext = { unityClient: this.unityClient };

    for (const toolInfo of toolDetails) {
      const toolName = (toolInfo as { name: string }).name;
      const description =
        (toolInfo as { description?: string }).description || `Execute Unity tool: ${toolName}`;
      const parameterSchema = (toolInfo as { parameterSchema?: unknown }).parameterSchema;
      const displayDevelopmentOnly =
        (toolInfo as { displayDevelopmentOnly?: boolean }).displayDevelopmentOnly || false;

      // Skip development-only tools in production mode
      if (displayDevelopmentOnly && !this.isDevelopment) {
        continue;
      }

      const finalToolName = toolName;

      const dynamicTool = new DynamicUnityCommandTool(
        toolContext,
        toolName,
        description,
        parameterSchema as UnityParameterSchema | undefined, // Type assertion for schema compatibility
      );

      this.dynamicTools.set(finalToolName, dynamicTool);
    }
  }

  /**
   * Refresh dynamic tools by re-fetching from Unity
   * This method can be called to update the tool list when Unity tools change
   *
   * IMPORTANT: This method is critical for domain reload recovery
   */
  async refreshDynamicTools(sendNotification?: () => void): Promise<void> {
    try {
      // Phase 3.2: Use RefreshToolsUseCase if connectionManager is available
      // BUT avoid during connection establishment to prevent circular dependency
      if (this.connectionManager && this.connectionManager.isConnected()) {
        await this.refreshToolsWithUseCase(sendNotification);
        return;
      }

      // Fallback: Use direct initialization if UseCase is not available
      await this.refreshToolsFallback(sendNotification);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : String(error);
      VibeLogger.logError(
        'unity_tool_manager_refresh_failed',
        'Failed to refresh dynamic tools',
        { error: errorMessage },
        undefined,
        'Tool refresh failed - domain reload recovery may be impacted',
      );

      // Final fallback: try direct initialization to maintain domain reload recovery
      await this.executeUltimateToolsFallback(sendNotification);
    }
  }

  /**
   * Refresh tools using RefreshToolsUseCase (preferred method)
   */
  private async refreshToolsWithUseCase(sendNotification?: () => void): Promise<void> {
    if (!this.connectionManager) {
      throw new Error('ConnectionManager is required for UseCase-based refresh');
    }
    const refreshToolsUseCase = new RefreshToolsUseCase(this.connectionManager, this);

    const result = await refreshToolsUseCase.execute({
      includeDevelopmentOnly: this.isDevelopment,
    });

    VibeLogger.logInfo(
      'unity_tool_manager_refresh_completed',
      'Dynamic tools refreshed successfully via UseCase',
      { tool_count: result.tools.length, refreshed_at: result.refreshedAt },
      undefined,
      'Tool refresh completed - ready for domain reload recovery',
    );

    // Send tools changed notification to MCP client if callback provided
    // This is critical for notifying the client after domain reload
    if (sendNotification) {
      sendNotification();
    }
  }

  /**
   * Refresh tools using direct initialization (fallback method)
   */
  private async refreshToolsFallback(sendNotification?: () => void): Promise<void> {
    VibeLogger.logDebug(
      'unity_tool_manager_fallback_refresh',
      'Using fallback refresh method (connectionManager not available)',
      {},
      undefined,
      'Direct initialization fallback for domain reload recovery',
    );

    await this.initializeDynamicTools();

    if (sendNotification) {
      sendNotification();
    }
  }

  /**
   * Execute ultimate fallback for tool refresh (last resort)
   */
  private async executeUltimateToolsFallback(sendNotification?: () => void): Promise<void> {
    try {
      await this.initializeDynamicTools();

      if (sendNotification) {
        sendNotification();
      }
    } catch (fallbackError) {
      VibeLogger.logError(
        'unity_tool_manager_fallback_failed',
        'Fallback tool initialization also failed',
        { error: fallbackError instanceof Error ? fallbackError.message : String(fallbackError) },
        undefined,
        'Both UseCase and fallback failed - domain reload recovery compromised',
      );
    }
  }

  /**
   * Safe version of refreshDynamicTools that prevents duplicate execution
   */
  async refreshDynamicToolsSafe(sendNotification?: () => void): Promise<void> {
    if (this.isRefreshing) {
      return;
    }

    this.isRefreshing = true;
    try {
      await this.refreshDynamicTools(sendNotification);
    } finally {
      this.isRefreshing = false;
    }
  }

  /**
   * Check if tool exists
   */
  hasTool(toolName: string): boolean {
    return this.dynamicTools.has(toolName);
  }

  /**
   * Get tool by name
   */
  getTool(toolName: string): DynamicUnityCommandTool | undefined {
    return this.dynamicTools.get(toolName);
  }

  /**
   * Get all tools as array
   */
  getAllTools(): Tool[] {
    const tools: Tool[] = [];
    for (const [toolName, dynamicTool] of this.dynamicTools) {
      tools.push({
        name: toolName,
        description: dynamicTool.description,
        inputSchema: this.convertToMcpSchema(dynamicTool.inputSchema),
      });
    }
    return tools;
  }

  /**
   * Get tools count
   */
  getToolsCount(): number {
    return this.dynamicTools.size;
  }

  // IToolService interface compatibility methods
  /**
   * Initialize dynamic tools (IToolService interface)
   */
  async initializeTools(): Promise<void> {
    return this.initializeDynamicTools();
  }

  /**
   * Refresh dynamic tools (IToolService interface)
   */
  async refreshTools(): Promise<void> {
    return this.initializeDynamicTools();
  }

  /**
   * Convert input schema to MCP-compatible format safely
   */
  private convertToMcpSchema(inputSchema: unknown): {
    type: 'object';
    properties?: Record<string, unknown>;
    required?: string[];
  } {
    if (!inputSchema || typeof inputSchema !== 'object') {
      return { type: 'object' };
    }

    const schema = inputSchema as Record<string, unknown>;
    const result: {
      type: 'object';
      properties?: Record<string, unknown>;
      required?: string[];
    } = { type: 'object' };

    if (schema.properties && typeof schema.properties === 'object') {
      result.properties = schema.properties as Record<string, unknown>;
    }

    if (Array.isArray(schema.required)) {
      result.required = schema.required as string[];
    }

    return result;
  }
}
