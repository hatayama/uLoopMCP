/**
 * Domain Tool Model - Pure Business Logic without External Dependencies
 *
 * Design document reference:
 * - .kiro/specs/typescript-server-ddd-refactoring/design.md#CleanArchitecture
 *
 * This model represents the core business concept of a "Tool" in our domain,
 * completely independent of any external framework or library (like MCP SDK).
 * 
 * Key principles:
 * - No imports from external libraries
 * - Pure business logic representation
 * - Framework-agnostic design
 * - Infrastructure layer handles conversion to/from external formats
 */

/**
 * Domain representation of a tool
 * This is the pure business concept, not tied to any external framework
 */
export interface DomainTool {
  /**
   * Unique identifier for the tool
   */
  name: string;

  /**
   * Human-readable description of what the tool does
   */
  description?: string;

  /**
   * Input schema definition (JSON Schema format)
   * Kept as unknown to avoid external schema library dependencies
   */
  inputSchema?: unknown;

  /**
   * Tool category for organization
   */
  category?: string;

  /**
   * Whether this tool is available in development mode only
   */
  isDevelopmentOnly?: boolean;

  /**
   * Tool execution timeout in milliseconds
   */
  timeoutMs?: number;

  /**
   * Tool version for compatibility tracking
   */
  version?: string;
}

/**
 * Domain representation of tool execution result
 */
export interface DomainToolExecutionResult {
  /**
   * Whether the execution was successful
   */
  success: boolean;

  /**
   * Result data from tool execution
   */
  data?: unknown;

  /**
   * Error message if execution failed
   */
  error?: string;

  /**
   * Execution time in milliseconds
   */
  executionTimeMs?: number;

  /**
   * Additional metadata from execution
   */
  metadata?: Record<string, unknown>;
}

/**
 * Domain representation of tool collection
 */
export interface DomainToolCollection {
  /**
   * List of available tools
   */
  tools: DomainTool[];

  /**
   * Total count of tools
   */
  count: number;

  /**
   * When this collection was last updated
   */
  lastUpdated: Date;

  /**
   * Source of the tools (e.g., "Unity", "External API")
   */
  source?: string;
}