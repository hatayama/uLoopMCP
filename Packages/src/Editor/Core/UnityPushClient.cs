/**
 * Unity Push通知クライアント
 * 設計書参照: /.kiro/specs/unity-push-notification-system/design.md
 * 関連クラス: UnityPushNotificationReceiveServer.ts, McpSessionManager.cs
 * 
 * 責任:
 * - TypeScript Push通知受信サーバーへの接続
 * - Push通知の送信
 * - エンドポイント情報の永続化
 * - 接続状態の監視
 */

using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    public class UnityPushClient : IDisposable
    {
        private TcpClient tcpClient;
        private NetworkStream stream;
        private string currentEndpoint;
        private bool isConnected;
        private bool isDisposed;
        private CancellationTokenSource cancellationTokenSource;

        public bool IsConnected => isConnected && tcpClient?.Connected == true;

        public event Action<string> OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<Exception> OnError;

        public UnityPushClient()
        {
            isConnected = false;
            isDisposed = false;
            cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task<bool> DiscoverAndConnectAsync()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(UnityPushClient));
            }

            string persistedEndpoint = LoadPersistedEndpoint();
            
            if (!string.IsNullOrEmpty(persistedEndpoint))
            {
                bool success = await ConnectToEndpointAsync(persistedEndpoint);
                if (success)
                {
                    return true;
                }
                
                Debug.LogWarning($"[uLoopMCP] Failed to connect to persisted endpoint: {persistedEndpoint}");
                ClearPersistedEndpoint();
            }

            // 設計書通り: 保存されたエンドポイントがない場合は接続を試行しない
            Debug.LogWarning("[uLoopMCP] No persisted endpoint found. Waiting for TypeScript server to provide endpoint.");
            return false;
        }

        public async Task<bool> ConnectToEndpointAsync(string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint))
            {
                return false;
            }

            string[] parts = endpoint.Split(':');
            if (parts.Length != 2)
            {
                Debug.LogError($"[uLoopMCP] Invalid endpoint format: {endpoint}");
                return false;
            }

            string host = parts[0];
            if (!int.TryParse(parts[1], out int port))
            {
                Debug.LogError($"[uLoopMCP] Invalid port in endpoint: {endpoint}");
                return false;
            }

            return await ConnectAsync(host, port);
        }

        private async Task<bool> ConnectAsync(string host, int port)
        {
            if (isDisposed)
            {
                return false;
            }

            await DisconnectAsync();

            tcpClient = new TcpClient();
            
            try
            {
                var connectTask = tcpClient.ConnectAsync(host, port);
                var timeoutTask = Task.Delay(ConnectionTimeouts.CONNECTION_TIMEOUT_MS, cancellationTokenSource.Token);
                
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                
                if (completedTask == timeoutTask || !tcpClient.Connected)
                {
                    Debug.LogWarning($"[uLoopMCP] Connection timeout or failed: {host}:{port}");
                    return false;
                }

                stream = tcpClient.GetStream();
                currentEndpoint = $"{host}:{port}";
                isConnected = true;
                
                PersistEndpoint(currentEndpoint);
                OnConnected?.Invoke(currentEndpoint);
                
                Debug.Log($"[uLoopMCP] Connected to TypeScript Push Server: {currentEndpoint}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[uLoopMCP] Connection failed: {ex.Message}");
                
                bool handled = await PushNotificationErrorHandler.HandleConnectionFailureAsync(this, ex);
                if (!handled)
                {
                    OnError?.Invoke(ex);
                }
                
                return false;
            }
        }


        public async Task SendPushNotificationAsync(PushNotification notification)
        {
            if (!IsConnected || isDisposed)
            {
                Debug.LogWarning("[uLoopMCP] Cannot send push notification: not connected");
                return;
            }

            try
            {
                string json = JsonUtility.ToJson(notification);
                byte[] data = Encoding.UTF8.GetBytes(json + "\n");

                var writeTask = stream.WriteAsync(data, 0, data.Length, cancellationTokenSource.Token);
                var timeoutTask = Task.Delay(ConnectionTimeouts.PUSH_NOTIFICATION_TIMEOUT_MS, cancellationTokenSource.Token);
                
                var completedTask = await Task.WhenAny(writeTask, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    Debug.LogWarning("[uLoopMCP] Push notification send timeout");
                    return;
                }

                await stream.FlushAsync(cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[uLoopMCP] Failed to send push notification: {ex.Message}");
                OnError?.Invoke(ex);
                
                if (!IsConnected)
                {
                    Debug.Log("[uLoopMCP] Connection lost during push notification send - triggering reconnection");
                    OnDisconnected?.Invoke(currentEndpoint);
                }
            }
        }

        public async Task SendDisconnectNotificationAsync(DisconnectReason reason)
        {
            PushNotification notification = new PushNotification
            {
                type = reason.type,
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                payload = new NotificationPayload
                {
                    reason = reason
                }
            };

            await SendPushNotificationAsync(notification);
        }

        public async Task DisconnectAsync()
        {
            if (isConnected && !isDisposed)
            {
                await SendDisconnectNotificationAsync(new DisconnectReason
                {
                    type = "USER_DISCONNECT",
                    message = "UnityPushClient disconnecting"
                });
                
                await Task.Delay(100);
            }

            isConnected = false;
            
            stream?.Close();
            stream?.Dispose();
            stream = null;
            
            tcpClient?.Close();
            tcpClient?.Dispose();
            tcpClient = null;

            string endpoint = currentEndpoint;
            currentEndpoint = null;
            
            OnDisconnected?.Invoke(endpoint);
        }

        public void PersistEndpoint(string endpoint)
        {
            if (McpSessionManager.instance != null)
            {
                McpSessionManager.instance.SetPushServerEndpoint(endpoint);
            }
        }

        public string LoadPersistedEndpoint()
        {
            if (McpSessionManager.instance != null)
            {
                return McpSessionManager.instance.GetPushServerEndpoint();
            }
            
            return null;
        }

        public void ClearPersistedEndpoint()
        {
            if (McpSessionManager.instance != null)
            {
                McpSessionManager.instance.ClearPushServerEndpoint();
            }
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            
            cancellationTokenSource?.Cancel();
            
            var disconnectTask = DisconnectAsync();
            disconnectTask.Wait(1000);
            
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
            
            OnConnected = null;
            OnDisconnected = null;
            OnError = null;
        }
    }

    [Serializable]
    public class PushNotification
    {
        public string type;
        public string timestamp;
        public NotificationPayload payload;
    }

    [Serializable]
    public class NotificationPayload
    {
        public DisconnectReason reason;
        public string endpoint;
        public ClientInfo clientInfo;
        public ToolsInfo toolsInfo;
    }

    [Serializable]
    public class DisconnectReason
    {
        public string type;
        public string message;
    }

    [Serializable]
    public class ClientInfo
    {
        public string unityVersion;
        public string projectPath;
        public string sessionId;
    }
}