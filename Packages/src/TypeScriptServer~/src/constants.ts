/**
 * Unity MCP Server common constants
 * Centralized management of constants used across all files
 *
 * Design document reference: Packages/src/TypeScriptServer~/ARCHITECTURE.md
 *
 * Related files:
 * - server.ts: Main server that uses these constants
 * - unity-connection-manager.ts: Uses connection and polling constants
 * - unity-tool-manager.ts: Uses timeout and configuration constants
 * - mcp-client-compatibility.ts: Uses client compatibility constants
 * - unity-event-handler.ts: Uses environment constants
 * - unity-client.ts: Unity TCP client that uses connection constants
 * - tools/*-tool.ts: Tool implementations that use timeout and configuration constants
 *
 * Key features:
 * - MCP protocol constants (version, capabilities)
 * - Server configuration (name, version)
 * - Unity connection settings (port, host)
 * - JSON-RPC protocol constants
 * - Tool-specific timeouts and configurations
 * - Error messages and logging configuration
 * - Client compatibility definitions
 */

// MCP Protocol Constants
export const MCP_PROTOCOL_VERSION = '2024-11-05';
export const MCP_SERVER_NAME = 'uloopmcp-server';

// MCP Capabilities
export const TOOLS_LIST_CHANGED_CAPABILITY = true;

// MCP Notification methods
export const NOTIFICATION_METHODS = {
  TOOLS_LIST_CHANGED: 'notifications/tools/list_changed',
} as const;

// Server configuration (legacy - kept for backward compatibility)
export const SERVER_CONFIG = {
  NAME: 'unity-mcp-server',
  VERSION: '0.1.0',
} as const;

// Unity connection configuration
export const UNITY_CONNECTION = {
  DEFAULT_PORT: '8700',
  DEFAULT_HOST: 'localhost',
  CONNECTION_TEST_MESSAGE: 'connection_test',
} as const;

// JSON-RPC configuration
export const JSONRPC = {
  VERSION: '2.0',
} as const;

// Parameter schema constants (must match Unity side)
export const PARAMETER_SCHEMA = {
  TYPE_PROPERTY: 'Type',
  DESCRIPTION_PROPERTY: 'Description',
  DEFAULT_VALUE_PROPERTY: 'DefaultValue',
  ENUM_PROPERTY: 'Enum',
  PROPERTIES_PROPERTY: 'Properties',
  REQUIRED_PROPERTY: 'Required',
} as const;

// Timeout configuration (milliseconds)
export const TIMEOUTS = {
  NETWORK: 120000, // 2分 - ネットワークレベルのタイムアウト（Unity側のタイムアウトより長く設定）
} as const;

// Log configuration
export const LOG_CONFIG = {
  TYPES: ['Error', 'Warning', 'Log', 'All'] as const,
  DEFAULT_TYPE: 'All',
  DEFAULT_MAX_COUNT: 100,
  DEFAULT_SEARCH_TEXT: '',
  DEFAULT_INCLUDE_STACK_TRACE: true,
} as const;

// Test configuration
export const TEST_CONFIG = {
  FILTER_TYPES: ['all', 'fullclassname', 'namespace', 'testname', 'assembly'] as const,
  DEFAULT_FILTER_TYPE: 'all',
  DEFAULT_FILTER_VALUE: '',
  DEFAULT_SAVE_XML: false,
} as const;

// Compile configuration
export const COMPILE_CONFIG = {
  DEFAULT_FORCE_RECOMPILE: false,
} as const;

// Default messages
export const DEFAULT_MESSAGES = {
  PING: 'Hello Unity MCP!',
  UNITY_PING: 'Hello from TypeScript MCP Server',
} as const;

// Client Name Constants
export const DEFAULT_CLIENT_NAME = ''; // Empty string to avoid showing default names

// Environment configuration
export const ENVIRONMENT = {
  NODE_ENV_DEVELOPMENT: 'development',
  NODE_ENV_PRODUCTION: 'production',
} as const;

// Error messages
export const ERROR_MESSAGES = {
  NOT_CONNECTED: 'Unity MCP Bridge is not connected',
  CONNECTION_FAILED: 'Unity connection failed',
  TIMEOUT: 'timeout',
  INVALID_RESPONSE: 'Invalid response from Unity',
} as const;

// Test timeouts (milliseconds)
export const TEST_TIMEOUTS = {
  INTEGRATION_TEST_MS: 2000,
  TOOLS_LIST_TEST_MS: 3000,
  JEST_DEFAULT_MS: 10000,
} as const;

// Log messages
export const LOG_MESSAGES = {
  SERVER_LOG_START_PREFIX: '=== Unity MCP Server Log Started at',
  CONNECTION_RECOVERY_POLLING: 'Starting connection recovery polling',
} as const;


// File output directories
export const OUTPUT_DIRECTORIES = {
  ROOT: 'uLoopMCPOutputs',
  VIBE_LOGS: 'VibeLogs',
} as const;
