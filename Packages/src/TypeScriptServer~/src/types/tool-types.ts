// Remove unused import

/**
 * Tool Type Definitions - Core interfaces for tool system
 *
 * Design document reference: Packages/src/TypeScriptServer~/ARCHITECTURE.md
 *
 * Related classes:
 * - BaseTool: Base class that implements ToolHandler interface
 * - DynamicUnityCommandTool: Specific tool implementation
 * - UnityToolManager: Manages dynamic tool registration from Unity tools
 *
 * Key features:
 * - ToolHandler interface for all tools
 * - ToolResponse type for MCP-compliant responses
 * - ToolContext for Unity client access
 * - ToolDefinition for tool metadata
 */

/**
 * Base interface for tool handlers
 */
export interface ToolHandler {
  readonly name: string;
  readonly description: string;
  readonly inputSchema: object;
  handle(args: unknown): Promise<ToolResponse>;
}

/**
 * Tool response type (aligned with MCP server return values)
 */
export interface ToolResponse {
  content: Array<{
    type: string;
    text: string;
  }>;
  isError?: boolean;
}

/**
 * Unity client interface for tool execution
 */
export interface UnityClient {
  readonly connected: boolean;
  executeTool(toolName: string, params?: Record<string, unknown>): Promise<unknown>;
}

/**
 * Tool execution context
 */
export interface ToolContext {
  unityClient: UnityClient;
}

/**
 * Tool definition type
 */
export interface ToolDefinition {
  name: string;
  description: string;
  inputSchema: object;
}
