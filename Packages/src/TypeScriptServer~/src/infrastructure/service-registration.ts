/**
 * Service Registration Configuration
 *
 * Design document reference:
 * - /Packages/docs/ARCHITECTURE_TypeScript.md#ファクトリー関数
 *
 * Related classes:
 * - ServiceLocator (infrastructure/service-locator.ts)
 * - ServiceTokens (infrastructure/service-tokens.ts)
 * - All ApplicationService and UseCase implementations
 */

import { ServiceLocator } from './service-locator.js';
import { ServiceTokens } from './service-tokens.js';

// Infrastructure Services
import { UnityClient } from '../unity-client.js';
import { UnityConnectionManager } from '../unity-connection-manager.js';
import { UnityToolManager } from '../unity-tool-manager.js';
import { McpClientCompatibility } from '../mcp-client-compatibility.js';

// Application Services (placeholder implementations - to be created later)
// import { ConnectionAppService } from '../application/services/connection-app-service.js';
// import { ToolManagementAppService } from '../application/services/tool-management-app-service.js';

// UseCase implementations
import { ExecuteToolUseCase } from '../domain/use-cases/execute-tool-use-case.js';
import { RefreshToolsUseCase } from '../domain/use-cases/refresh-tools-use-case.js';
import { InitializeServerUseCase } from '../domain/use-cases/initialize-server-use-case.js';
import { HandleConnectionLostUseCase } from '../domain/use-cases/handle-connection-lost-use-case.js';
import { ProcessNotificationUseCase } from '../domain/use-cases/process-notification-use-case.js';

// Application Service interfaces
import { IConnectionService } from '../application/interfaces/connection-service.js';
import { IToolQueryService } from '../application/interfaces/tool-query-service.js';
import { IToolManagementService } from '../application/interfaces/tool-management-service.js';
import { IClientCompatibilityService } from '../application/interfaces/client-compatibility-service.js';
import { IDiscoveryService } from '../application/interfaces/discovery-service.js';
import { INotificationService } from '../application/interfaces/notification-service.js';

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

  // Register Infrastructure Services
  ServiceLocator.register(ServiceTokens.UNITY_CLIENT, () => UnityClient.getInstance());

  // Register actual implementation classes as application services temporarily
  // TODO: Replace with proper application service wrappers in future
  ServiceLocator.register(ServiceTokens.CONNECTION_APP_SERVICE, () => {
    const unityClient = ServiceLocator.resolve(ServiceTokens.UNITY_CLIENT) as UnityClient;
    return new UnityConnectionManager(unityClient);
  });

  ServiceLocator.register(ServiceTokens.TOOL_MANAGEMENT_APP_SERVICE, () => {
    const unityClient = ServiceLocator.resolve(ServiceTokens.UNITY_CLIENT) as UnityClient;
    return new UnityToolManager(unityClient);
  });

  // Register same UnityToolManager instance for tool query operations
  ServiceLocator.register(ServiceTokens.TOOL_QUERY_APP_SERVICE, () => {
    const unityClient = ServiceLocator.resolve(ServiceTokens.UNITY_CLIENT) as UnityClient;
    return new UnityToolManager(unityClient);
  });

  ServiceLocator.register(ServiceTokens.CLIENT_COMPATIBILITY_APP_SERVICE, () => {
    const unityClient = ServiceLocator.resolve(ServiceTokens.UNITY_CLIENT) as UnityClient;
    return new McpClientCompatibility(unityClient);
  });

  // Register UseCase factories (create new instance each time)
  ServiceLocator.register(ServiceTokens.EXECUTE_TOOL_USE_CASE, () => {
    const connectionService = ServiceLocator.resolve<IConnectionService>(
      ServiceTokens.CONNECTION_APP_SERVICE,
    );
    const toolService = ServiceLocator.resolve<IToolQueryService>(
      ServiceTokens.TOOL_QUERY_APP_SERVICE,
    );
    return new ExecuteToolUseCase(connectionService, toolService);
  });

  ServiceLocator.register(ServiceTokens.REFRESH_TOOLS_USE_CASE, () => {
    const connectionService = ServiceLocator.resolve<IConnectionService>(
      ServiceTokens.CONNECTION_APP_SERVICE,
    );
    const toolManagementService = ServiceLocator.resolve<IToolManagementService>(
      ServiceTokens.TOOL_MANAGEMENT_APP_SERVICE,
    );
    const toolQueryService = ServiceLocator.resolve<IToolQueryService>(
      ServiceTokens.TOOL_QUERY_APP_SERVICE,
    );
    return new RefreshToolsUseCase(connectionService, toolManagementService, toolQueryService);
  });

  ServiceLocator.register(ServiceTokens.INITIALIZE_SERVER_USE_CASE, () => {
    const connectionService = ServiceLocator.resolve<IConnectionService>(
      ServiceTokens.CONNECTION_APP_SERVICE,
    );
    const toolService = ServiceLocator.resolve<IToolQueryService>(
      ServiceTokens.TOOL_QUERY_APP_SERVICE,
    );
    const toolManagementService = ServiceLocator.resolve<IToolManagementService>(
      ServiceTokens.TOOL_MANAGEMENT_APP_SERVICE,
    );
    const clientCompatibilityService = ServiceLocator.resolve<IClientCompatibilityService>(
      ServiceTokens.CLIENT_COMPATIBILITY_APP_SERVICE,
    );
    return new InitializeServerUseCase(
      connectionService,
      toolService,
      toolManagementService,
      clientCompatibilityService,
    );
  });

  ServiceLocator.register(ServiceTokens.HANDLE_CONNECTION_LOST_USE_CASE, () => {
    const connectionService = ServiceLocator.resolve<IConnectionService>(
      ServiceTokens.CONNECTION_APP_SERVICE,
    );
    const toolManagementService = ServiceLocator.resolve<IToolManagementService>(
      ServiceTokens.TOOL_MANAGEMENT_APP_SERVICE,
    );
    const discoveryService = ServiceLocator.resolve<IDiscoveryService>(
      ServiceTokens.DISCOVERY_APP_SERVICE,
    );
    return new HandleConnectionLostUseCase(
      connectionService,
      toolManagementService,
      discoveryService,
    );
  });

  ServiceLocator.register(ServiceTokens.PROCESS_NOTIFICATION_USE_CASE, () => {
    const notificationService = ServiceLocator.resolve<INotificationService>(
      ServiceTokens.EVENT_APP_SERVICE,
    );
    return new ProcessNotificationUseCase(notificationService);
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
