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
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    [InitializeOnLoad]
    public static class UnityPushConnectionManager
    {
        // Connection retry configuration
        private const int MAX_ENDPOINT_RETRY_COUNT = 5;
        private const int ENDPOINT_RETRY_DELAY_MS = 500;
        
        // Timing constants for various operations
        private const int RECONNECTION_DELAY_MS = 3000;
        private const int DOMAIN_RELOAD_DELAY_MS = 500;
        private const int SHUTDOWN_DELAY_MS = 500;
        
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
            if (isInitialized) 
            {
                return;
            }

            isInitialized = true;
            await DisposePushClientAsync();

            pushClient = new UnityPushClient();
            pushClient.OnConnected += OnPushClientConnected;
            pushClient.OnDisconnected += OnPushClientDisconnected;

            await AutoConnectToPushServerAsync();
        }

        private static async Task AutoConnectToPushServerAsync()
        {
            McpSessionManager sessionManager = McpSessionManager.GetSafeInstance();
            
            List<McpSessionManager.ClientEndpointPair> allEndpoints = await GetValidEndpointsWithRetry(sessionManager);
            
            if (await TryConnectToStoredEndpoints(allEndpoints, sessionManager))
            {
                return; // Successfully connected
            }
            
            await StartPushServerDiscoveryAsync();
        }
        
        private static async Task<List<McpSessionManager.ClientEndpointPair>> GetValidEndpointsWithRetry(McpSessionManager sessionManager)
        {
            List<McpSessionManager.ClientEndpointPair> allEndpoints = null;
            int retryCount = 0;
            
            while (retryCount < MAX_ENDPOINT_RETRY_COUNT)
            {
                allEndpoints = sessionManager?.GetAllPushServerEndpoints();
                
                if (allEndpoints != null && allEndpoints.Count > 0)
                {
                    break; // Found valid endpoints
                }
                
                retryCount++;
                if (retryCount < MAX_ENDPOINT_RETRY_COUNT)
                {
                    await Task.Delay(ENDPOINT_RETRY_DELAY_MS);
                }
            }
            
            return allEndpoints;
        }
        
        private static async Task<bool> TryConnectToStoredEndpoints(List<McpSessionManager.ClientEndpointPair> endpoints, McpSessionManager sessionManager)
        {
            if (endpoints == null || endpoints.Count == 0)
                return false;
            
            // Try each endpoint until one succeeds
            foreach (McpSessionManager.ClientEndpointPair pair in endpoints)
            {
                bool success = await pushClient.ConnectToEndpointAsync(pair.endpoint);
                
                if (success)
                {
                    Debug.Log($"[uLoopMCP] Successfully connected to Push Server using endpoint '{pair.endpoint}' for client '{pair.clientName}'");
                    sessionManager.SetPushServerConnected(true);
                    await SendConnectionEstablishedNotificationAsync();
                    return true;
                }
                
                Debug.LogWarning($"[uLoopMCP] Failed to connect to endpoint '{pair.endpoint}' for client '{pair.clientName}', trying next endpoint");
            }
            
            return false;
        }

        private static async Task StartPushServerDiscoveryAsync()
        {
            Debug.Log("[uLoopMCP] Starting TypeScript Push Server discovery");
            
            bool success = await pushClient.DiscoverAndConnectAsync();
            
            if (success)
            {
                McpSessionManager sessionManager = McpSessionManager.GetSafeInstance();
                sessionManager?.SetPushServerConnected(true);
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

            string unityServerEndpoint = $"localhost:{McpServerController.ServerPort}";
            PushNotification notification = PushNotificationSerializer.CreateConnectionEstablishedNotification(
                unityServerEndpoint
            );

            await pushClient.SendPushNotificationAsync(notification);
        }

        private static void OnPushClientConnected(string endpoint)
        {
            Debug.Log($"[uLoopMCP] Push client connected to: {endpoint}");
            
            McpSessionManager sessionManager = McpSessionManager.GetSafeInstance();
            if (sessionManager != null)
            {
                sessionManager.SetPushServerConnected(true);
                sessionManager.UpdateLastConnectionTime();
            }
            
            isReconnecting = false;
        }

        private static void OnPushClientDisconnected(string endpoint)
        {
            // Only log disconnection if we were actually connected (not during discovery)
            if (!string.IsNullOrEmpty(endpoint))
            {
                Debug.Log($"[uLoopMCP] Push client disconnected from: {endpoint}");
            }
            
            McpSessionManager sessionManager = McpSessionManager.GetSafeInstance();
            sessionManager?.SetPushServerConnected(false);
            
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
            
            await Task.Delay(RECONNECTION_DELAY_MS);
            
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
            
            await Task.Delay(DOMAIN_RELOAD_DELAY_MS);
        }

        private static async void OnAfterAssemblyReload()
        {
            Debug.Log("[uLoopMCP] Domain reload completed - starting recovery process");
            
            // Clear all persisted endpoints since they're all invalid after domain reload
            McpSessionManager sessionManager = McpSessionManager.GetSafeInstance();
            if (sessionManager != null)
            {
                sessionManager.ClearPushServerEndpoint();
            }
            
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
            
            await Task.Delay(SHUTDOWN_DELAY_MS);
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