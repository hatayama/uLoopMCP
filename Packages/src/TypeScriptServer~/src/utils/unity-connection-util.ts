/**
 * Unity Connection Utilities
 *
 * Provides reusable utility functions for Unity connection operations.
 * These utilities can be used by both ClientInitializationUseCase and ConnectionRecoveryUseCase
 * without creating circular dependencies.
 */

import { UNITY_CONNECTION } from '../constants.js';
import { VibeLogger } from './vibe-logger.js';
import { UnityClient } from '../unity-client.js';

export class UnityConnectionUtil {
  /**
   * Establish Unity connection with detailed logging
   */
  static async establishUnityConnection(
    unityClient: UnityClient,
    port: number,
    correlationId?: string,
    context: string = 'utility',
  ): Promise<boolean> {
    if (unityClient.connected) {
      VibeLogger.logInfo(
        'unity_connection_already_established',
        'Unity client already connected',
        {
          unity_connected: true,
          unity_endpoint: `${UNITY_CONNECTION.DEFAULT_HOST}:${port}`,
          context,
        },
        correlationId,
        'Connection already established',
      );
      return true;
    }

    VibeLogger.logInfo(
      'unity_connection_establishing',
      'Establishing Unity connection',
      {
        unity_endpoint: `${UNITY_CONNECTION.DEFAULT_HOST}:${port}`,
        context,
      },
      correlationId,
      'Calling unityClient.connect()',
    );

    try {
      await unityClient.connect('ClientInitializationUseCase');

      VibeLogger.logInfo(
        'unity_connection_established',
        'Unity connection established successfully',
        {
          unity_connected: unityClient.connected,
          unity_endpoint: `${UNITY_CONNECTION.DEFAULT_HOST}:${port}`,
          context,
        },
        correlationId,
        'Connection established - unityClient.connected should now be true',
      );

      return unityClient.connected;
    } catch (error) {
      VibeLogger.logError(
        'unity_connection_failed',
        'Failed to establish Unity connection',
        {
          error_message: error instanceof Error ? error.message : JSON.stringify(error),
          unity_endpoint: `${UNITY_CONNECTION.DEFAULT_HOST}:${port}`,
          context,
        },
        correlationId,
        'Connection failed',
      );
      return false;
    }
  }

  /**
   * Discover and connect to Unity in one operation
   * This is the main utility for initial connections
   */
  static async discoverAndConnect(
    unityClient: UnityClient,
    port: number,
    correlationId?: string,
    context: string = 'discover_and_connect',
  ): Promise<{ success: boolean; port?: number }> {
    VibeLogger.logInfo(
      'unity_discover_and_connect_start',
      'Starting Unity discovery and connection',
      {
        target_port: port,
        unity_endpoint: `${UNITY_CONNECTION.DEFAULT_HOST}:${port}`,
        context,
      },
      correlationId,
      'Single operation: discover + connect',
    );

    // Direct connection approach - no separate availability check needed
    // Connection attempt will handle both discovery and connection establishment

    // Step 2: Establish connection
    const connected = await this.establishUnityConnection(
      unityClient,
      port,
      correlationId,
      context,
    );

    if (connected) {
      VibeLogger.logInfo(
        'unity_discover_and_connect_success',
        'Unity discovery and connection completed successfully',
        {
          connected_port: port,
          unity_endpoint: `${UNITY_CONNECTION.DEFAULT_HOST}:${port}`,
          context,
        },
        correlationId,
        'Single operation completed successfully',
      );
      return { success: true, port };
    } else {
      VibeLogger.logError(
        'unity_discover_and_connect_failed',
        'Unity connection failed - server not available or connection refused',
        {
          target_port: port,
          unity_endpoint: `${UNITY_CONNECTION.DEFAULT_HOST}:${port}`,
          context,
        },
        correlationId,
        'Direct connection approach failed - Unity server may not be running',
      );
      return { success: false };
    }
  }
}
