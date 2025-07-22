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
            Debug.Log($"[uLoopMCP] [DEBUG] InitializePushClientAsync: Starting initialization. isInitialized: {isInitialized}");
            if (isInitialized) 
            {
                Debug.Log("[uLoopMCP] [DEBUG] InitializePushClientAsync: Already initialized, returning");
                return;
            }

            isInitialized = true;
            Debug.Log("[uLoopMCP] [DEBUG] InitializePushClientAsync: Set isInitialized to true, disposing old client");

            await DisposePushClientAsync();

            Debug.Log("[uLoopMCP] [DEBUG] InitializePushClientAsync: Creating new UnityPushClient and setting up event handlers");
            pushClient = new UnityPushClient();
            pushClient.OnConnected += OnPushClientConnected;
            pushClient.OnDisconnected += OnPushClientDisconnected;

            Debug.Log("[uLoopMCP] [DEBUG] InitializePushClientAsync: Starting auto connect process");
            await AutoConnectToPushServerAsync();
        }

        private static async Task AutoConnectToPushServerAsync()
        {
            Debug.Log("[uLoopMCP] [DEBUG] AutoConnectToPushServerAsync: Starting auto connect process");
            McpSessionManager sessionManager = McpSessionManager.GetSafeInstance();
            Debug.Log($"[uLoopMCP] [DEBUG] AutoConnectToPushServerAsync: McpSessionManager.GetSafeInstance() != null: {sessionManager != null}");
            
            // Retry logic for ScriptableSingleton initialization after domain reload
            List<McpSessionManager.ClientEndpointPair> allEndpoints = null;
            int retryCount = 0;
            const int maxRetries = 5;
            const int retryDelayMs = 500;
            
            while (retryCount < maxRetries)
            {
                allEndpoints = sessionManager?.GetAllPushServerEndpoints();
                Debug.Log($"[uLoopMCP] [DEBUG] AutoConnectToPushServerAsync: Retrieved endpoints (attempt {retryCount + 1}/{maxRetries}): {allEndpoints?.Count ?? 0} endpoints found");
                
                if (allEndpoints != null && allEndpoints.Count > 0)
                {
                    break; // Found valid endpoints
                }
                
                retryCount++;
                if (retryCount < maxRetries)
                {
                    Debug.Log($"[uLoopMCP] [DEBUG] AutoConnectToPushServerAsync: No endpoints found, waiting {retryDelayMs}ms before retry {retryCount + 1}/{maxRetries}");
                    await Task.Delay(retryDelayMs);
                }
            }
            
            if (allEndpoints != null && allEndpoints.Count > 0)
            {
                Debug.Log($"[uLoopMCP] [DEBUG] AutoConnectToPushServerAsync: Found {allEndpoints.Count} persisted endpoints, attempting connections");
                
                bool anySuccess = false;
                // Try each endpoint until one succeeds
                foreach (McpSessionManager.ClientEndpointPair pair in allEndpoints)
                {
                    Debug.Log($"[uLoopMCP] Attempting to connect to endpoint '{pair.endpoint}' for client '{pair.clientName}'");
                    
                    bool success = await pushClient.ConnectToEndpointAsync(pair.endpoint);
                    Debug.Log($"[uLoopMCP] [DEBUG] AutoConnectToPushServerAsync: Connection attempt to '{pair.endpoint}' result: {success}");
                    
                    if (success)
                    {
                        Debug.Log($"[uLoopMCP] Successfully connected to Push Server using endpoint '{pair.endpoint}' for client '{pair.clientName}'");
                        anySuccess = true;
                        sessionManager.SetPushServerConnected(true);
                        await SendConnectionEstablishedNotificationAsync();
                        Debug.Log("[uLoopMCP] [DEBUG] AutoConnectToPushServerAsync: Auto connect completed successfully");
                        return;
                    }
                    else
                    {
                        Debug.LogWarning($"[uLoopMCP] Failed to connect to endpoint '{pair.endpoint}' for client '{pair.clientName}', trying next endpoint");
                    }
                }
                
                if (!anySuccess)
                {
                    Debug.Log("[uLoopMCP] [DEBUG] AutoConnectToPushServerAsync: Failed to connect to all persisted endpoints");
                }
            }
            else
            {
                Debug.Log("[uLoopMCP] [DEBUG] AutoConnectToPushServerAsync: No persisted endpoints found after retries");
            }

            Debug.Log("[uLoopMCP] [DEBUG] AutoConnectToPushServerAsync: Starting push server discovery");
            await StartPushServerDiscoveryAsync();
        }

        private static async Task StartPushServerDiscoveryAsync()
        {
            Debug.Log("[uLoopMCP] [DEBUG] StartPushServerDiscoveryAsync: Starting TypeScript Push Server discovery");
            Debug.Log("[uLoopMCP] Starting TypeScript Push Server discovery");
            
            bool success = await pushClient.DiscoverAndConnectAsync();
            Debug.Log($"[uLoopMCP] [DEBUG] StartPushServerDiscoveryAsync: Discovery result: {success}");
            
            if (success)
            {
                Debug.Log("[uLoopMCP] [DEBUG] StartPushServerDiscoveryAsync: Discovery successful, setting session state");
                McpSessionManager sessionManager = McpSessionManager.GetSafeInstance();
                sessionManager?.SetPushServerConnected(true);
                await SendConnectionEstablishedNotificationAsync();
                Debug.Log("[uLoopMCP] Successfully connected to TypeScript Push Server");
            }
            else
            {
                Debug.Log("[uLoopMCP] [DEBUG] StartPushServerDiscoveryAsync: Discovery failed - this triggers fallback or waiting mode");
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
            Debug.Log("[uLoopMCP] [DEBUG] OnAfterAssemblyReload: Domain reload completed - starting recovery process");
            
            // Clear all persisted endpoints since they're all invalid after domain reload
            McpSessionManager sessionManager = McpSessionManager.GetSafeInstance();
            if (sessionManager != null)
            {
                sessionManager.ClearPushServerEndpoint();
                Debug.Log("[uLoopMCP] [DEBUG] OnAfterAssemblyReload: Cleared all persisted endpoints (all invalid after domain reload)");
            }
            
            isInitialized = false;
            Debug.Log("[uLoopMCP] [DEBUG] OnAfterAssemblyReload: isInitialized reset to false, starting push client initialization");
            await InitializePushClientAsync();
            
            Debug.Log($"[uLoopMCP] [DEBUG] OnAfterAssemblyReload: Push client initialization completed. IsConnected: {pushClient?.IsConnected == true}");
            if (pushClient?.IsConnected == true)
            {
                Debug.Log("[uLoopMCP] [DEBUG] OnAfterAssemblyReload: Sending domain reload recovered notification");
                await SendPushNotificationAsync(PushNotificationSerializer.CreateDomainReloadRecoveredNotification());
            }
            else
            {
                Debug.Log("[uLoopMCP] [DEBUG] OnAfterAssemblyReload: Push client not connected, skipping recovery notification");
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