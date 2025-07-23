import { UnityClient } from './unity-client.js';
import { DynamicUnityCommandTool } from './tools/dynamic-unity-command-tool.js';
import { Tool } from '@modelcontextprotocol/sdk/types.js';
import { ENVIRONMENT } from './constants.js';
import { VibeLogger } from './utils/vibe-logger.js';
import { createHash } from 'crypto';

// Import UnityParameterSchema type from the tool file
type UnityParameterSchema = { [key: string]: unknown };

/**
 * Unity Tool Manager - Manages dynamic tool generation and management
 *
 * Design document reference: .kiro/specs/mcp-tool-recognition-fix/design.md
 *
 * Related classes:
 * - UnityClient: Manages communication with Unity Editor
 * - DynamicUnityCommandTool: Implementation of dynamic tools
 * - UnityMcpServer: Main server class that uses this manager
 * - ClientInitializationHandler: Uses this for tool availability management
 *
 * Key features:
 * - Unity connection state-aware tool list management
 * - Dynamic tool generation from Unity tools
 * - Tool list change detection and caching
 * - Performance-optimized tool retrieval
 * - Tool refresh management
 * - Tool details fetching and parsing
 * - Development mode support
 */
export class UnityToolManager {
  private unityClient: UnityClient;
  private readonly includeDevelopmentOnlyTools: boolean;
  private readonly dynamicTools: Map<string, DynamicUnityCommandTool> = new Map();
  private isRefreshing: boolean = false;
  private clientName: string = '';
  private cachedToolList: Tool[] = [];
  private lastToolListHash: string = '';
  private clientInitializationHandler?: {
    handleUnityConnection(): void;
    handleUnityDisconnection(): void;
  };

  constructor(unityClient: UnityClient) {
    this.unityClient = unityClient;
    this.includeDevelopmentOnlyTools = process.env.NODE_ENV === ENVIRONMENT.NODE_ENV_DEVELOPMENT;
  }

  /**
   * Set client name for Unity communication
   */
  setClientName(clientName: string): void {
    this.clientName = clientName;
  }

  /**
   * Set client initialization handler for Unity connection events
   */
  setClientInitializationHandler(handler: {
    handleUnityConnection(): void;
    handleUnityDisconnection(): void;
  }): void {
    this.clientInitializationHandler = handler;
  }

  /**
   * Get dynamic tools map
   */
  getDynamicTools(): Map<string, DynamicUnityCommandTool> {
    return this.dynamicTools;
  }

  /**
   * Get available tools based on Unity connection state
   */
  async getAvailableTools(): Promise<Tool[]> {
    if (!this.isUnityConnected()) {
      VibeLogger.logInfo(
        'mcp_tools_unavailable_no_unity',
        'Tools unavailable - Unity not connected',
        {
          client_name: this.clientName,
          unity_connected: false,
          cached_tools_count: this.cachedToolList.length,
        },
        undefined,
        'Unity not connected, returning empty tool list',
      );
      return []; // Unity not connected - return empty list
    }

    try {
      const tools = await this.fetchUnityTools();
      this.updateToolCache(tools);

      VibeLogger.logInfo(
        'mcp_tools_available',
        'Tools available from Unity',
        {
          client_name: this.clientName,
          tools_count: tools.length,
          unity_connected: true,
        },
        undefined,
        'Unity tools successfully retrieved and cached',
      );

      return tools;
    } catch (error) {
      VibeLogger.logError(
        'mcp_tools_fetch_error',
        'Failed to fetch tools from Unity',
        {
          client_name: this.clientName,
          error_message: error instanceof Error ? error.message : String(error),
          unity_connected: this.isUnityConnected(),
        },
        undefined,
        'Unity tool fetch failed, returning cached tools or empty list',
      );

      // Return cached tools on error, or empty list if no cache
      return this.cachedToolList.length > 0 ? this.cachedToolList : [];
    }
  }

  /**
   * Get tools from Unity (original method for backward compatibility)
   */
  async getToolsFromUnity(): Promise<Tool[]> {
    return await this.getAvailableTools();
  }

  /**
   * Initialize dynamic Unity tools
   */
  async initializeDynamicTools(): Promise<void> {
    try {
      await this.unityClient.ensureConnected();

      const toolDetails = await this.fetchToolDetailsFromUnity();
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
   * Fetch tool details from Unity
   */
  private async fetchToolDetailsFromUnity(): Promise<unknown[] | null> {
    // Get detailed tool information including schemas
    // Include development-only tools if in development mode
    const params = { IncludeDevelopmentOnly: this.includeDevelopmentOnlyTools };

    // Requesting tool details from Unity with params

    const toolDetailsResponse = await this.unityClient.executeTool('get-tool-details', params);
    // Received tool details response

    // Handle new GetToolDetailsResponse structure
    const toolDetails =
      (toolDetailsResponse as { Tools?: unknown[] })?.Tools || toolDetailsResponse;
    if (!Array.isArray(toolDetails)) {
      // Invalid tool details response
      return null;
    }

    // Successfully parsed tools from Unity
    return toolDetails as unknown[];
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
      if (displayDevelopmentOnly && !this.includeDevelopmentOnlyTools) {
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
   */
  async refreshDynamicTools(sendNotification?: () => void): Promise<void> {
    await this.initializeDynamicTools();

    // Send tools changed notification to MCP client if callback provided
    if (sendNotification) {
      sendNotification();
    }
  }

  /**
   * Safe version of refreshDynamicTools using ToolRefreshUseCase pattern
   * Prevents duplicate execution and provides temporal cohesion
   */
  async refreshDynamicToolsSafe(sendNotification?: () => void): Promise<void> {
    if (this.isRefreshing) {
      if (this.includeDevelopmentOnlyTools) {
        // refreshDynamicToolsSafe skipped: already in progress
      }
      return;
    }

    this.isRefreshing = true;
    try {
      if (this.includeDevelopmentOnlyTools) {
        // refreshDynamicToolsSafe called
      }

      // Import UseCase dynamically to avoid circular dependencies
      const { ToolRefreshUseCase } = await import('./usecases/tool-refresh-use-case.js');
      
      // Create UseCase instance (single-use pattern)
      const useCase = new ToolRefreshUseCase(
        this.unityClient,
        this.includeDevelopmentOnlyTools,
        sendNotification,
      );
      
      // Execute all refresh steps with temporal cohesion
      const result = await useCase.execute();
      
      // UseCase instance is automatically discarded after this point
      
      // Handle the result by updating the tool cache if successful
      if (result.isSuccess && result.reason === 'success') {
        // The UseCase has already handled tool creation, notification, etc.
        // We just need to re-run the full refresh to update our cache
        await this.refreshDynamicTools(undefined); // Don't send notification again
      }
      
      // Log result for debugging
      if (!result.isSuccess) {
        // Tool refresh UseCase failed but error already logged
        // Continue with existing behavior (no exception thrown)
      }
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
   * Convert dynamic tools map to Tool array
   */
  private convertDynamicToolsToArray(): Tool[] {
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
   * Get all tools as array
   */
  getAllTools(): Tool[] {
    return this.convertDynamicToolsToArray();
  }

  /**
   * Get tools count
   */
  getToolsCount(): number {
    return this.dynamicTools.size;
  }

  /**
   * Check if Unity is connected
   */
  private isUnityConnected(): boolean {
    return this.unityClient.connected;
  }

  /**
   * Fetch tools from Unity with error handling
   */
  private async fetchUnityTools(): Promise<Tool[]> {
    const toolDetails = await this.fetchToolDetailsFromUnity();

    if (!toolDetails) {
      return [];
    }

    this.createDynamicToolsFromTools(toolDetails);

    // Convert dynamic tools to Tool array
    return this.convertDynamicToolsToArray();
  }

  /**
   * Update tool cache and detect changes
   */
  private updateToolCache(tools: Tool[]): void {
    this.cachedToolList = [...tools]; // Create a copy
    this.lastToolListHash = this.calculateToolListHash(tools);
  }

  /**
   * Check if tool list has changed since last update
   */
  hasToolListChanged(): boolean {
    if (!this.isUnityConnected()) {
      return false; // No change if Unity is not connected
    }

    try {
      // Get current tools without updating cache
      const currentTools = this.getAllTools();
      const currentHash = this.calculateToolListHash(currentTools);
      const hasChanged = currentHash !== this.lastToolListHash;

      if (hasChanged) {
        VibeLogger.logInfo(
          'mcp_tools_list_changed',
          'Tool list change detected',
          {
            client_name: this.clientName,
            previous_hash: this.lastToolListHash,
            current_hash: currentHash,
            tools_count: currentTools.length,
          },
          undefined,
          'Unity tool list has changed since last update',
        );
      }

      return hasChanged;
    } catch (error) {
      VibeLogger.logError(
        'mcp_tools_change_detection_error',
        'Error during tool list change detection',
        {
          client_name: this.clientName,
          error_message: error instanceof Error ? error.message : String(error),
        },
        undefined,
        'Tool change detection failed',
      );
      return false;
    }
  }

  /**
   * Handle Unity connection established
   */
  async onUnityConnected(): Promise<void> {
    try {
      VibeLogger.logInfo(
        'mcp_unity_connected_tools_refresh',
        'Unity connected - refreshing tools',
        { client_name: this.clientName },
        undefined,
        'Unity connection established, refreshing tool list',
      );

      const tools = await this.fetchUnityTools();
      this.updateToolCache(tools);

      // Notify ClientInitializationHandler if available
      if (this.clientInitializationHandler) {
        this.clientInitializationHandler.handleUnityConnection();
      }
    } catch (error) {
      VibeLogger.logError(
        'mcp_unity_connected_tools_refresh_error',
        'Error refreshing tools after Unity connection',
        {
          client_name: this.clientName,
          error_message: error instanceof Error ? error.message : String(error),
        },
        undefined,
        'Failed to refresh tools after Unity connection',
      );
    }
  }

  /**
   * Handle Unity disconnection
   */
  onUnityDisconnected(): void {
    VibeLogger.logInfo(
      'mcp_unity_disconnected_tools_clear',
      'Unity disconnected - clearing tools',
      { client_name: this.clientName, cached_tools_count: this.cachedToolList.length },
      undefined,
      'Unity disconnected, clearing tool cache',
    );

    this.cachedToolList = [];
    this.lastToolListHash = '';
    this.dynamicTools.clear();

    // Notify ClientInitializationHandler if available
    if (this.clientInitializationHandler) {
      this.clientInitializationHandler.handleUnityDisconnection();
    }
  }

  /**
   * Calculate hash for tool list to detect changes
   */
  private calculateToolListHash(tools: Tool[]): string {
    const toolNames = tools.map((tool) => tool.name).sort();
    return createHash('md5').update(JSON.stringify(toolNames)).digest('hex');
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
