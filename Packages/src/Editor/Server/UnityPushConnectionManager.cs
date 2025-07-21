/**
 * Unity Push通知接続管理
 * 設計書参照: /.kiro/specs/unity-push-notification-system/design.md
 * 関連クラス: UnityPushClient.cs, McpServerController.cs, McpSessionManager.cs
 * 
 * 責任:
 * - UnityPushClientの生成・管理
 * - TypeScript Push通知受信サーバーの探索・接続
 * - 自動再接続ロジック
 * - Unity起動時のエンドポイント情報確認ロジック
 */

using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    [InitializeOnLoad]
    public static class UnityPushConnectionManager
    {
        private static UnityPushClient pushClient;
        private static bool isInitialized = false;
        private static bool isReconnecting = false;

        public static UnityPushClient CurrentPushClient => pushClient;
        public static bool IsPushClientConnected => pushClient?.IsConnected ?? false;

        static UnityPushConnectionManager()
        {
            EditorApplication.delayCall += InitializeAfterDomainReload;
            EditorApplication.quitting += OnEditorQuitting;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }

        private static async void InitializeAfterDomainReload()
        {
            if (isInitialized) return;

            await InitializePushClientAsync();
        }

        public static async Task InitializePushClientAsync()
        {
            if (isInitialized) return;

            isInitialized = true;

            await DisposePushClientAsync();

            pushClient = new UnityPushClient();
            pushClient.OnConnected += OnPushClientConnected;
            pushClient.OnDisconnected += OnPushClientDisconnected;

            await AutoConnectToPushServerAsync();
        }

        private static async Task AutoConnectToPushServerAsync()
        {
            McpSessionManager sessionManager = McpSessionManager.instance;
            
            string persistedEndpoint = sessionManager.GetPushServerEndpoint();
            
            if (!string.IsNullOrEmpty(persistedEndpoint))
            {
                Debug.Log($"[uLoopMCP] Attempting to connect to persisted endpoint: {persistedEndpoint}");
                
                bool success = await pushClient.ConnectToEndpointAsync(persistedEndpoint);
                if (success)
                {
                    sessionManager.SetPushServerConnected(true);
                    await SendConnectionEstablishedNotificationAsync();
                    return;
                }
                
                Debug.LogWarning($"[uLoopMCP] Failed to connect to persisted endpoint, clearing it");
                sessionManager.ClearPushServerEndpoint();
            }

            await StartPushServerDiscoveryAsync();
        }

        private static async Task StartPushServerDiscoveryAsync()
        {
            Debug.Log("[uLoopMCP] Starting TypeScript Push Server discovery");
            
            bool success = await pushClient.DiscoverAndConnectAsync();
            
            if (success)
            {
                McpSessionManager.instance.SetPushServerConnected(true);
                await SendConnectionEstablishedNotificationAsync();
                Debug.Log("[uLoopMCP] Successfully connected to TypeScript Push Server");
            }
            else
            {
                Debug.LogWarning("[uLoopMCP] Failed to discover TypeScript Push Server");
            }
        }

        private static async Task SendConnectionEstablishedNotificationAsync()
        {
            if (pushClient?.IsConnected != true) return;

            PushNotification notification = PushNotificationSerializer.CreateConnectionEstablishedNotification(
                $"localhost:{McpServerController.ServerPort}"
            );

            await pushClient.SendPushNotificationAsync(notification);
        }

        private static void OnPushClientConnected(string endpoint)
        {
            Debug.Log($"[uLoopMCP] Push client connected to: {endpoint}");
            
            McpSessionManager sessionManager = McpSessionManager.instance;
            sessionManager.SetPushServerConnected(true);
            sessionManager.UpdateLastConnectionTime();
            
            isReconnecting = false;
        }

        private static void OnPushClientDisconnected(string endpoint)
        {
            Debug.Log($"[uLoopMCP] Push client disconnected from: {endpoint}");
            
            McpSessionManager sessionManager = McpSessionManager.instance;
            sessionManager.SetPushServerConnected(false);
            
            if (!isReconnecting && !EditorApplication.isCompiling)
            {
                StartReconnectionProcessAsync();
            }
        }

        private static async void StartReconnectionProcessAsync()
        {
            if (isReconnecting) return;

            isReconnecting = true;
            
            Debug.Log("[uLoopMCP] Starting reconnection process");
            
            await Task.Delay(3000);
            
            if (pushClient != null && !pushClient.IsConnected)
            {
                await AutoConnectToPushServerAsync();
            }
            
            isReconnecting = false;
        }

        public static async Task SendPushNotificationAsync(PushNotification notification)
        {
            if (pushClient?.IsConnected == true)
            {
                await pushClient.SendPushNotificationAsync(notification);
            }
        }

        public static async Task SendDisconnectNotificationAsync(string reasonType, string message)
        {
            if (pushClient?.IsConnected == true)
            {
                await pushClient.SendDisconnectNotificationAsync(new DisconnectReason
                {
                    type = reasonType,
                    message = message
                });
            }
        }

        private static async void OnBeforeAssemblyReload()
        {
            Debug.Log("[uLoopMCP] Domain reload starting - sending notification");
            
            await SendPushNotificationAsync(PushNotificationSerializer.CreateDomainReloadNotification());
            
            await Task.Delay(500);
        }

        private static async void OnAfterAssemblyReload()
        {
            Debug.Log("[uLoopMCP] Domain reload completed - sending recovery notification");
            
            isInitialized = false;
            await InitializePushClientAsync();
            
            if (pushClient?.IsConnected == true)
            {
                await SendPushNotificationAsync(PushNotificationSerializer.CreateDomainReloadRecoveredNotification());
            }
        }

        private static async void OnEditorQuitting()
        {
            Debug.Log("[uLoopMCP] Unity Editor quitting - sending shutdown notification");
            
            await SendDisconnectNotificationAsync(
                PushNotificationConstants.UNITY_SHUTDOWN,
                "Unity Editor is shutting down"
            );
            
            await Task.Delay(500);
            await DisposePushClientAsync();
        }

        public static async Task DisposePushClientAsync()
        {
            if (pushClient != null)
            {
                await pushClient.DisconnectAsync();
                pushClient.Dispose();
                pushClient = null;
            }
        }

        public static async Task RestartPushClientAsync()
        {
            await DisposePushClientAsync();
            await InitializePushClientAsync();
        }
    }
}