using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Lightweight TCP server embedded in Unity runtime builds.
    /// Accepts a single CLI connection and handles JSON-RPC 2.0 communication
    /// using the same Content-Length framing protocol as the Editor MCP server.
    /// </summary>
    public sealed class DeviceAgentServer : IDisposable
    {
        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private Task _serverTask;
        private bool _isRunning;
        private bool _disposed;

        private readonly DeviceJsonRpcProcessor _processor;
        private readonly int _port;

        public bool IsRunning => _isRunning;
        public int Port => _port;

        public DeviceAgentServer(DeviceToolRegistry registry, string authToken, int port = DeviceAgentConstants.DEFAULT_PORT)
        {
            System.Diagnostics.Debug.Assert(registry != null, "registry must not be null");
            System.Diagnostics.Debug.Assert(!string.IsNullOrEmpty(authToken), "authToken must not be empty");

            _port = port;
            _processor = new DeviceJsonRpcProcessor(registry, authToken);
        }

        public void Start()
        {
            if (_isRunning) return;

            _cts = new CancellationTokenSource();

            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start();
            _isRunning = true;

            Debug.Log($"[DeviceAgent] Server started on 127.0.0.1:{_port}");

            _serverTask = Task.Run(() => ServerLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;

            _cts?.Cancel();

            try { _listener?.Stop(); }
            finally { _listener = null; }

            try { _serverTask?.Wait(TimeSpan.FromSeconds(3)); }
            finally { _serverTask = null; }

            _cts?.Dispose();
            _cts = null;

            _processor.ResetAuth();
            Debug.Log("[DeviceAgent] Server stopped");
        }

        private async Task ServerLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _isRunning)
            {
                TcpClient client = null;
                try
                {
                    client = await Task.Run(() => _listener.AcceptTcpClient(), ct);
                    Debug.Log($"[DeviceAgent] Client connected: {client.Client.RemoteEndPoint}");

                    // Reset auth state for new connection
                    _processor.ResetAuth();

                    await HandleClientAsync(client, ct);
                }
                catch (ObjectDisposedException) { break; }
                catch (OperationCanceledException) { break; }
                catch (SocketException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    if (!ct.IsCancellationRequested)
                    {
                        Debug.LogWarning($"[DeviceAgent] Server loop error: {ex.Message}");
                    }
                }
                finally
                {
                    client?.Close();
                    _processor.ResetAuth();
                    Debug.Log("[DeviceAgent] Client disconnected");
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            using DynamicBufferManager bufferManager = new();
            using MessageReassembler reassembler = new(bufferManager);
            using NetworkStream stream = client.GetStream();

            byte[] readBuffer = bufferManager.GetBuffer(BufferConfig.INITIAL_BUFFER_SIZE);

            while (!ct.IsCancellationRequested && client.Connected)
            {
                int bytesRead = await stream.ReadAsync(readBuffer, 0, readBuffer.Length, ct);
                if (bytesRead == 0) break;

                reassembler.AddData(readBuffer, bytesRead);

                string[] messages = reassembler.ExtractCompleteMessages();
                foreach (string requestJson in messages)
                {
                    if (string.IsNullOrWhiteSpace(requestJson)) continue;

                    // Enforce max request size
                    if (Encoding.UTF8.GetByteCount(requestJson) > DeviceAgentConstants.MAX_REQUEST_BYTES)
                    {
                        string errorResponse = DeviceJsonRpcProcessor.CreateErrorResponse(
                            null,
                            DeviceAgentConstants.ErrorCodes.PAYLOAD_TOO_LARGE,
                            "Request payload too large");
                        await WriteFramedResponseAsync(stream, errorResponse, ct);
                        continue;
                    }

                    string responseJson = await _processor.ProcessRequestAsync(requestJson, ct);

                    if (!string.IsNullOrEmpty(responseJson))
                    {
                        await WriteFramedResponseAsync(stream, responseJson, ct);
                    }
                }
            }
        }

        private static async Task WriteFramedResponseAsync(NetworkStream stream, string json, CancellationToken ct)
        {
            int contentLength = Encoding.UTF8.GetByteCount(json);
            string framed = $"Content-Length: {contentLength}\r\n\r\n{json}";
            byte[] data = Encoding.UTF8.GetBytes(framed);
            await stream.WriteAsync(data, 0, data.Length, ct);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }
    }
}
