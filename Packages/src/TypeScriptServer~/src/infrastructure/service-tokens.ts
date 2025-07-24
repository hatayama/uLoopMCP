/**
 * サービストークン定義
 * 
 * 設計ドキュメント参照:
 * - .kiro/specs/typescript-server-ddd-refactoring/design.md#サービストークン定義
 * 
 * 関連クラス:
 * - ServiceLocator（infrastructure/service-locator.ts）
 * - 各ApplicationService実装クラス
 * - 各UseCase実装クラス
 */

/**
 * サービストークン定数
 * 
 * 責任:
 * - 依存性注入時の型安全なトークン提供
 * - サービス識別子の一元管理
 * - 文字列リテラルによるタイプミス防止
 */
export const ServiceTokens = {
  // ApplicationService層のトークン
  CONNECTION_APP_SERVICE: 'ConnectionAppService',
  TOOL_MANAGEMENT_APP_SERVICE: 'ToolManagementAppService', 
  EVENT_HANDLING_APP_SERVICE: 'EventHandlingAppService',
  DISCOVERY_APP_SERVICE: 'DiscoveryAppService',
  CLIENT_COMPATIBILITY_APP_SERVICE: 'ClientCompatibilityAppService',
  MESSAGE_APP_SERVICE: 'MessageAppService',

  // UseCase層のトークン（毎回新しいインスタンスを生成するためファクトリーを使用）
  INITIALIZE_SERVER_USE_CASE: 'InitializeServerUseCase',
  EXECUTE_TOOL_USE_CASE: 'ExecuteToolUseCase',
  REFRESH_TOOLS_USE_CASE: 'RefreshToolsUseCase',
  HANDLE_CONNECTION_LOST_USE_CASE: 'HandleConnectionLostUseCase',
  PROCESS_NOTIFICATION_USE_CASE: 'ProcessNotificationUseCase',

  // Infrastructure層のトークン
  UNITY_CLIENT: 'UnityClient',
  VIBE_LOGGER: 'VibeLogger',
  MESSAGE_HANDLER: 'MessageHandler',
  UNITY_CONNECTION_MANAGER: 'UnityConnectionManager',
  UNITY_TOOL_MANAGER: 'UnityToolManager',
  UNITY_DISCOVERY: 'UnityDiscovery',
  UNITY_EVENT_HANDLER: 'UnityEventHandler',
  MCP_CLIENT_COMPATIBILITY: 'McpClientCompatibility',
} as const;

/**
 * サービストークンの型定義
 * 
 * コンパイル時の型チェックを提供
 */
export type ServiceToken = typeof ServiceTokens[keyof typeof ServiceTokens];