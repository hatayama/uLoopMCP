/**
 * Response Models for UseCase Layer
 *
 * Design document reference:
 * - .kiro/specs/typescript-server-ddd-refactoring/design.md#データモデル
 *
 * Related classes:
 * - UseCase implementations use these as TResponse type parameters
 * - Request models defined in requests.ts
 */

import { DomainTool } from './domain-tool.js';

/**
 * Response for tool execution UseCase
 */
export interface ExecuteToolResponse {
  content: Array<{
    type: string;
    text: string;
  }>;
  isError?: boolean;
}

/**
 * Response for tools refresh UseCase
 */
export interface RefreshToolsResponse {
  tools: DomainTool[];
  refreshedAt: string;
}

/**
 * Response for server initialization UseCase
 */
export interface InitializeServerResponse {
  protocolVersion: string;
  capabilities: object;
  serverInfo: object;
  tools?: DomainTool[];
}

/**
 * Response for connection handling UseCase
 */
export interface HandleConnectionResponse {
  connected: boolean;
  port: number;
  connectionTime: string;
}

/**
 * Response for notification processing UseCase
 */
export interface ProcessNotificationResponse {
  processed: boolean;
  notificationsSent?: string[];
}
