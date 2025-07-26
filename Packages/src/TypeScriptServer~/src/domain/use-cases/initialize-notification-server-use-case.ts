/**
 * Initialize Notification Server UseCase
 *
 * Design document reference:
 * - /working-notes/SOW_Push_Notification_HTTP_Server.md
 *
 * Related classes:
 * - NotificationReceiveServer (notification-receive-server.ts)
 * - UnityClient (unity-client.ts)
 * - VibeLogger (utils/vibe-logger.ts)
 */

import { UseCase } from '../base-interfaces.js';
import { VibeLogger } from '../../utils/vibe-logger.js';
import { NotificationReceiveServer } from '../../notification-receive-server.js';
import { UnityClient } from '../../unity-client.js';

/**
 * Request for notification server initialization
 */
export interface InitializeNotificationServerRequest {
  clientName: string;
  unityEndpoint?: string;
}

/**
 * Response from notification server initialization
 */
export interface InitializeNotificationServerResponse {
  success: boolean;
  notificationPort: number;
  error?: string;
}

/**
 * UseCase for initializing Push Notification Server before Unity connection
 *
 * Responsibilities:
 * - Start notification receive server on dynamic port
 * - Register notification port with Unity
 * - Ensure proper initialization order (notification server → Unity connection)
 * - Handle temporal cohesion of Push notification setup
 *
 * Workflow:
 * 1. Start NotificationReceiveServer on dynamic port
 * 2. Wait for server startup completion
 * 3. Register notification port with Unity via UnityClient
 * 4. Return notification port for further processing
 *
 * This UseCase ensures that Push notification infrastructure is ready
 * BEFORE Unity connection begins, preventing NotificationPort: 0 registrations
 */
export class InitializeNotificationServerUseCase
  implements UseCase<InitializeNotificationServerRequest, InitializeNotificationServerResponse>
{
  private notificationReceiveServer: NotificationReceiveServer;
  private unityClient: UnityClient;

  constructor(notificationReceiveServer: NotificationReceiveServer, unityClient: UnityClient) {
    this.notificationReceiveServer = notificationReceiveServer;
    this.unityClient = unityClient;
  }

  /**
   * Execute notification server initialization workflow
   *
   * @param request Notification server initialization request
   * @returns Notification server initialization response
   */
  async execute(
    request: InitializeNotificationServerRequest,
  ): Promise<InitializeNotificationServerResponse> {
    const correlationId = VibeLogger.generateCorrelationId();

    VibeLogger.logInfo(
      'initialize_notification_server_use_case_start',
      'Starting notification server initialization workflow',
      {
        client_name: request.clientName,
        unity_endpoint: request.unityEndpoint,
      },
      correlationId,
      'UseCase orchestrating Push notification server setup before Unity connection',
    );

    try {
      // Step 1: Start notification receive server
      const notificationPort = await this.startNotificationServer(correlationId);

      // Step 2: Store notification port in UnityClient for later use
      this.unityClient.setNotificationPort(notificationPort);

      VibeLogger.logInfo(
        'initialize_notification_server_use_case_success',
        'Notification server initialization workflow completed successfully',
        {
          client_name: request.clientName,
          notification_port: notificationPort,
        },
        correlationId,
        'Push notification infrastructure ready - Unity connection can proceed',
      );

      return {
        success: true,
        notificationPort,
      };
    } catch (error) {
      return this.handleInitializationError(error, request.clientName, correlationId);
    }
  }

  /**
   * Start notification receive server on dynamic port
   *
   * @param correlationId Correlation ID for logging
   * @returns Assigned notification port
   */
  private async startNotificationServer(correlationId: string): Promise<number> {
    VibeLogger.logInfo(
      'notification_server_starting',
      'Starting notification receive server on dynamic port',
      {},
      correlationId,
      'HTTP server will listen for domain reload notifications from Unity',
    );

    const notificationPort = await this.notificationReceiveServer.start();

    VibeLogger.logInfo(
      'notification_server_started',
      'Notification receive server started successfully',
      { notification_port: notificationPort },
      correlationId,
      'Push notification infrastructure ready for domain reload notifications',
    );

    return notificationPort;
  }

  /**
   * Handle initialization errors
   *
   * @param error Error that occurred
   * @param clientName Client name
   * @param correlationId Correlation ID for logging
   * @returns InitializeNotificationServerResponse with error state
   */
  private handleInitializationError(
    error: unknown,
    clientName: string,
    correlationId: string,
  ): InitializeNotificationServerResponse {
    const errorMessage = error instanceof Error ? error.message : 'Unknown error';

    VibeLogger.logError(
      'initialize_notification_server_use_case_error',
      'Notification server initialization workflow failed',
      {
        client_name: clientName,
        error_message: errorMessage,
        error_type: error instanceof Error ? error.constructor.name : typeof error,
      },
      correlationId,
      'UseCase workflow failed - Push notification features may not be available',
    );

    return {
      success: false,
      notificationPort: 0,
      error: errorMessage,
    };
  }
}
