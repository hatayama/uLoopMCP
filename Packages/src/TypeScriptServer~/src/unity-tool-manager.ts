import { UnityClient } from './unity-client.js';
import { DynamicUnityCommandTool } from './tools/dynamic-unity-command-tool.js';
import { Tool } from '@modelcontextprotocol/sdk/types.js';
import { errorToFile, debugToFile } from './utils/log-to-file.js';
import { ENVIRONMENT } from './constants.js';

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
export class UnityToolManager {
  private unityClient: UnityClient;
  private readonly isDevelopment: boolean;
  private readonly dynamicTools: Map<string, DynamicUnityCommandTool> = new Map();
  private isRefreshing: boolean = false;
  private clientName: string = '';

  constructor(unityClient: UnityClient) {
    this.unityClient = unityClient;
    this.isDevelopment = process.env.NODE_ENV === ENVIRONMENT.NODE_ENV_DEVELOPMENT;
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
      const toolDetails = await this.fetchToolDetailsFromUnity();

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
          inputSchema: dynamicTool.inputSchema,
        });
      }

      return tools;
    } catch (error) {
      errorToFile('[Unity Tool Manager] Failed to get tools from Unity:', error);
      return [];
    }
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
      errorToFile('[Unity Tool Manager] Failed to initialize dynamic tools:', error);
      // Continue without dynamic tools
    }
  }

  /**
   * Fetch tool details from Unity
   */
  private async fetchToolDetailsFromUnity(): Promise<unknown[] | null> {
    // Get detailed tool information including schemas
    // Include development-only tools if in development mode
    const params = { IncludeDevelopmentOnly: this.isDevelopment };

    debugToFile('[Unity Tool Manager] Requesting tool details from Unity with params:', params);
    const startTime = Date.now();

    try {
      const toolDetailsResponse = await this.unityClient.executeTool('get-tool-details', params);
      const duration = Date.now() - startTime;

      debugToFile(
        '[Unity Tool Manager] Received tool details response in',
        duration,
        'ms:',
        toolDetailsResponse,
      );

      // Handle new GetToolDetailsResponse structure
      const toolDetails =
        (toolDetailsResponse as { Tools?: unknown[] })?.Tools || toolDetailsResponse;
      if (!Array.isArray(toolDetails)) {
        errorToFile('[Unity Tool Manager] Invalid tool details response:', toolDetailsResponse);
        return null;
      }

      debugToFile(
        '[Unity Tool Manager] Successfully parsed',
        toolDetails.length,
        'tools from Unity',
      );
      return toolDetails as unknown[];
    } catch (error) {
      const duration = Date.now() - startTime;
      errorToFile(
        '[Unity Tool Manager] Failed to fetch tool details after',
        duration,
        'ms:',
        error,
      );
      throw error;
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
        parameterSchema, // Pass schema information
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
   * Safe version of refreshDynamicTools that prevents duplicate execution
   */
  async refreshDynamicToolsSafe(sendNotification?: () => void): Promise<void> {
    if (this.isRefreshing) {
      if (this.isDevelopment) {
        debugToFile('[Unity Tool Manager] refreshDynamicToolsSafe skipped: already in progress');
      }
      return;
    }

    this.isRefreshing = true;
    try {
      if (this.isDevelopment) {
        const stack = new Error().stack;
        const callerLine = stack?.split('\n')[2]?.trim() || 'Unknown caller';
        const timestamp = new Date().toISOString().split('T')[1].slice(0, 12);
        debugToFile(
          `[Unity Tool Manager] refreshDynamicToolsSafe called at ${timestamp} from: ${callerLine}`,
        );
      }

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
        inputSchema: dynamicTool.inputSchema,
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
}
