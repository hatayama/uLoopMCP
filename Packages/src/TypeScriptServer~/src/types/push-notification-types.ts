/**
 * Push通知メッセージ形式定義
 * 設計書参照: /.kiro/specs/unity-push-notification-system/design.md
 * 関連クラス: UnityPushClient.cs, UnityPushNotificationReceiveServer.ts
 *
 * JSON-RPC 2.0準拠のメッセージフォーマット定義
 */

export type PushNotificationType =
  | 'CONNECTION_ESTABLISHED'
  | 'DOMAIN_RELOAD'
  | 'DOMAIN_RELOAD_RECOVERED'
  | 'USER_DISCONNECT'
  | 'UNITY_SHUTDOWN'
  | 'TOOLS_CHANGED';

export type DisconnectReasonType = 'USER_DISCONNECT' | 'UNITY_SHUTDOWN' | 'DOMAIN_RELOAD';

export interface PushNotification {
  type: PushNotificationType;
  timestamp: string;
  payload?: NotificationPayload;
}

export interface NotificationPayload {
  reason?: DisconnectReason;
  endpoint?: string;
  clientInfo?: ClientInfo;
  toolsInfo?: ToolsInfo;
}

export interface DisconnectReason {
  type: DisconnectReasonType;
  message: string;
}

export interface ClientInfo {
  name: string;
  version: string;
}

export interface UnityEditorInfo {
  unityVersion?: string;
  projectPath?: string;
  sessionId?: string;
}

export interface ToolsInfo {
  toolCount?: number;
  changedTools?: string[];
  changeType?: 'added' | 'removed' | 'modified';
}

export interface ServerEndpoint {
  host: string;
  port: number;
  protocol: 'tcp';
}

export interface UnityConnection {
  socket: unknown; // net.Socket in Node.js
  clientId: string;
  connectedAt: Date;
  lastPingAt?: Date;
}

export interface PushNotificationEvent {
  clientId: string;
  notification: PushNotification;
}

export interface ConnectionEvent {
  clientId: string;
  endpoint?: ServerEndpoint;
  reason?: DisconnectReason;
}

// JSON-RPC 2.0準拠の通知メッセージ
export interface JsonRpcNotification {
  jsonrpc: '2.0';
  method: string;
  params: PushNotification;
}

// Unity Push通知の定数
export const PushNotificationConstants = {
  PROTOCOL_VERSION: '2.0',
  METHODS: {
    PUSH_NOTIFICATION: 'notifications/push',
    TOOLS_LIST_CHANGED: 'notifications/tools/list_changed',
  },
  TYPES: {
    CONNECTION_ESTABLISHED: 'CONNECTION_ESTABLISHED' as const,
    DOMAIN_RELOAD: 'DOMAIN_RELOAD' as const,
    DOMAIN_RELOAD_RECOVERED: 'DOMAIN_RELOAD_RECOVERED' as const,
    USER_DISCONNECT: 'USER_DISCONNECT' as const,
    UNITY_SHUTDOWN: 'UNITY_SHUTDOWN' as const,
    TOOLS_CHANGED: 'TOOLS_CHANGED' as const,
  },
  DISCONNECT_REASONS: {
    USER_DISCONNECT: 'USER_DISCONNECT' as const,
    UNITY_SHUTDOWN: 'UNITY_SHUTDOWN' as const,
    DOMAIN_RELOAD: 'DOMAIN_RELOAD' as const,
  },
} as const;

// Push通知メッセージのバリデーション
export function validatePushNotification(notification: unknown): notification is PushNotification {
  if (!notification || typeof notification !== 'object') {
    return false;
  }

  const validTypes = Object.values(PushNotificationConstants.TYPES);
  const notificationType = (notification as { type?: string }).type;
  if (!notificationType || !validTypes.includes(notificationType as PushNotificationType)) {
    return false;
  }

  if (
    !(notification as { timestamp?: string }).timestamp ||
    typeof (notification as { timestamp?: string }).timestamp !== 'string'
  ) {
    return false;
  }

  return true;
}

// Push通知メッセージの作成ヘルパー
export function createPushNotification(
  type: PushNotificationType,
  payload?: NotificationPayload,
): PushNotification {
  return {
    type,
    timestamp: new Date().toISOString(),
    payload,
  };
}

// JSON-RPC通知メッセージの作成
export function createJsonRpcNotification(
  method: string,
  params: PushNotification,
): JsonRpcNotification {
  return {
    jsonrpc: PushNotificationConstants.PROTOCOL_VERSION,
    method,
    params,
  };
}

// 切断理由メッセージの作成
export function createDisconnectReason(
  type: DisconnectReasonType,
  message: string,
): DisconnectReason {
  return {
    type,
    message,
  };
}
