/**
 * Request Models for UseCase Layer
 *
 * Design document reference:
 * - .kiro/specs/typescript-server-ddd-refactoring/design.md#データモデル
 *
 * Related classes:
 * - UseCase implementations use these as TRequest type parameters
 * - Response models defined in responses.ts
 */

/**
 * Request for tool execution UseCase
 */
export interface ExecuteToolRequest {
  toolName: string;
  arguments: Record<string, unknown>;
}

/**
 * Request for tools refresh UseCase
 */
export interface RefreshToolsRequest {
  includeDevelopmentOnly?: boolean;
}

/**
 * Request for server initialization UseCase
 */
export interface InitializeServerRequest {
  clientInfo?: {
    name: string;
    version?: string;
  };
}

/**
 * Request for connection handling UseCase
 */
export interface HandleConnectionRequest {
  port?: number;
  timeout?: number;
}

/**
 * Request for notification processing UseCase
 */
export interface ProcessNotificationRequest {
  method: string;
  params: unknown;
}
