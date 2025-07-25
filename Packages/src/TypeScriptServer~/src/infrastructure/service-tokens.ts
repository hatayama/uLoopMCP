/**
 * Service Tokens for Type-Safe Dependency Injection
 *
 * Design document reference:
 * - /Packages/docs/ARCHITECTURE_TypeScript.md#ServiceLocator
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
 * All service tokens for the application
 * Organized by service type and lifecycle
 */
export const ServiceTokens = {
  // Application Services (Singleton lifecycle)
  CONNECTION_APP_SERVICE: Symbol('CONNECTION_APP_SERVICE') as ServiceToken<IConnectionService>,
  TOOL_MANAGEMENT_APP_SERVICE: Symbol(
    'TOOL_MANAGEMENT_APP_SERVICE',
  ) as ServiceToken<IToolManagementService>,
  TOOL_QUERY_APP_SERVICE: Symbol('TOOL_QUERY_APP_SERVICE') as ServiceToken<IToolQueryService>,
  EVENT_APP_SERVICE: Symbol('EVENT_APP_SERVICE') as ServiceToken<IEventService>,
  MESSAGE_APP_SERVICE: Symbol('MESSAGE_APP_SERVICE') as ServiceToken<IMessageService>,
  DISCOVERY_APP_SERVICE: Symbol('DISCOVERY_APP_SERVICE') as ServiceToken<IDiscoveryService>,
  CLIENT_COMPATIBILITY_APP_SERVICE: Symbol(
    'CLIENT_COMPATIBILITY_APP_SERVICE',
  ) as ServiceToken<IClientCompatibilityService>,

  // UseCase Services (Transient lifecycle)
  EXECUTE_TOOL_USE_CASE: Symbol('EXECUTE_TOOL_USE_CASE') as ServiceToken<ExecuteToolUseCase>,
  REFRESH_TOOLS_USE_CASE: Symbol('REFRESH_TOOLS_USE_CASE') as ServiceToken<RefreshToolsUseCase>,
  INITIALIZE_SERVER_USE_CASE: Symbol(
    'INITIALIZE_SERVER_USE_CASE',
  ) as ServiceToken<InitializeServerUseCase>,
  HANDLE_CONNECTION_LOST_USE_CASE: Symbol(
    'HANDLE_CONNECTION_LOST_USE_CASE',
  ) as ServiceToken<HandleConnectionLostUseCase>,
  PROCESS_NOTIFICATION_USE_CASE: Symbol(
    'PROCESS_NOTIFICATION_USE_CASE',
  ) as ServiceToken<ProcessNotificationUseCase>,

  // Infrastructure Services (Singleton lifecycle)
  UNITY_CLIENT: Symbol('UNITY_CLIENT') as ServiceToken<unknown>,
  UNITY_CONNECTION_MANAGER: Symbol('UNITY_CONNECTION_MANAGER') as ServiceToken<unknown>,
  UNITY_TOOL_MANAGER: Symbol('UNITY_TOOL_MANAGER') as ServiceToken<unknown>,
  UNITY_DISCOVERY: Symbol('UNITY_DISCOVERY') as ServiceToken<unknown>,
  MCP_CLIENT_COMPATIBILITY: Symbol('MCP_CLIENT_COMPATIBILITY') as ServiceToken<unknown>,
  UNITY_EVENT_HANDLER: Symbol('UNITY_EVENT_HANDLER') as ServiceToken<unknown>,
  VIBE_LOGGER: Symbol('VIBE_LOGGER') as ServiceToken<unknown>,
} as const;

/**
 * Service token type mapping for better type inference
 * This helps with type-safe resolution in ServiceLocator
 */
import type { IConnectionService } from '../application/interfaces/connection-service.js';
import type { IToolManagementService } from '../application/interfaces/tool-management-service.js';
import type { IToolQueryService } from '../application/interfaces/tool-query-service.js';
import type { IEventService } from '../application/interfaces/event-service.js';
import type { IMessageService } from '../application/interfaces/message-service.js';
import type { IDiscoveryService } from '../application/interfaces/discovery-service.js';
import type { IClientCompatibilityService } from '../application/interfaces/client-compatibility-service.js';
import type { ExecuteToolUseCase } from '../domain/use-cases/execute-tool-use-case.js';
import type { RefreshToolsUseCase } from '../domain/use-cases/refresh-tools-use-case.js';
import type { InitializeServerUseCase } from '../domain/use-cases/initialize-server-use-case.js';
import type { HandleConnectionLostUseCase } from '../domain/use-cases/handle-connection-lost-use-case.js';
import type { ProcessNotificationUseCase } from '../domain/use-cases/process-notification-use-case.js';

export type ServiceTokenMap = {
  [ServiceTokens.CONNECTION_APP_SERVICE]: IConnectionService;
  [ServiceTokens.TOOL_MANAGEMENT_APP_SERVICE]: IToolManagementService;
  [ServiceTokens.TOOL_QUERY_APP_SERVICE]: IToolQueryService;
  [ServiceTokens.EVENT_APP_SERVICE]: IEventService;
  [ServiceTokens.MESSAGE_APP_SERVICE]: IMessageService;
  [ServiceTokens.DISCOVERY_APP_SERVICE]: IDiscoveryService;
  [ServiceTokens.CLIENT_COMPATIBILITY_APP_SERVICE]: IClientCompatibilityService;

  [ServiceTokens.EXECUTE_TOOL_USE_CASE]: ExecuteToolUseCase;
  [ServiceTokens.REFRESH_TOOLS_USE_CASE]: RefreshToolsUseCase;
  [ServiceTokens.INITIALIZE_SERVER_USE_CASE]: InitializeServerUseCase;
  [ServiceTokens.HANDLE_CONNECTION_LOST_USE_CASE]: HandleConnectionLostUseCase;
  [ServiceTokens.PROCESS_NOTIFICATION_USE_CASE]: ProcessNotificationUseCase;

  [ServiceTokens.UNITY_CLIENT]: unknown; // External class - avoid circular imports
  [ServiceTokens.UNITY_CONNECTION_MANAGER]: unknown;
  [ServiceTokens.UNITY_TOOL_MANAGER]: unknown;
  [ServiceTokens.UNITY_DISCOVERY]: unknown;
  [ServiceTokens.MCP_CLIENT_COMPATIBILITY]: unknown;
  [ServiceTokens.UNITY_EVENT_HANDLER]: unknown;
  [ServiceTokens.VIBE_LOGGER]: unknown;
};
