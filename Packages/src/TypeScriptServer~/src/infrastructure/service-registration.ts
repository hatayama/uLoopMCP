/**
 * Service Registration Configuration
 *
 * Design document reference:
 * - .kiro/specs/typescript-server-ddd-refactoring/design.md#ファクトリー関数
 *
 * Related classes:
 * - ServiceLocator (infrastructure/service-locator.ts)
 * - ServiceTokens (infrastructure/service-tokens.ts)
 * - All ApplicationService and UseCase implementations
 */

import { ServiceLocator } from './service-locator.js';
import { ServiceTokens } from './service-tokens.js';

// Application Services (placeholder implementations - to be created later)
// import { ConnectionAppService } from '../application/services/connection-app-service.js';
// import { ToolManagementAppService } from '../application/services/tool-management-app-service.js';

// UseCase implementations
import { createExecuteToolUseCase } from '../domain/use-cases/execute-tool-use-case.js';
import { createRefreshToolsUseCase } from '../domain/use-cases/refresh-tools-use-case.js';
import { createInitializeServerUseCase } from '../domain/use-cases/initialize-server-use-case.js';
import { createHandleConnectionLostUseCase } from '../domain/use-cases/handle-connection-lost-use-case.js';

// Infrastructure components
// import { UnityClient } from '../unity-client.js';
// import { VibeLogger } from '../utils/vibe-logger.js';

/**
 * Register all services with the ServiceLocator
 *
 * This function sets up dependency injection by registering factory functions
 * for all services, maintaining the principle that:
 * - ApplicationServices are singletons (same instance returned)
 * - UseCases are created fresh each time (new instance per request)
 * - Infrastructure components are managed appropriately
 */
export function registerServices(): void {
  // TODO: Register Application Services (will be implemented in Phase 4)
  // ServiceLocator.register(ServiceTokens.CONNECTION_APP_SERVICE, () => {
  //   const unityClient = ServiceLocator.resolve(ServiceTokens.UNITY_CLIENT);
  //   return new ConnectionAppService(unityClient);
  // });

  // TODO: Register Infrastructure Services (will be connected in Phase 4)
  // ServiceLocator.register(ServiceTokens.UNITY_CLIENT, () => UnityClient.getInstance());

  // Register UseCase factories (create new instance each time)
  ServiceLocator.register(ServiceTokens.EXECUTE_TOOL_USE_CASE, () => {
    return createExecuteToolUseCase();
  });

  ServiceLocator.register(ServiceTokens.REFRESH_TOOLS_USE_CASE, () => {
    return createRefreshToolsUseCase();
  });

  ServiceLocator.register(ServiceTokens.INITIALIZE_SERVER_USE_CASE, () => {
    return createInitializeServerUseCase();
  });

  ServiceLocator.register(ServiceTokens.HANDLE_CONNECTION_LOST_USE_CASE, () => {
    return createHandleConnectionLostUseCase();
  });

  // TODO: Add more UseCase registrations as they are implemented
}

/**
 * Clear all service registrations
 *
 * Primarily used for testing
 */
export function clearServices(): void {
  ServiceLocator.clear();
}
