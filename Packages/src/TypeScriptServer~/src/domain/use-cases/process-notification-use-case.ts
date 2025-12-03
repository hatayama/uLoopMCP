/**
 * Process Notification UseCase
 *
 * Design document reference:
 * - /Packages/docs/ARCHITECTURE_TypeScript.md#ProcessNotificationUseCase
 *
 * Related classes:
 * - INotificationService (application/interfaces/notification-service.ts)
 * - ServiceLocator (infrastructure/service-locator.ts)
 */

import { UseCase } from '../base-interfaces.js';
import { ProcessNotificationRequest } from '../models/requests.js';
import { ProcessNotificationResponse } from '../models/responses.js';
import { VibeLogger } from '../../utils/vibe-logger.js';
import { INotificationService } from '../../application/interfaces/notification-service.js';
import { NOTIFICATION_METHODS } from '../../constants.js';

/**
 * UseCase for processing and sending MCP notifications
 *
 * Responsibilities:
 * - Orchestrate the complete notification sending workflow
 * - Handle different types of MCP notifications
 * - Manage temporal cohesion of notification process
 * - Provide duplicate notification prevention
 * - Support various notification methods (tools/list_changed, etc.)
 *
 * Workflow:
 * 1. Validate notification request and method
 * 2. Check for duplicate notification prevention
 * 3. Send notification via MCP server
 * 4. Handle notification errors gracefully
 * 5. Return notification status
 */
export class ProcessNotificationUseCase
  implements UseCase<ProcessNotificationRequest, ProcessNotificationResponse>
{
  private notificationService: INotificationService;
  private isNotifying: boolean = false;

  constructor(notificationService: INotificationService) {
    this.notificationService = notificationService;
  }

  /**
   * Execute the notification processing workflow
   *
   * @param request Notification processing request
   * @returns Notification processing response
   */
  // eslint-disable-next-line @typescript-eslint/require-await
  async execute(request: ProcessNotificationRequest): Promise<ProcessNotificationResponse> {
    const correlationId = VibeLogger.generateCorrelationId();

    VibeLogger.logInfo(
      'process_notification_use_case_start',
      'Starting notification processing workflow',
      {
        method: request.method,
        has_params: !!request.params,
        is_currently_notifying: this.isNotifying,
      },
      correlationId,
      'UseCase orchestrating MCP notification sending with duplicate prevention',
    );

    try {
      // Step 1: Validate notification method
      this.validateNotificationMethod(request.method, correlationId);

      // Step 2: Check duplicate notification prevention
      if (this.shouldSkipNotification(request.method, correlationId)) {
        return {
          processed: false,
          notificationsSent: [],
        };
      }

      // Step 3: Send notification via MCP server
      const notificationResult = this.sendNotification(request, correlationId);

      // Step 4: Return notification status
      VibeLogger.logInfo(
        'process_notification_use_case_success',
        'Notification processing workflow completed successfully',
        {
          method: request.method,
          processed: notificationResult.processed,
          notifications_sent: notificationResult.notificationsSent,
        },
        correlationId,
        'MCP notification sent successfully to client',
      );

      return notificationResult;
    } catch (error) {
      return this.handleNotificationError(error, request, correlationId);
    }
  }

  /**
   * Validate notification method is supported
   *
   * @param method Notification method to validate
   * @param correlationId Correlation ID for logging
   */
  private validateNotificationMethod(method: string, correlationId: string): void {
    const supportedMethods = Object.values(NOTIFICATION_METHODS) as string[];

    if (!supportedMethods.includes(method)) {
      VibeLogger.logWarning(
        'notification_method_unsupported',
        'Unsupported notification method requested',
        {
          requested_method: method,
          supported_methods: supportedMethods,
        },
        correlationId,
        'Notification method not supported - processing will continue but may not work as expected',
      );
    } else {
      VibeLogger.logDebug(
        'notification_method_validated',
        'Notification method validated successfully',
        { method },
        correlationId,
        'Supported notification method confirmed',
      );
    }
  }

  /**
   * Check if notification should be skipped due to duplicate prevention
   *
   * @param method Notification method
   * @param correlationId Correlation ID for logging
   * @returns True if notification should be skipped
   */
  private shouldSkipNotification(method: string, correlationId: string): boolean {
    // Only apply duplicate prevention for tools/list_changed notifications
    if (method === NOTIFICATION_METHODS.TOOLS_LIST_CHANGED && this.isNotifying) {
      VibeLogger.logDebug(
        'notification_duplicate_prevented',
        'Duplicate notification prevented',
        {
          method,
          is_notifying: this.isNotifying,
        },
        correlationId,
        'Duplicate tools/list_changed notification prevented',
      );
      return true;
    }

    return false;
  }

  /**
   * Send notification via MCP server
   *
   * @param request Notification request
   * @param correlationId Correlation ID for logging
   * @returns Notification processing response
   */
  private sendNotification(
    request: ProcessNotificationRequest,
    correlationId: string,
  ): ProcessNotificationResponse {
    const { method, params } = request;

    // Set notifying flag for duplicate prevention
    this.isNotifying = true;

    try {
      VibeLogger.logInfo(
        'notification_sending',
        'Sending MCP notification to client',
        {
          method,
          params: params || {},
        },
        correlationId,
        'Sending notification via MCP server protocol',
      );

      // Send appropriate notification based on method type
      switch (request.method) {
        case NOTIFICATION_METHODS.TOOLS_LIST_CHANGED:
          this.notificationService.sendToolsChangedNotification();
          break;
        default:
          VibeLogger.logWarning(
            'process_notification_unsupported_method',
            'Unsupported notification method requested',
            { method: request.method },
            correlationId,
            'Notification method not implemented - skipping notification',
          );
          break;
      }

      VibeLogger.logInfo(
        'notification_sent_success',
        'MCP notification sent successfully',
        {
          method,
          timestamp: new Date().toISOString(),
        },
        correlationId,
        'Notification delivered to MCP client successfully',
      );

      return {
        processed: true,
        notificationsSent: [method],
      };
    } catch (error) {
      VibeLogger.logError(
        'notification_send_failed',
        'Failed to send MCP notification',
        {
          method,
          error_message: error instanceof Error ? error.message : String(error),
        },
        correlationId,
        'MCP notification sending failed - client may not receive update',
      );

      // Return partial success - notification was attempted
      return {
        processed: false,
        notificationsSent: [],
      };
    } finally {
      // Always reset notifying flag
      this.isNotifying = false;
    }
  }

  /**
   * Handle notification processing errors
   *
   * @param error Error that occurred
   * @param request Original request
   * @param correlationId Correlation ID for logging
   * @returns ProcessNotificationResponse with error state
   */
  private handleNotificationError(
    error: unknown,
    request: ProcessNotificationRequest,
    correlationId: string,
  ): ProcessNotificationResponse {
    const errorMessage = error instanceof Error ? error.message : 'Unknown error';

    VibeLogger.logError(
      'process_notification_use_case_error',
      'Notification processing workflow failed',
      {
        method: request.method,
        error_message: errorMessage,
        error_type: error instanceof Error ? error.constructor.name : typeof error,
      },
      correlationId,
      'UseCase workflow failed - returning failed notification status',
    );

    // Reset notifying flag in case of error
    this.isNotifying = false;

    // Return failed response - notification processing should be resilient
    return {
      processed: false,
      notificationsSent: [],
    };
  }

  /**
   * Reset notification state (for testing and edge cases)
   */
  resetNotificationState(): void {
    this.isNotifying = false;
  }

  /**
   * Get current notification state (for monitoring)
   */
  getNotificationState(): { isNotifying: boolean } {
    return {
      isNotifying: this.isNotifying,
    };
  }
}
