/**
 * Push通知エラーハンドリング
 * 設計書参照: /.kiro/specs/unity-push-notification-system/design.md
 * 関連クラス: UnityPushClient.cs, UnityPushConnectionManager.cs
 * 
 * 責任:
 * - 接続タイムアウト処理
 * - ネットワークエラー時の再試行メカニズム
 * - ポート競合時の代替ポート選択機能
 * - Unity Editorクラッシュ検出と復旧処理
 * - TypeScriptサーバークラッシュ時の無効エンドポイントクリア機能
 */

using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    public static class PushNotificationErrorHandler
    {
        private const int MAX_RETRY_ATTEMPTS = 3;
        private const int RETRY_DELAY_MS = 2000;

        public static async Task<bool> HandleConnectionFailureAsync(UnityPushClient pushClient, Exception error)
        {
            Debug.LogError($"[uLoopMCP] Push client connection failed: {error.Message}");

            if (IsTimeoutError(error))
            {
                return await HandleTimeoutErrorAsync(pushClient);
            }

            if (IsNetworkError(error))
            {
                return await HandleNetworkErrorAsync(pushClient);
            }

            return false;
        }

        public static async Task HandleUnityEditorCrashDetectionAsync(UnityPushClient pushClient)
        {
            Debug.Log("[uLoopMCP] Unity Editor crash detected - clearing invalid endpoint data");

            McpSessionManager sessionManager = McpSessionManager.instance;
            if (sessionManager != null)
            {
                sessionManager.ClearPushServerEndpoint();
                sessionManager.SetPushServerConnected(false);
            }

            await AttemptReconnectionAfterCrashAsync(pushClient);
        }

        public static void HandleTypeScriptServerCrash()
        {
            Debug.Log("[uLoopMCP] TypeScript server crash detected - clearing invalid endpoint data");

            McpSessionManager sessionManager = McpSessionManager.instance;
            if (sessionManager != null)
            {
                sessionManager.ClearPushServerEndpoint();
                sessionManager.SetPushServerConnected(false);
            }
        }

        private static async Task<bool> HandleTimeoutErrorAsync(UnityPushClient pushClient)
        {
            Debug.LogWarning("[uLoopMCP] Connection timeout - attempting retry with exponential backoff");

            for (int attempt = 1; attempt <= MAX_RETRY_ATTEMPTS; attempt++)
            {
                int delay = Math.Min(RETRY_DELAY_MS * (int)Math.Pow(2, attempt - 1), 30000); // Cap at 30s
                await Task.Delay(delay);

                bool success = await pushClient.DiscoverAndConnectAsync();
                if (success)
                {
                    Debug.Log($"[uLoopMCP] Connection successful on retry attempt {attempt}");
                    return true;
                }

                Debug.LogWarning($"[uLoopMCP] Retry attempt {attempt} failed, waited {delay}ms");
            }

            Debug.LogError("[uLoopMCP] All retry attempts failed for timeout error");
            return false;
        }

        private static async Task<bool> HandleNetworkErrorAsync(UnityPushClient pushClient)
        {
            Debug.LogWarning("[uLoopMCP] Network error detected - attempting reconnection");
            return await pushClient.DiscoverAndConnectAsync();
        }


        private static async Task AttemptReconnectionAfterCrashAsync(UnityPushClient pushClient)
        {
            await Task.Delay(ConnectionTimeouts.RECONNECTION_DELAY_MS);

            Debug.Log("[uLoopMCP] Attempting reconnection after Unity Editor crash");

            for (int attempt = 1; attempt <= MAX_RETRY_ATTEMPTS; attempt++)
            {
                bool success = await pushClient.DiscoverAndConnectAsync();
                if (success)
                {
                    Debug.Log($"[uLoopMCP] Reconnection successful after crash on attempt {attempt}");
                    return;
                }

                int delay = Math.Min(RETRY_DELAY_MS * (int)Math.Pow(2, attempt - 1), 30000); // Cap at 30s
                await Task.Delay(delay);
            }

            Debug.LogWarning("[uLoopMCP] Failed to reconnect after Unity Editor crash");
        }

        private static bool IsTimeoutError(Exception error)
        {
            return error is TimeoutException ||
                   (error.Message?.Contains("timeout") == true) ||
                   (error.Message?.Contains("timed out") == true);
        }

        private static bool IsNetworkError(Exception error)
        {
            return error is System.Net.Sockets.SocketException ||
                   error is WebException ||
                   (error.Message?.Contains("network") == true) ||
                   (error.Message?.Contains("connection refused") == true);
        }




        public static void LogConnectionStatistics(UnityPushClient pushClient)
        {
            if (pushClient == null) return;

            McpSessionManager sessionManager = McpSessionManager.instance;
            if (sessionManager == null) return;

            DateTime lastConnectionTime = sessionManager.GetLastConnectionTime();
            bool isConnected = sessionManager.IsPushServerConnected();
            string endpoint = sessionManager.GetPushServerEndpoint();

            Debug.Log($"[uLoopMCP] Connection Statistics:");
            Debug.Log($"  - Connected: {isConnected}");
            Debug.Log($"  - Endpoint: {endpoint ?? "None"}");
            Debug.Log($"  - Last Connection: {lastConnectionTime}");
            Debug.Log($"  - Connection Age: {DateTime.Now - lastConnectionTime}");
        }
    }

    public static class ConnectionTimeouts
    {
        public const int CONNECTION_TIMEOUT_MS = 5000;      // 5秒
        public const int PUSH_NOTIFICATION_TIMEOUT_MS = 2000; // 2秒
        public const int DISCOVERY_TIMEOUT_MS = 10000;     // 10秒
        public const int RECONNECTION_DELAY_MS = 3000;     // 3秒
    }
}