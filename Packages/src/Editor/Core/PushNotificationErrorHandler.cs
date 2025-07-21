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
        private const int CONNECTION_TIMEOUT_MS = 5000;
        private const int PUSH_NOTIFICATION_TIMEOUT_MS = 2000;
        private const int DISCOVERY_TIMEOUT_MS = 10000;
        private const int RECONNECTION_DELAY_MS = 3000;

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

            if (IsPortConflictError(error))
            {
                return await HandlePortConflictAsync(pushClient);
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
            Debug.LogWarning("[uLoopMCP] Connection timeout - attempting retry with increased timeout");

            for (int attempt = 1; attempt <= MAX_RETRY_ATTEMPTS; attempt++)
            {
                await Task.Delay(RETRY_DELAY_MS * attempt);

                bool success = await pushClient.DiscoverAndConnectAsync();
                if (success)
                {
                    Debug.Log($"[uLoopMCP] Connection successful on retry attempt {attempt}");
                    return true;
                }

                Debug.LogWarning($"[uLoopMCP] Retry attempt {attempt} failed");
            }

            Debug.LogError("[uLoopMCP] All retry attempts failed for timeout error");
            return false;
        }

        private static async Task<bool> HandleNetworkErrorAsync(UnityPushClient pushClient)
        {
            Debug.LogWarning("[uLoopMCP] Network error detected - checking network connectivity");

            if (!await IsNetworkAvailableAsync())
            {
                Debug.LogError("[uLoopMCP] Network is not available");
                return false;
            }

            Debug.Log("[uLoopMCP] Network is available - attempting reconnection");
            return await pushClient.DiscoverAndConnectAsync();
        }

        private static async Task<bool> HandlePortConflictAsync(UnityPushClient pushClient)
        {
            Debug.LogWarning("[uLoopMCP] Port conflict detected - searching for alternative port");

            McpSessionManager sessionManager = McpSessionManager.instance;
            string currentEndpoint = sessionManager?.GetPushServerEndpoint();
            
            if (string.IsNullOrEmpty(currentEndpoint))
            {
                return false;
            }

            string[] parts = currentEndpoint.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out int currentPort))
            {
                return false;
            }

            int alternativePort = FindAlternativePort(currentPort);
            if (alternativePort == -1)
            {
                Debug.LogError("[uLoopMCP] No alternative port found");
                return false;
            }

            string alternativeEndpoint = $"{parts[0]}:{alternativePort}";
            bool success = await pushClient.ConnectToEndpointAsync(alternativeEndpoint);

            if (success)
            {
                sessionManager?.SetPushServerEndpoint(alternativeEndpoint);
                Debug.Log($"[uLoopMCP] Successfully connected to alternative port: {alternativePort}");
                return true;
            }

            return false;
        }

        private static async Task AttemptReconnectionAfterCrashAsync(UnityPushClient pushClient)
        {
            await Task.Delay(RECONNECTION_DELAY_MS);

            Debug.Log("[uLoopMCP] Attempting reconnection after Unity Editor crash");

            for (int attempt = 1; attempt <= MAX_RETRY_ATTEMPTS; attempt++)
            {
                bool success = await pushClient.DiscoverAndConnectAsync();
                if (success)
                {
                    Debug.Log($"[uLoopMCP] Reconnection successful after crash on attempt {attempt}");
                    return;
                }

                await Task.Delay(RETRY_DELAY_MS * attempt);
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

        private static bool IsPortConflictError(Exception error)
        {
            return (error.Message?.Contains("port") == true && error.Message?.Contains("use") == true) ||
                   (error.Message?.Contains("address already in use") == true);
        }

        private static async Task<bool> IsNetworkAvailableAsync()
        {
            try
            {
                using (System.Net.NetworkInformation.Ping ping = new System.Net.NetworkInformation.Ping())
                {
                    PingReply reply = await ping.SendPingAsync("127.0.0.1", 1000);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        private static int FindAlternativePort(int currentPort)
        {
            for (int port = currentPort + 1; port <= currentPort + 100; port++)
            {
                if (IsPortAvailable(port))
                {
                    return port;
                }
            }

            return -1;
        }

        private static bool IsPortAvailable(int port)
        {
            try
            {
                IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
                IPEndPoint[] tcpEndPoints = ipGlobalProperties.GetActiveTcpListeners();

                foreach (IPEndPoint endPoint in tcpEndPoints)
                {
                    if (endPoint.Port == port)
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
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