/**
 * Tool Service Interface
 *
 * Design document reference:
 * - .kiro/specs/typescript-server-ddd-refactoring/design.md#IToolService
 *
 * Related classes:
 * - UnityToolManager (existing implementation class)
 * - ToolManagementAppService (new application service implementation)
 * - DynamicUnityCommandTool (tool implementation)
 * - Used by UseCase classes
 */

import { Tool } from '@modelcontextprotocol/sdk/types.js';
import { DynamicUnityCommandTool } from '../../tools/dynamic-unity-command-tool.js';
import { ApplicationService } from '../../domain/base-interfaces.js';

/**
 * Interface providing technical functionality for Unity tool management
 *
 * Responsibilities:
 * - Unity tool initialization and updates
 * - Tool information retrieval and management
 * - Dynamic tool creation and deletion
 * - Provide single-purpose operations
 */
export interface IToolService extends ApplicationService {
  /**
   * Initialize dynamic tools
   *
   * @throws ToolExecutionError if initialization fails
   */
  initializeTools(): Promise<void>;

  /**
   * Refresh dynamic tools (re-fetch)
   *
   * @throws ToolExecutionError if refresh fails
   */
  refreshTools(): Promise<void>;

  /**
   * Get all tools
   *
   * @returns Array of tools
   */
  getAllTools(): Tool[];

  /**
   * Check if specified tool exists
   *
   * @param name Tool name
   * @returns true if exists
   */
  hasTool(name: string): boolean;

  /**
   * Get specified tool
   *
   * @param name Tool name
   * @returns Tool instance, undefined if not found
   */
  getTool(name: string): DynamicUnityCommandTool | undefined;

  /**
   * Get tool list from Unity
   *
   * @returns Array of tools retrieved from Unity
   * @throws ToolExecutionError if Unity communication fails
   */
  getToolsFromUnity(): Promise<Tool[]>;

  /**
   * Set client name
   *
   * @param clientName Client name
   */
  setClientName(clientName: string): void;

  /**
   * Get number of tools
   *
   * @returns Current number of tools
   */
  getToolsCount(): number;
}
