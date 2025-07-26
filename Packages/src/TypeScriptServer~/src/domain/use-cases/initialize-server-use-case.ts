/**
 * Initialize Server UseCase
 *
 * Design document reference:
 * - /Packages/docs/ARCHITECTURE_TypeScript.md#InitializeServerUseCase
 *
 * Related classes:
 * - IConnectionService (application/interfaces/connection-service.ts)
 * - IToolQueryService (application/interfaces/tool-query-service.ts)
 * - IClientCompatibilityService (application/interfaces/client-compatibility-service.ts)
 * - ServiceLocator (infrastructure/service-locator.ts)
 */

import { UseCase } from '../base-interfaces.js';
import { InitializeServerRequest } from '../models/requests.js';
import { InitializeServerResponse } from '../models/responses.js';
import { VibeLogger } from '../../utils/vibe-logger.js';
import { IConnectionService } from '../../application/interfaces/connection-service.js';
import { IToolQueryService } from '../../application/interfaces/tool-query-service.js';
import { IToolManagementService } from '../../application/interfaces/tool-management-service.js';
import { IClientCompatibilityService } from '../../application/interfaces/client-compatibility-service.js';
import { DomainTool } from '../models/domain-tool.js';
import { readFileSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

// MCP Protocol constants
const MCP_PROTOCOL_VERSION = '2024-11-05';
const MCP_SERVER_NAME = 'Unity Editor MCP Bridge';
const TOOLS_LIST_CHANGED_CAPABILITY = true;

/**
 * Get package version from package.json
 */
function getPackageVersion(): string {
  try {
    const __filename = fileURLToPath(import.meta.url);
    const __dirname = dirname(__filename);
    const packageJsonPath = join(__dirname, '..', '..', '..', 'package.json');
    const packageJson = JSON.parse(readFileSync(packageJsonPath, 'utf-8')) as { version?: string };
    return packageJson.version || '0.5.0';
  } catch (error) {
    // Fallback to hardcoded version if package.json read fails
    VibeLogger.logWarning(
      'package_version_read_error',
      'Failed to read package.json version, using fallback',
      { error: error instanceof Error ? error.message : 'Unknown error' },
      undefined,
      'Package version could not be dynamically loaded',
    );
    return '0.5.0';
  }
}

/**
 * UseCase for initializing the MCP server
 *
 * Responsibilities:
 * - Orchestrate the complete server initialization workflow
 * - Handle client compatibility detection and setup
 * - Manage Unity connection establishment
 * - Handle temporal cohesion of initialization process
 * - Support both sync and async initialization patterns
 *
 * Workflow:
 * 1. Setup client compatibility based on client name
 * 2. Determine initialization strategy (sync vs async)
 * 3. Initialize Unity connection and tools
 * 4. Return server capabilities and tool list
 */
export class InitializeServerUseCase
  implements UseCase<InitializeServerRequest, InitializeServerResponse>
{
  private connectionService: IConnectionService;
  private toolService: IToolQueryService;
  private toolManagementService: IToolManagementService;
  private clientCompatibilityService: IClientCompatibilityService;

  constructor(
    connectionService: IConnectionService,
    toolService: IToolQueryService,
    toolManagementService: IToolManagementService,
    clientCompatibilityService: IClientCompatibilityService,
  ) {
    this.connectionService = connectionService;
    this.toolService = toolService;
    this.toolManagementService = toolManagementService;
    this.clientCompatibilityService = clientCompatibilityService;
  }

  /**
   * Execute the server initialization workflow
   *
   * @param request Server initialization request
   * @returns Server initialization response
   */
  async execute(request: InitializeServerRequest): Promise<InitializeServerResponse> {
    const clientName = request.clientInfo?.name || '';
    const correlationId = VibeLogger.generateCorrelationId();

    VibeLogger.logInfo(
      'initialize_server_use_case_start',
      'Starting server initialization workflow',
      { client_name: clientName, has_client_info: !!request.clientInfo },
      correlationId,
      'UseCase orchestrating MCP server initialization with client compatibility detection',
    );

    try {
      // Step 1: Setup client compatibility
      this.setupClientCompatibility(clientName, correlationId);

      // Step 2: Determine initialization strategy and execute
      const response = await this.executeInitializationStrategy(clientName, correlationId);

      VibeLogger.logInfo(
        'initialize_server_use_case_success',
        'Server initialization workflow completed successfully',
        {
          client_name: clientName,
          tools_count: response.tools?.length || 0,
        },
        correlationId,
        'MCP server initialization completed - client connected and tools available',
      );

      return response;
    } catch (error) {
      return this.handleInitializationError(error, clientName, correlationId);
    }
  }

  /**
   * Setup client compatibility configuration
   *
   * @param clientName Client name for compatibility detection
   * @param correlationId Correlation ID for logging
   */
  private setupClientCompatibility(clientName: string, correlationId: string): void {
    if (clientName) {
      this.clientCompatibilityService.setClientName(clientName);
      this.clientCompatibilityService.logClientCompatibility(clientName);

      VibeLogger.logInfo(
        'initialize_server_client_compatibility_setup',
        'Client compatibility configuration completed',
        {
          client_name: clientName,
        },
        correlationId,
        'Client compatibility determined - unified initialization will be used',
      );
    }
  }

  /**
   * Execute initialization strategy based on client compatibility
   *
   * @param clientName Client name for strategy determination
   * @param correlationId Correlation ID for logging
   * @returns Server initialization response
   */
  private async executeInitializationStrategy(
    clientName: string,
    correlationId: string,
  ): Promise<InitializeServerResponse> {
    // All clients use synchronous initialization for consistency
    // This eliminates complexity from list_changed support detection
    return await this.executeSyncInitialization(clientName, correlationId);
  }

  /**
   * Execute synchronous initialization workflow (unified for all clients)
   *
   * @param clientName Client name
   * @param correlationId Correlation ID for logging
   * @returns Server initialization response with tools
   */
  private async executeSyncInitialization(
    clientName: string,
    correlationId: string,
  ): Promise<InitializeServerResponse> {
    VibeLogger.logInfo(
      'initialize_server_sync_strategy',
      'Using unified synchronous initialization for all clients',
      { client_name: clientName },
      correlationId,
      'Sync initialization - waiting for Unity connection before returning response',
    );

    try {
      // Initialize client compatibility
      await this.clientCompatibilityService.initializeClient(clientName);

      // Setup tool manager with client name
      this.toolManagementService.setClientName(clientName);

      // Wait for Unity connection with timeout
      await this.connectionService.ensureConnected(10000);

      // Get tools from Unity
      const tools = this.toolService.getAllTools();

      VibeLogger.logInfo(
        'initialize_server_sync_completed',
        'Unified synchronous initialization completed successfully',
        { client_name: clientName, tools_count: tools.length },
        correlationId,
        'Unity connection established and tools loaded for all clients',
      );

      return this.createSuccessResponse(tools);
    } catch (error) {
      VibeLogger.logError(
        'initialize_server_sync_unity_timeout',
        'Unity connection timeout during synchronous initialization',
        {
          client_name: clientName,
          error_message: error instanceof Error ? error.message : String(error),
        },
        correlationId,
        'Unity connection timed out - returning empty tools list for client',
      );

      // Return empty tools response for clients on timeout
      return this.createSuccessResponse([]);
    }
  }

  /**
   * Create success response with server info and capabilities
   *
   * @param tools Tools array to include in response
   * @returns Server initialization response
   */
  private createSuccessResponse(tools: DomainTool[]): InitializeServerResponse {
    return {
      protocolVersion: MCP_PROTOCOL_VERSION,
      capabilities: {
        tools: {
          listChanged: TOOLS_LIST_CHANGED_CAPABILITY,
        },
      },
      serverInfo: {
        name: MCP_SERVER_NAME,
        version: getPackageVersion(),
      },
      tools,
    };
  }

  /**
   * Handle initialization errors
   *
   * @param error Error that occurred
   * @param clientName Client name
   * @param correlationId Correlation ID for logging
   * @returns InitializeServerResponse with error state
   */
  private handleInitializationError(
    error: unknown,
    clientName: string,
    correlationId: string,
  ): InitializeServerResponse {
    const errorMessage = error instanceof Error ? error.message : 'Unknown error';

    VibeLogger.logError(
      'initialize_server_use_case_error',
      'Server initialization workflow failed',
      {
        client_name: clientName,
        error_message: errorMessage,
        error_type: error instanceof Error ? error.constructor.name : typeof error,
      },
      correlationId,
      'UseCase workflow failed - returning fallback response to prevent client errors',
    );

    // Return fallback response instead of throwing - server initialization should be resilient
    return this.createSuccessResponse([]);
  }
}
