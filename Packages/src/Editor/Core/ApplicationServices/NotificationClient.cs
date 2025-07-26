using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
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
            httpClient.Timeout = TimeSpan.FromSeconds(5);
            
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
            McpEditorSettings.SetClientNotificationPort(clientEndpoint, notificationPort);
            
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

        private async Task SendNotificationToPort(int notificationPort)
        {
            try
            {
                object payload = new
                {
                    type = "domain_reload_complete",
                    timestamp = DateTime.UtcNow.ToString("O"),
                    unityProcessId = System.Diagnostics.Process.GetCurrentProcess().Id
                };

                string json = JsonUtility.ToJson(payload);
                StringContent content = new(json, Encoding.UTF8, "application/json");
                string notificationUrl = $"http://127.0.0.1:{notificationPort}";

                VibeLogger.LogInfo(
                    "domain_reload_notification_sending",
                    "Sending domain reload complete notification",
                    new { notificationPort, url = $"{notificationUrl}/domain-reload-complete" }
                );

                HttpResponseMessage response = await httpClient.PostAsync(
                    $"{notificationUrl}/domain-reload-complete", 
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

        /// <summary>
        /// TypeScript側のNotification Serverが起動しているかチェック
        /// </summary>
        public async Task<bool> CheckServerHealthAsync(int notificationPort)
        {
            try
            {
                string healthUrl = $"http://127.0.0.1:{notificationPort}/health";
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