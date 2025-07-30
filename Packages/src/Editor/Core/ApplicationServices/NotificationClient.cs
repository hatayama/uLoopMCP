using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Payload for domain reload complete notification
    /// </summary>
    [System.Serializable]
    public record DomainReloadCompletePayload
    {
        public string type;
        public string timestamp;
        public int unityProcessId;

        public DomainReloadCompletePayload()
        {
            type = "domain_reload_complete";
            timestamp = DateTime.UtcNow.ToString("O");
            unityProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
        }
    }

    /// <summary>
    /// Payload for server restart complete notification
    /// </summary>
    [System.Serializable]
    public record ServerRestartCompletePayload
    {
        public string type;
        public string timestamp;
        public string message;

        public ServerRestartCompletePayload()
        {
            type = "server_restart_complete";
            timestamp = DateTime.UtcNow.ToString("O");
            message = "Unity MCP Server has restarted - please reconnect";
        }
    }

    /// <summary>
    /// TypeScript側のNotification Receive Serverに通知を送信するクライアント
    /// McpEditorSettingsを使用してクライアントごとのポート情報を管理
    /// 
    /// Design document reference: working-notes/SOW_Push_Notification_HTTP_Server.md
    /// 
    /// Related classes:
    /// - McpEditorSettings: ポート情報の永続化
    /// - DomainReloadDetectionService: domain reload検知との統合
    /// - TypeScript NotificationReceiveServer: 通知受信側
    /// </summary>
    public class NotificationClient : IDisposable
    {
        private readonly HttpClient httpClient;

        public NotificationClient()
        {
            httpClient = new();
            httpClient.Timeout = TimeSpan.FromSeconds(McpConstants.Network.HTTP_TIMEOUT_SECONDS);
            
            VibeLogger.LogInfo(
                "notification_client_initialized",
                "Notification client initialized",
                new { }
            );
        }

        /// <summary>
        /// クライアントのNotification Receiveポートを保存
        /// </summary>
        public void SaveClientNotificationPort(string clientEndpoint, int notificationPort)
        {
            // Update in McpEditorSettings
            McpEditorSettings.SetClientNotificationPort(clientEndpoint, notificationPort);
            
            // Get client info for display update
            ConnectedLLMToolData[] tools = McpEditorSettings.GetConnectedLLMTools();
            ConnectedLLMToolData tool = tools?.FirstOrDefault(t => t.Endpoint == clientEndpoint);
            
            if (tool != null && notificationPort > 0)
            {
                // Update display
                McpEditorWindow.Instance?.AddDisplayTool(
                    tool.Name,
                    tool.Endpoint,
                    notificationPort
                );
            }
            
            VibeLogger.LogInfo(
                "client_notification_port_saved",
                "Client notification port saved",
                new { clientEndpoint, notificationPort }
            );
        }

        /// <summary>
        /// Domain reload完了をTypeScript側に通知
        /// McpEditorSettingsから保存されたポート情報を使用
        /// </summary>
        public async Task SendDomainReloadCompleteAsync()
        {
            try
            {
                int[] notificationPorts = McpEditorSettings.GetAllNotificationPorts();

                foreach (int notificationPort in notificationPorts)
                {
                    await SendNotificationToPort(notificationPort);
                }
            }
            catch (Exception ex)
            {
                VibeLogger.LogError(
                    "domain_reload_notification_error",
                    "Error sending domain reload notification",
                    new { error = ex.Message, type = ex.GetType().Name }
                );
            }
        }

        /// <summary>
        /// Server restart完了をTypeScript側に通知
        /// Domain reload通知と同様の仕組みでクライアント復旧を促す
        /// </summary>
        public async Task SendServerRestartCompleteAsync()
        {
            try
            {
                int[] notificationPorts = McpEditorSettings.GetAllNotificationPorts();
                
                VibeLogger.LogInfo(
                    "server_restart_notification_start",
                    $"Sending server restart notification to {notificationPorts.Length} clients",
                    new { portCount = notificationPorts.Length, ports = notificationPorts }
                );

                foreach (int notificationPort in notificationPorts)
                {
                    if (notificationPort > 0)
                    {
                        await SendServerRestartNotificationToPort(notificationPort);
                    }
                }
            }
            catch (Exception ex)
            {
                VibeLogger.LogError(
                    "server_restart_notification_error",
                    "Failed to send server restart notification",
                    new { error = ex.Message, type = ex.GetType().Name }
                );
            }
        }

        private async Task SendNotificationToPort(int notificationPort)
        {
            try
            {
                DomainReloadCompletePayload payload = new();
                string json = JsonUtility.ToJson(payload);
                StringContent content = new(json, Encoding.UTF8, "application/json");
                string notificationUrl = $"http://{McpConstants.Network.LOCALHOST_IP}:{notificationPort}";

                VibeLogger.LogInfo(
                    "domain_reload_notification_sending",
                    "Sending domain reload complete notification",
                    new { notificationPort, url = $"{notificationUrl}{McpConstants.Endpoints.DOMAIN_RELOAD_COMPLETE}" }
                );

                HttpResponseMessage response = await httpClient.PostAsync(
                    $"{notificationUrl}{McpConstants.Endpoints.DOMAIN_RELOAD_COMPLETE}", 
                    content
                );

                if (response.IsSuccessStatusCode)
                {
                    VibeLogger.LogInfo(
                        "domain_reload_notification_sent",
                        "Domain reload notification sent successfully",
                        new { notificationPort, statusCode = (int)response.StatusCode }
                    );
                }
                else
                {
                    VibeLogger.LogWarning(
                        "domain_reload_notification_failed",
                        "Domain reload notification failed",
                        new { notificationPort, statusCode = (int)response.StatusCode, reason = response.ReasonPhrase }
                    );
                }
            }
            catch (Exception ex)
            {
                VibeLogger.LogError(
                    "port_notification_error",
                    "Error sending notification to specific port",
                    new { notificationPort, error = ex.Message }
                );
            }
        }

        private async Task SendServerRestartNotificationToPort(int notificationPort)
        {
            try
            {
                ServerRestartCompletePayload payload = new();
                string jsonPayload = JsonUtility.ToJson(payload);
                StringContent content = new(jsonPayload, Encoding.UTF8, "application/json");
                
                string url = $"http://localhost:{notificationPort}/api/notification";
                HttpResponseMessage response = await httpClient.PostAsync(url, content);
                
                VibeLogger.LogInfo(
                    "server_restart_notification_sent",
                    $"Server restart notification sent to port {notificationPort}",
                    new { 
                        port = notificationPort, 
                        success = response.IsSuccessStatusCode,
                        statusCode = (int)response.StatusCode 
                    }
                );
            }
            catch (Exception ex)
            {
                VibeLogger.LogWarning(
                    "server_restart_notification_port_failed",
                    $"Failed to send server restart notification to port {notificationPort}",
                    new { port = notificationPort, error = ex.Message }
                );
            }
        }

        /// <summary>
        /// TypeScript側のNotification Serverが起動しているかチェック
        /// </summary>
        public async Task<bool> CheckServerHealthAsync(int notificationPort)
        {
            try
            {
                string healthUrl = $"http://{McpConstants.Network.LOCALHOST_IP}:{notificationPort}{McpConstants.Endpoints.HEALTH_CHECK}";
                HttpResponseMessage response = await httpClient.GetAsync(healthUrl);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            httpClient?.Dispose();
        }
    }
}