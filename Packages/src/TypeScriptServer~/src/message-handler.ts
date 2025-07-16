import { JSONRPC } from './constants.js';
import { ContentLengthFramer } from './utils/content-length-framer.js';
import { DynamicBuffer } from './utils/dynamic-buffer.js';

// Constants for JSON-RPC error types
const JsonRpcErrorTypes = {
  SECURITY_BLOCKED: 'security_blocked',
  INTERNAL_ERROR: 'internal_error',
} as const;

// Type definitions for JSON-RPC messages
interface JsonRpcNotification {
  method: string;
  params?: unknown;
  jsonrpc?: string;
}

interface JsonRpcResponse {
  id: number;
  result?: unknown;
  error?: {
    message: string;
    data?: {
      command?: string;
      reason?: string;
      message?: string;
      type?: string;
    };
  };
  jsonrpc?: string;
}

// Type guard functions
const isJsonRpcNotification = (msg: unknown): msg is JsonRpcNotification => {
  return (
    typeof msg === 'object' &&
    msg !== null &&
    'method' in msg &&
    typeof (msg as JsonRpcNotification).method === 'string' &&
    !('id' in msg)
  );
};

const isJsonRpcResponse = (msg: unknown): msg is JsonRpcResponse => {
  return (
    typeof msg === 'object' &&
    msg !== null &&
    'id' in msg &&
    typeof (msg as JsonRpcResponse).id === 'number' &&
    !('method' in msg)
  );
};

const hasValidId = (msg: unknown): msg is { id: number } => {
  return (
    typeof msg === 'object' &&
    msg !== null &&
    'id' in msg &&
    typeof (msg as { id: number }).id === 'number'
  );
};

/**
 * Handles JSON-RPC message processing with Content-Length framing support
 * Follows Single Responsibility Principle - only handles message parsing and routing
 *
 * Design document reference: .kiro/specs/tcp-protocol-improvement/design.md
 * Architecture reference: Packages/src/TypeScriptServer~/ARCHITECTURE.md
 *
 * Related classes:
 * - UnityClient: Uses this class for JSON-RPC message handling
 * - UnityMcpServer: Indirectly uses via UnityClient for Unity communication
 * - ContentLengthFramer: Handles framing/parsing of Content-Length protocol
 * - DynamicBuffer: Manages buffering of incoming data fragments
 */
export class MessageHandler {
  private notificationHandlers: Map<string, (params: unknown) => void> = new Map();
  private pendingRequests: Map<
    number,
    { resolve: (value: unknown) => void; reject: (reason: unknown) => void }
  > = new Map();

  // Content-Length framing components
  private dynamicBuffer: DynamicBuffer = new DynamicBuffer();

  /**
   * Register notification handler for specific method
   */
  onNotification(method: string, handler: (params: unknown) => void): void {
    this.notificationHandlers.set(method, handler);
  }

  /**
   * Remove notification handler
   */
  offNotification(method: string): void {
    this.notificationHandlers.delete(method);
  }

  /**
   * Register a pending request
   */
  registerPendingRequest(
    id: number,
    resolve: (value: unknown) => void,
    reject: (reason: unknown) => void,
  ): void {
    this.pendingRequests.set(id, { resolve, reject });
  }

  /**
   * Handle incoming data from Unity using Content-Length framing
   */
  handleIncomingData(data: Buffer | string): void {
    try {
      // Debug: Received ${data instanceof Buffer ? data.length : data.length} bytes of data

      // Append new data to dynamic buffer (Buffer or string - DynamicBuffer handles both)
      this.dynamicBuffer.append(data);
      // Debug: Data appended to buffer successfully

      // Extract all complete frames
      const frames = this.dynamicBuffer.extractAllFrames();
      // Debug: Extracted ${frames.length} complete frames

      for (const frame of frames) {
        if (!frame || frame.trim() === '') {
          continue;
        }

        try {
          const message: unknown = JSON.parse(frame);

          // Check if this is a notification (no id field)
          if (isJsonRpcNotification(message)) {
            this.handleNotification(message);
          } else if (isJsonRpcResponse(message)) {
            // This is a response to a request
            this.handleResponse(message);
          } else if (hasValidId(message)) {
            // Fallback for other messages with valid id
            this.handleResponse(message as JsonRpcResponse);
          }
        } catch (parseError) {
          console.error('Error parsing JSON frame:', parseError);
          console.error('Problematic frame:', frame);
        }
      }
    } catch (error) {
      console.error('Error processing incoming data:', error);
    }
  }

  /**
   * Handle notification from Unity
   */
  private handleNotification(notification: JsonRpcNotification): void {
    const { method, params } = notification;

    const handler = this.notificationHandlers.get(method);
    if (handler) {
      try {
        handler(params);
      } catch (error) {
        console.error(`Error in notification handler for ${method}:`, error);
      }
    }
  }

  /**
   * Handle response from Unity
   */
  private handleResponse(response: JsonRpcResponse): void {
    const { id } = response;
    const pending = this.pendingRequests.get(id);

    if (pending) {
      this.pendingRequests.delete(id);

      if (response.error) {
        let errorMessage = response.error.message || 'Unknown error';

        // If security blocked, provide detailed information
        if (response.error.data?.type === JsonRpcErrorTypes.SECURITY_BLOCKED) {
          const data = response.error.data;
          errorMessage = `${data.reason || errorMessage}`;
          if (data.command) {
            errorMessage += ` (Command: ${data.command})`;
          }
          // Add instruction for enabling the feature
          errorMessage +=
            ' To use this feature, enable the corresponding option in Unity menu: Window > uLoopMCP > Security Settings';
        }

        pending.reject(new Error(errorMessage));
      } else {
        pending.resolve(response);
      }
    } else {
      console.error(`Received response for unknown request ID: ${id}`);
    }
  }

  /**
   * Clear all pending requests (used during disconnect)
   */
  clearPendingRequests(reason: string): void {
    for (const [, pending] of this.pendingRequests) {
      pending.reject(new Error(reason));
    }
    this.pendingRequests.clear();
  }

  /**
   * Create JSON-RPC request with Content-Length framing
   */
  createRequest(method: string, params: Record<string, unknown>, id: number): string {
    const request = {
      jsonrpc: JSONRPC.VERSION,
      id,
      method,
      params,
    };
    const jsonContent = JSON.stringify(request);
    return ContentLengthFramer.createFrame(jsonContent);
  }

  /**
   * Clear the dynamic buffer (for connection reset)
   */
  clearBuffer(): void {
    this.dynamicBuffer.clear();
  }

  /**
   * Get buffer statistics for debugging
   */
  getBufferStats(): { size: number; frames: number } {
    return this.dynamicBuffer.getStats();
  }
}
