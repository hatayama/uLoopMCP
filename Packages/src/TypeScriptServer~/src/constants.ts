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
//
// Note on TOOLS_LIST_CHANGED:
// This is an MCP standard notification for tool list changes.
// In this project, it also serves as a "Unity ready" signal after Domain Reload.
// When Unity completes Domain Reload and finishes initialization (~16 seconds),
// it sends this notification. TypeScript side uses it to confirm Unity is ready
// to process requests. The name doesn't perfectly match this secondary purpose,
// but it works and avoids adding custom notification complexity.
export const NOTIFICATION_METHODS = {
  TOOLS_LIST_CHANGED: 'notifications/tools/list_changed',
  SERVER_SHUTDOWN: 'notifications/server/shutdown',
} as const;

// Server shutdown reason (must match Unity side ServerShutdownReason enum)
export const ServerShutdownReason = {
  DOMAIN_RELOAD: 'DomainReload',
  EDITOR_QUIT: 'EditorQuit',
} as const;

export type ServerShutdownReason = (typeof ServerShutdownReason)[keyof typeof ServerShutdownReason];

// Unity connection configuration
export const UNITY_CONNECTION = {
  DEFAULT_PORT: '8700',
  DEFAULT_HOST: '127.0.0.1',
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
  NETWORK: 180000, // 3 minutes - Network-level timeout (accounts for Roslyn initialization after Domain Reload)
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
  TIMEOUT:
    'timed out waiting for Unity response (uLoopMCP). Unity may be frozen in the background - bringing Unity window to foreground. Please check Unity and retry.',
  INVALID_RESPONSE: 'Invalid response from Unity',
} as const;

// Polling configuration
export const POLLING = {
  INTERVAL_MS: 1000, // Reduced from 3000ms to 1000ms for better responsiveness
  BUFFER_SECONDS: 15, // Increased for safer Unity startup timing
  // Adaptive polling configuration
  INITIAL_ATTEMPTS: 1, // Number of initial attempts with fast polling
  INITIAL_INTERVAL_MS: 1000, // Fast polling interval for initial attempts
  EXTENDED_INTERVAL_MS: 10000, // Slower polling interval after initial attempts
} as const;

// Log messages
export const LOG_MESSAGES = {
  SERVER_LOG_START_PREFIX: '=== Unity MCP Server Log Started at',
  CONNECTION_RECOVERY_POLLING: 'Starting connection recovery polling',
} as const;

// List of clients that support list_changed notifications
export const LIST_CHANGED_SUPPORTED_CLIENTS = ['cursor', 'mcp-inspector'] as const;

// File output directories
export const OUTPUT_DIRECTORIES = {
  ROOT: 'uLoopMCPOutputs',
  VIBE_LOGS: 'VibeLogs',
} as const;

// Connection lost debounce configuration
export const CONNECTION_LOST_DEBOUNCE_MS = 500;

// MCP Keepalive configuration
// Prevents Cursor's idle timeout by sending periodic pings to the client
export const KEEPALIVE = {
  ENABLED: true,
  INTERVAL_MS: 30000, // 30 seconds - shorter than Cursor's idle timeout
  TIMEOUT_MS: 5000, // Ping response timeout
  MAX_CONSECUTIVE_FAILURES: 3, // Stop keepalive after 3 consecutive failures
} as const;

// Connection recovery configuration
// Detects stuck states where connected=false persists despite Unity running
export const CONNECTION_RECOVERY = {
  STUCK_THRESHOLD_MS: 60000, // 60 seconds - if disconnected longer than this, consider stuck
  FORCE_RECONNECT_DELAY_MS: 2000, // Delay before force reconnection attempt
  MAX_FORCE_RECONNECT_ATTEMPTS: 3, // Maximum number of force reconnection attempts per stuck detection
} as const;
