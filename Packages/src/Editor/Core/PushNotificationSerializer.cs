/**
 * Push通知メッセージシリアライゼーション
 * 設計書参照: /.kiro/specs/unity-push-notification-system/design.md
 * 関連クラス: push-notification-types.ts, UnityPushClient.cs
 * 
 * JSON-RPC 2.0準拠のメッセージフォーマット処理
 */

using System;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    public static class PushNotificationSerializer
    {
        public static string SerializePushNotification(PushNotification notification)
        {
            return JsonUtility.ToJson(notification);
        }

        public static PushNotification DeserializePushNotification(string json)
        {
            return JsonUtility.FromJson<PushNotification>(json);
        }

        public static JsonRpcNotification CreateJsonRpcNotification(string method, PushNotification parameters)
        {
            return new JsonRpcNotification
            {
                jsonrpc = PushNotificationConstants.PROTOCOL_VERSION,
                method = method,
                parameters = parameters
            };
        }

        public static PushNotification CreateConnectionEstablishedNotification(string endpoint)
        {
            return new PushNotification
            {
                type = PushNotificationConstants.CONNECTION_ESTABLISHED,
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                payload = new NotificationPayload
                {
                    endpoint = endpoint,
                    clientInfo = GetCurrentClientInfo()
                }
            };
        }

        public static PushNotification CreateDomainReloadNotification()
        {
            return new PushNotification
            {
                type = PushNotificationConstants.DOMAIN_RELOAD,
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                payload = new NotificationPayload
                {
                    reason = new DisconnectReason
                    {
                        type = PushNotificationConstants.DOMAIN_RELOAD,
                        message = "Unity domain reload initiated"
                    }
                }
            };
        }

        public static PushNotification CreateDomainReloadRecoveredNotification()
        {
            return new PushNotification
            {
                type = PushNotificationConstants.DOMAIN_RELOAD_RECOVERED,
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                payload = new NotificationPayload
                {
                    clientInfo = GetCurrentClientInfo()
                }
            };
        }

        public static PushNotification CreateDisconnectNotification(string reasonType, string message)
        {
            return new PushNotification
            {
                type = reasonType,
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                payload = new NotificationPayload
                {
                    reason = new DisconnectReason
                    {
                        type = reasonType,
                        message = message
                    }
                }
            };
        }

        public static PushNotification CreateToolsChangedNotification(int toolCount, string[] changedTools, string changeType)
        {
            return new PushNotification
            {
                type = PushNotificationConstants.TOOLS_CHANGED,
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                payload = new NotificationPayload
                {
                    toolsInfo = new ToolsInfo
                    {
                        toolCount = toolCount,
                        changedTools = changedTools,
                        changeType = changeType
                    }
                }
            };
        }

        private static ClientInfo GetCurrentClientInfo()
        {
            return new ClientInfo
            {
                unityVersion = Application.unityVersion,
                projectPath = Application.dataPath,
                sessionId = Guid.NewGuid().ToString()
            };
        }

        public static bool ValidatePushNotification(string json, out PushNotification notification)
        {
            notification = null;

            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            try
            {
                notification = DeserializePushNotification(json);
                return notification != null && IsValidNotificationType(notification.type);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsValidNotificationType(string type)
        {
            return type == PushNotificationConstants.CONNECTION_ESTABLISHED ||
                   type == PushNotificationConstants.DOMAIN_RELOAD ||
                   type == PushNotificationConstants.DOMAIN_RELOAD_RECOVERED ||
                   type == PushNotificationConstants.USER_DISCONNECT ||
                   type == PushNotificationConstants.UNITY_SHUTDOWN ||
                   type == PushNotificationConstants.TOOLS_CHANGED;
        }
    }

    [Serializable]
    public class JsonRpcNotification
    {
        public string jsonrpc = PushNotificationConstants.PROTOCOL_VERSION;
        public string method;
        public PushNotification parameters;
    }

    [Serializable]
    public class ToolsInfo
    {
        public int toolCount;
        public string[] changedTools;
        public string changeType; // "added", "removed", "modified"
    }

    public static class PushNotificationConstants
    {
        public const string PROTOCOL_VERSION = "2.0";

        // Notification types
        public const string CONNECTION_ESTABLISHED = "CONNECTION_ESTABLISHED";
        public const string DOMAIN_RELOAD = "DOMAIN_RELOAD";
        public const string DOMAIN_RELOAD_RECOVERED = "DOMAIN_RELOAD_RECOVERED";
        public const string USER_DISCONNECT = "USER_DISCONNECT";
        public const string UNITY_SHUTDOWN = "UNITY_SHUTDOWN";
        public const string TOOLS_CHANGED = "TOOLS_CHANGED";

        // JSON-RPC methods
        public const string METHOD_PUSH_NOTIFICATION = "notifications/push";
        public const string METHOD_TOOLS_LIST_CHANGED = "notifications/tools/list_changed";

        // Change types
        public const string CHANGE_TYPE_ADDED = "added";
        public const string CHANGE_TYPE_REMOVED = "removed";
        public const string CHANGE_TYPE_MODIFIED = "modified";
    }
}