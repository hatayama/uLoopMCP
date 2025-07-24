/**
 * Service Tokens for Type-Safe Dependency Injection
 *
 * Design document reference:
 * - .kiro/specs/typescript-server-ddd-refactoring/design.md#ServiceLocator
 *
 * Related classes:
 * - ServiceLocator (infrastructure/service-locator.ts)
 * - service-registration.ts (registration logic)
 * - All UseCase factory functions
 */

/**
 * Type-safe service token definition
 * Uses Symbol for unique identification and type information
 */
export type ServiceToken<T> = symbol & { __type: T };

/**
 * Create a type-safe service token
 */
export function createServiceToken<T>(name: string): ServiceToken<T> {
  return Symbol(name) as ServiceToken<T>;
}

/**
 * All service tokens for the application
 * Organized by service type and lifecycle
 */
export const ServiceTokens = {
  // Application Services (Singleton lifecycle)
  CONNECTION_APP_SERVICE: Symbol('CONNECTION_APP_SERVICE'),
  TOOL_MANAGEMENT_APP_SERVICE: Symbol('TOOL_MANAGEMENT_APP_SERVICE'),
  EVENT_APP_SERVICE: Symbol('EVENT_APP_SERVICE'),
  MESSAGE_APP_SERVICE: Symbol('MESSAGE_APP_SERVICE'),
  DISCOVERY_APP_SERVICE: Symbol('DISCOVERY_APP_SERVICE'),

  // UseCase Services (Transient lifecycle)
  EXECUTE_TOOL_USE_CASE: Symbol('EXECUTE_TOOL_USE_CASE'),
  REFRESH_TOOLS_USE_CASE: Symbol('REFRESH_TOOLS_USE_CASE'),
  INITIALIZE_SERVER_USE_CASE: Symbol('INITIALIZE_SERVER_USE_CASE'),
  HANDLE_CONNECTION_LOST_USE_CASE: Symbol('HANDLE_CONNECTION_LOST_USE_CASE'),
  PROCESS_NOTIFICATION_USE_CASE: Symbol('PROCESS_NOTIFICATION_USE_CASE'),

  // Infrastructure Services (Singleton lifecycle)
  UNITY_CLIENT: Symbol('UNITY_CLIENT'),
  UNITY_CONNECTION_MANAGER: Symbol('UNITY_CONNECTION_MANAGER'),
  UNITY_TOOL_MANAGER: Symbol('UNITY_TOOL_MANAGER'),
  UNITY_DISCOVERY: Symbol('UNITY_DISCOVERY'),
  MCP_CLIENT_COMPATIBILITY: Symbol('MCP_CLIENT_COMPATIBILITY'),
  UNITY_EVENT_HANDLER: Symbol('UNITY_EVENT_HANDLER'),
  VIBE_LOGGER: Symbol('VIBE_LOGGER'),
} as const;

/**
 * Service token type mapping for better type inference
 * This helps with type-safe resolution in ServiceLocator
 */
export type ServiceTokenMap = {
  [ServiceTokens.CONNECTION_APP_SERVICE]: any; // Will be typed properly when implemented
  [ServiceTokens.TOOL_MANAGEMENT_APP_SERVICE]: any;
  [ServiceTokens.EVENT_APP_SERVICE]: any;
  [ServiceTokens.MESSAGE_APP_SERVICE]: any;
  [ServiceTokens.DISCOVERY_APP_SERVICE]: any;

  [ServiceTokens.EXECUTE_TOOL_USE_CASE]: any;
  [ServiceTokens.REFRESH_TOOLS_USE_CASE]: any;
  [ServiceTokens.INITIALIZE_SERVER_USE_CASE]: any;
  [ServiceTokens.HANDLE_CONNECTION_LOST_USE_CASE]: any;
  [ServiceTokens.PROCESS_NOTIFICATION_USE_CASE]: any;

  [ServiceTokens.UNITY_CLIENT]: any;
  [ServiceTokens.UNITY_CONNECTION_MANAGER]: any;
  [ServiceTokens.UNITY_TOOL_MANAGER]: any;
  [ServiceTokens.UNITY_DISCOVERY]: any;
  [ServiceTokens.MCP_CLIENT_COMPATIBILITY]: any;
  [ServiceTokens.UNITY_EVENT_HANDLER]: any;
  [ServiceTokens.VIBE_LOGGER]: any;
};
