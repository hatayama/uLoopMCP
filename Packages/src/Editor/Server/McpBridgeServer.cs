using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using UnityEditor;
using Newtonsoft.Json;


namespace io.github.hatayama.uLoopMCP
{
    // Related classes:
    // - McpServerController: Manages the lifecycle of this server.
    // - UnityCommandExecutor: Executes commands received from clients.
    // - JsonRpcProcessor: Handles JSON-RPC 2.0 message processing.
    /// <summary>
    /// Represents a connected client
    /// </summary>
    public class ConnectedClient
    {
        public readonly string Endpoint;
        public readonly string ClientName; 
        public readonly DateTime ConnectedAt;
        public readonly NetworkStream Stream;

        public ConnectedClient(string endpoint, NetworkStream stream, string clientName = McpConstants.UNKNOWN_CLIENT_NAME)
        {
            Endpoint = endpoint;
            Stream = stream;
            ClientName = clientName;
            ConnectedAt = DateTime.Now;
        }
        
        // Private constructor for WithClientName to preserve ConnectedAt
        private ConnectedClient(string endpoint, NetworkStream stream, string clientName, DateTime connectedAt)
        {
            Endpoint = endpoint;
            Stream = stream;
            ClientName = clientName;
            ConnectedAt = connectedAt;
        }
        
        public ConnectedClient WithClientName(string clientName)
        {
            return new ConnectedClient(Endpoint, Stream, clientName, ConnectedAt);
        }
    }

    /// <summary>
    /// Immutable JSON-RPC notification structure
    /// </summary>
    internal class JsonRpcNotification
    {
        public readonly string JsonRpc;
        public readonly string Method;
        public readonly object Params;
        
        public JsonRpcNotification(string jsonRpc, string method, object parameters)
        {
            JsonRpc = jsonRpc;
            Method = method;
            Params = parameters;
        }
    }

    /// <summary>
    /// Unity MCP Bridge TCP/IP Server.
    /// Accepts connections from the TypeScript MCP Server and handles JSON-RPC 2.0 communication.
    /// </summary>
    public class McpBridgeServer : IDisposable
    {
        // Note: Domain reload progress is now tracked via McpSessionManager
        
        // HResult error codes for normal disconnection detection
        private static readonly HashSet<int> NormalDisconnectionHResults = new HashSet<int>
        {
            unchecked((int)0x800703E3), // ERROR_OPERATION_ABORTED
            unchecked((int)0x80070040), // ERROR_NETNAME_DELETED
            unchecked((int)0x80072745), // ERROR_CONNECTION_ABORTED
            unchecked((int)0x80072746)  // ERROR_CONNECTION_RESET
        };
        
        private TcpListener tcpListener;
        private CancellationTokenSource cancellationTokenSource;
        private Task serverTask;
        private bool isRunning = false;
        
        // Client management for broadcasting notifications
        private readonly ConcurrentDictionary<string, ConnectedClient> connectedClients = new ConcurrentDictionary<string, ConnectedClient>();
        
        /// <summary>
        /// Whether the server is running.
        /// </summary>
        public bool IsRunning => isRunning;
        
        /// <summary>
        /// The server's port number.
        /// </summary>
        public int Port { get; private set; } = McpServerConfig.DEFAULT_PORT;
        
        /// <summary>
        /// Event on client connection.
        /// </summary>
        public event Action<string> OnClientConnected;
        
        /// <summary>
        /// Event on client disconnection.
        /// </summary>
        public event Action<string> OnClientDisconnected;
        
        /// <summary>
        /// Event on error.
        /// </summary>
        public event Action<string> OnError;

        /// <summary>
        /// Generate unique client key using Endpoint
        /// </summary>
        private string GenerateClientKey(string endpoint)
        {
            // Use endpoint as unique identifier
            return endpoint;
        }

        /// <summary>
        /// Get list of connected clients
        /// </summary>
        public IReadOnlyCollection<ConnectedClient> GetConnectedClients()
        {
            return connectedClients.Values.ToArray();
        }

        /// <summary>
        /// Update client name for a connected client
        /// </summary>
        public void UpdateClientName(string clientEndpoint, string clientName)
        {
            // Find client by endpoint (backward compatibility)
            ConnectedClient targetClient = connectedClients.Values
                .FirstOrDefault(c => c.Endpoint == clientEndpoint);
                
            if (targetClient != null)
            {
                string clientKey = GenerateClientKey(targetClient.Endpoint);
                ConnectedClient updatedClient = targetClient.WithClientName(clientName);
                bool updateResult = connectedClients.TryUpdate(clientKey, updatedClient, targetClient);
                
                McpLogger.LogInfo($"[UpdateClientName] {clientEndpoint}: '{targetClient.ClientName}' → '{clientName}' (Success: {updateResult})");
                McpLogger.LogInfo($"[UpdateClientName] ConnectedAt preserved: {updatedClient.ConnectedAt:HH:mm:ss.fff}");
                
                // Clear reconnecting flags when client name is successfully set (client is now fully connected)
                if (updateResult && clientName != McpConstants.UNKNOWN_CLIENT_NAME)
                {
                    McpServerController.ClearReconnectingFlag();
                }
            }
            else
            {
                McpLogger.LogWarning($"[UpdateClientName] Client not found for endpoint: {clientEndpoint}");
            }
        }




        /// <summary>
        /// Checks if the specified port is in use.
        /// Delegates to NetworkUtility for consistent port checking behavior.
        /// </summary>
        /// <param name="port">The port number to check.</param>
        /// <returns>True if the port is in use.</returns>
        public static bool IsPortInUse(int port)
        {
            return NetworkUtility.IsPortInUse(port);
        }

        /// <summary>
        /// Starts the server.
        /// </summary>
        /// <param name="port">The port number (default: 7400).</param>
        public void StartServer(int port = McpServerConfig.DEFAULT_PORT)
        {
            if (isRunning)
            {
                return;
            }

            Port = port;
            cancellationTokenSource = new CancellationTokenSource();
            
            try
            {
                tcpListener = new TcpListener(IPAddress.Loopback, Port);
                tcpListener.Start();
                isRunning = true;
                
                serverTask = Task.Run(() => ServerLoop(cancellationTokenSource.Token));
                
                McpLogger.LogInfo($"Unity MCP Server started on port {Port}");
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                isRunning = false;
                string errorMessage = $"Port {Port} is already in use. Please choose a different port.";
                McpLogger.LogError(errorMessage);
                OnError?.Invoke(errorMessage);
                throw new InvalidOperationException(errorMessage, ex);
            }
            catch (Exception ex)
            {
                isRunning = false;
                string errorMessage = $"Failed to start MCP Server: {ex.Message}";
                McpLogger.LogError(errorMessage);
                OnError?.Invoke(errorMessage);
                throw;
            }
        }

        /// <summary>
        /// Stops the server.
        /// </summary>
        public void StopServer()
        {
            if (!isRunning)
            {
                return;
            }

            McpLogger.LogInfo("Stopping Unity MCP Server...");
            isRunning = false;
            
            // Explicitly disconnect all connected clients before stopping the server
            DisconnectAllClients();
            
            // Request cancellation.
            cancellationTokenSource?.Cancel();
            
            // Stop the TCP listener.
            try
            {
                tcpListener?.Stop();
            }
            finally
            {
                // Set the TCP listener to null regardless of success/failure
                tcpListener = null;
            }
            
            // Wait for the server task to complete.
            try
            {
                serverTask?.Wait(TimeSpan.FromSeconds(McpServerConfig.SHUTDOWN_TIMEOUT_SECONDS));
            }
            finally
            {
                // Set the server task to null regardless of success/failure
                serverTask = null;
            }
            
            // Dispose of the cancellation token source.
            try
            {
                cancellationTokenSource?.Dispose();
            }
            finally
            {
                // Set the cancellation token source to null regardless of success/failure
                cancellationTokenSource = null;
            }
            

            
            McpLogger.LogInfo("Unity MCP Server stopped");
        }

        /// <summary>
        /// Explicitly disconnect all connected clients
        /// This ensures TypeScript clients receive proper close events
        /// </summary>
        private void DisconnectAllClients()
        {
            if (connectedClients.IsEmpty)
            {
                return;
            }

            McpLogger.LogInfo($"Disconnecting {connectedClients.Count} connected clients...");
            
            List<string> clientsToRemove = new List<string>();
            
            foreach (KeyValuePair<string, ConnectedClient> client in connectedClients)
            {
                try
                {
                    // Close the NetworkStream to send proper close event to TypeScript client
                    if (client.Value.Stream != null && client.Value.Stream.CanWrite)
                    {
                        client.Value.Stream.Close();
                    }
                    clientsToRemove.Add(client.Key);
                }
                catch (Exception ex)
                {
                    McpLogger.LogWarning($"Error disconnecting client {client.Key}: {ex.Message}");
                    clientsToRemove.Add(client.Key); // Remove even if disconnect failed
                }
            }
            
            // Remove all clients from the connected clients list
            foreach (string clientKey in clientsToRemove)
            {
                connectedClients.TryRemove(clientKey, out _);
            }
            
            McpLogger.LogInfo("All clients disconnected");
        }

        /// <summary>
        /// The server's main loop.
        /// </summary>
        private async Task ServerLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && isRunning)
            {
                try
                {
                    TcpClient client = await AcceptTcpClientAsync(tcpListener, cancellationToken);
                    if (client != null)
                    {
                        string clientEndpoint = client.Client.RemoteEndPoint?.ToString() ?? McpServerConfig.UNKNOWN_CLIENT_ENDPOINT;
                        OnClientConnected?.Invoke(clientEndpoint);
                        
                        // Execute client handling in a separate task (fire-and-forget).
                        _ = Task.Run(() => HandleClient(client, cancellationToken));
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Normal exception when stopping the server.
                    break;
                }
                catch (ThreadAbortException ex)
                {
                    // Log and re-throw ThreadAbortException
                    if (!McpSessionManager.instance.IsDomainReloadInProgress)
                    {
                        McpLogger.LogError($"Unexpected thread abort in server loop: {ex.Message}");
                        OnError?.Invoke($"Unexpected thread abort: {ex.Message}");
                    }
                    throw;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        string errorMessage = $"Server loop error: {ex.Message}";
                        McpLogger.LogError(errorMessage);
                        OnError?.Invoke(errorMessage);
                    }
                }
            }
        }

        /// <summary>
        /// Asynchronously accepts a client from the TcpListener.
        /// </summary>
        private async Task<TcpClient> AcceptTcpClientAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            try
            {
                return await Task.Run(() => listener.AcceptTcpClient(), cancellationToken);
            }
            catch (ThreadAbortException ex)
            {
                // Log and re-throw ThreadAbortException
                if (!McpSessionManager.instance.IsDomainReloadInProgress)
                {
                    McpLogger.LogError($"Unexpected thread abort in AcceptTcpClient: {ex.Message}");
                }
                throw;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Handles communication with the client using Content-Length framing.
        /// </summary>
        private async Task HandleClient(TcpClient client, CancellationToken cancellationToken)
        {
            string clientEndpoint = client.Client.RemoteEndPoint?.ToString() ?? McpServerConfig.UNKNOWN_CLIENT_ENDPOINT;
            string correlationId = VibeLogger.GenerateCorrelationId();
            
            VibeLogger.LogInfo(
                "mcp_client_connection_start",
                "Starting client connection handling",
                new {
                    client_endpoint = clientEndpoint,
                    client_connected = client.Connected,
                    cancellation_requested = cancellationToken.IsCancellationRequested
                },
                correlationId,
                "New client connection initiated. Starting message handling loop."
            );
            
            // Initialize new components for Content-Length framing
            DynamicBufferManager bufferManager = null;
            MessageReassembler messageReassembler = null;
            
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    VibeLogger.LogInfo(
                        "mcp_network_stream_acquired",
                        "NetworkStream acquired for client",
                        new {
                            client_endpoint = clientEndpoint,
                            stream_can_read = stream.CanRead,
                            stream_can_write = stream.CanWrite,
                            stream_can_timeout = stream.CanTimeout
                        },
                        correlationId,
                        "NetworkStream successfully acquired and ready for communication."
                    );
                    
                    // Check for existing connection from same endpoint and close it
                    string clientKey = GenerateClientKey(clientEndpoint);
                    if (connectedClients.TryGetValue(clientKey, out ConnectedClient existingClient))
                    {
                        VibeLogger.LogWarning(
                            "mcp_existing_client_cleanup",
                            "Closing existing connection from same endpoint",
                            new {
                                client_endpoint = clientEndpoint,
                                existing_client_connected_at = existingClient.ConnectedAt,
                                client_key = clientKey
                            },
                            correlationId,
                            "Duplicate connection detected. Closing previous connection before establishing new one."
                        );
                        
                        existingClient.Stream?.Close();
                        connectedClients.TryRemove(clientKey, out _);
                    }
                    
                    // Add new client to connected clients for notification broadcasting
                    ConnectedClient connectedClient = new ConnectedClient(clientEndpoint, stream);
                    bool addResult = connectedClients.TryAdd(clientKey, connectedClient);
                    
                    VibeLogger.LogInfo(
                        "mcp_client_registered",
                        "Client registered in connected clients collection",
                        new {
                            client_endpoint = clientEndpoint,
                            client_key = clientKey,
                            add_result = addResult,
                            total_connected_clients = connectedClients.Count
                        },
                        correlationId,
                        "Client successfully registered for notification broadcasting."
                    );
                    
                    // Initialize new framing components
                    bufferManager = new DynamicBufferManager();
                    messageReassembler = new MessageReassembler(bufferManager);
                    
                    // Start with initial buffer size
                    byte[] buffer = bufferManager.GetBuffer(BufferConfig.INITIAL_BUFFER_SIZE);
                    
                    while (!cancellationToken.IsCancellationRequested && client.Connected)
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                        
                        if (bytesRead == 0)
                        {
                            break; // Client disconnected.
                        }
                        
                        // Add received data to message reassembler
                        messageReassembler.AddData(buffer, bytesRead);
                        
                        // Extract any complete messages
                        string[] completeJsonMessages = messageReassembler.ExtractCompleteMessages();
                        
                        foreach (string requestJson in completeJsonMessages)
                        {
                            if (string.IsNullOrWhiteSpace(requestJson)) continue;
                            
                            // JSON-RPC processing and response sending with client context
                            McpLogger.LogDebug($"[McpBridgeServer] Processing request from {clientEndpoint}: {requestJson}");
                            string responseJson = await JsonRpcProcessor.ProcessRequest(requestJson, clientEndpoint);
                            
                            // Only send response if it's not null (notifications return null)
                            if (!string.IsNullOrEmpty(responseJson))
                            {
                                VibeLogger.LogInfo(
                                    "mcp_response_sending_start",
                                    "Starting response send to client",
                                    new {
                                        client_endpoint = clientEndpoint,
                                        response_length = responseJson.Length,
                                        stream_can_write = stream.CanWrite,
                                        client_connected = client.Connected,
                                        cancellation_requested = cancellationToken.IsCancellationRequested
                                    },
                                    correlationId,
                                    "About to send JSON-RPC response to client. Checking stream state before write."
                                );
                                
                                // Check stream and client state before attempting write
                                if (!stream.CanWrite || !client.Connected || cancellationToken.IsCancellationRequested)
                                {
                                    VibeLogger.LogWarning(
                                        "mcp_response_send_skipped",
                                        "Skipping response send due to invalid stream or client state",
                                        new {
                                            client_endpoint = clientEndpoint,
                                            stream_can_write = stream.CanWrite,
                                            client_connected = client.Connected,
                                            cancellation_requested = cancellationToken.IsCancellationRequested,
                                            reason = !stream.CanWrite ? "stream_not_writable" : 
                                                    !client.Connected ? "client_disconnected" : "cancellation_requested"
                                        },
                                        correlationId,
                                        "Response send skipped to prevent ObjectDisposedException or write to closed connection.",
                                        "This is normal behavior during domain reload or client disconnection."
                                    );
                                    return; // Skip the write operation
                                }
                                
                                // Send response with Content-Length framing
                                string framedResponse = CreateContentLengthFrame(responseJson);
                                byte[] responseData = Encoding.UTF8.GetBytes(framedResponse);
                                
                                try
                                {
                                    await stream.WriteAsync(responseData, 0, responseData.Length, cancellationToken);
                                    
                                    VibeLogger.LogInfo(
                                        "mcp_response_sent_success",
                                        "Response sent successfully to client",
                                        new {
                                            client_endpoint = clientEndpoint,
                                            bytes_sent = responseData.Length,
                                            content_length = Encoding.UTF8.GetByteCount(responseJson)
                                        },
                                        correlationId,
                                        "JSON-RPC response successfully written to NetworkStream."
                                    );
                                }
                                catch (ObjectDisposedException ex)
                                {
                                    VibeLogger.LogError(
                                        "mcp_response_send_disposed_error",
                                        "Cannot write to disposed NetworkStream",
                                        new {
                                            client_endpoint = clientEndpoint,
                                            exception_message = ex.Message,
                                            object_name = ex.ObjectName,
                                            client_connected = client.Connected,
                                            stream_can_write = false, // Stream is disposed
                                            cancellation_requested = cancellationToken.IsCancellationRequested
                                        },
                                        correlationId,
                                        "NetworkStream was disposed while trying to send response. This indicates a connection cleanup race condition.",
                                        "Investigate connection disposal timing during domain reload cycles."
                                    );
                                    throw; // Re-throw to maintain existing error handling
                                }
                            }
                        }
                        
                        // Validate reassembler state and clear if needed
                        if (!messageReassembler.ValidateState())
                        {
                            McpLogger.LogWarning($"[HandleClient] Message reassembler state invalid for client {clientEndpoint}, cleared");
                        }
                    }
                }
            }
            catch (ThreadAbortException)
            {
                // Treat as normal behavior if a domain reload is in progress.
                // No need to log thread aborts during domain reload
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation during server shutdown or domain reload
                // No logging needed as this is expected behavior during Unity Editor operations
            }
            catch (IOException ex)
            {
                // I/O errors are usually normal disconnections - only log as info instead of warning
                if (IsNormalDisconnectionException(ex))
                {
                    // Log normal disconnections as info level
                    McpLogger.LogInfo($"Client {clientEndpoint} disconnected normally");
                }
                else
                {
                    McpLogger.LogWarning($"I/O error with client {clientEndpoint}: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                VibeLogger.LogException(
                    "mcp_client_handling_error",
                    ex,
                    new {
                        client_endpoint = clientEndpoint,
                        client_connected = client?.Connected,
                        exception_type = ex.GetType().Name,
                        domain_reload_in_progress = McpSessionManager.instance.IsDomainReloadInProgress
                    },
                    correlationId,
                    "Unexpected error occurred while handling client connection.",
                    "Analyze the exception type and timing relative to domain reload cycles."
                );
                
                McpLogger.LogError($"Error handling client {clientEndpoint}: {ex.Message}\n{ex.StackTrace}");
                OnError?.Invoke(ex.Message);
            }
            finally
            {
                VibeLogger.LogInfo(
                    "mcp_client_cleanup_start",
                    "Starting client connection cleanup",
                    new {
                        client_endpoint = clientEndpoint,
                        total_connected_clients_before = connectedClients.Count
                    },
                    correlationId,
                    "Client connection handling completed. Starting cleanup process."
                );
                
                // Dispose of framing components
                try
                {
                    messageReassembler?.Dispose();
                    bufferManager?.Dispose();
                }
                catch (Exception ex)
                {
                    VibeLogger.LogWarning(
                        "mcp_framing_components_dispose_error",
                        "Error disposing framing components",
                        new {
                            client_endpoint = clientEndpoint,
                            exception_message = ex.Message
                        },
                        correlationId,
                        "Failed to properly dispose message reassembler or buffer manager."
                    );
                    McpLogger.LogWarning($"Error disposing framing components for client {clientEndpoint}: {ex.Message}");
                }
                
                // Remove client from connected clients list
                // Find client by endpoint to get the correct key
                ConnectedClient clientToRemove = connectedClients.Values
                    .FirstOrDefault(c => c.Endpoint == clientEndpoint);
                    
                bool removeResult = false;
                if (clientToRemove != null)
                {
                    string clientKey = GenerateClientKey(clientToRemove.Endpoint);
                    removeResult = connectedClients.TryRemove(clientKey, out ConnectedClient removedClient);
                    
                    VibeLogger.LogInfo(
                        "mcp_client_removed_from_collection",
                        "Client removed from connected clients collection",
                        new {
                            client_endpoint = clientEndpoint,
                            client_key = clientKey,
                            remove_result = removeResult,
                            total_connected_clients_after = connectedClients.Count
                        },
                        correlationId,
                        "Client connection cleanup completed successfully."
                    );
                }
                else
                {
                    VibeLogger.LogWarning(
                        "mcp_client_not_found_in_collection",
                        "Client not found in connected clients collection during cleanup",
                        new {
                            client_endpoint = clientEndpoint,
                            total_connected_clients = connectedClients.Count
                        },
                        correlationId,
                        "Client was not found in the collection during cleanup. May have been removed concurrently.",
                        "Check for race conditions in client connection management."
                    );
                }
                
                
                client.Close();
                OnClientDisconnected?.Invoke(clientEndpoint);
            }
        }

        /// <summary>
        /// Sends a JSON-RPC notification to all connected clients using Content-Length framing.
        /// </summary>
        /// <param name="method">The notification method name</param>
        /// <param name="parameters">The notification parameters (optional)</param>
        public async Task SendNotificationToClients(string method, object parameters = null)
        {
            if (connectedClients.IsEmpty)
            {
                return;
            }

            // Create JSON-RPC notification
            JsonRpcNotification notification = new JsonRpcNotification("2.0", method, parameters);

            string notificationJson = JsonConvert.SerializeObject(notification);
            
            // Frame the notification with Content-Length header
            string framedNotification = CreateContentLengthFrame(notificationJson);
            byte[] notificationData = Encoding.UTF8.GetBytes(framedNotification);

            await SendNotificationData(notificationData);
        }

        /// <summary>
        /// Sends a pre-formatted JSON-RPC notification to all connected clients using Content-Length framing.
        /// </summary>
        /// <param name="notificationJson">The complete JSON-RPC notification string</param>
        public void SendNotificationToClients(string notificationJson)
        {
            if (connectedClients.IsEmpty)
            {
                return;
            }

            // Frame the notification with Content-Length header
            string framedNotification = CreateContentLengthFrame(notificationJson);
            byte[] notificationData = Encoding.UTF8.GetBytes(framedNotification);
            
            _ = SendNotificationData(notificationData);
        }

        /// <summary>
        /// Send notification data to all connected clients
        /// </summary>
        private async Task SendNotificationData(byte[] notificationData)
        {
            List<string> clientsToRemove = new List<string>();
            
            foreach (KeyValuePair<string, ConnectedClient> client in connectedClients)
            {
                try
                {
                    if (client.Value.Stream?.CanWrite == true)
                    {
                        await client.Value.Stream.WriteAsync(notificationData, 0, notificationData.Length);
                    }
                    else
                    {
                        clientsToRemove.Add(client.Key);
                    }
                }
                catch (Exception ex)
                {
                    // Log the error before removing the client
                    McpLogger.LogWarning($"Error sending notification to client {client.Key}: {ex.Message}");
                    clientsToRemove.Add(client.Key);
                }
            }
            
            // Remove disconnected clients
            foreach (string clientKey in clientsToRemove)
            {
                connectedClients.TryRemove(clientKey, out _);
            }
        }



        /// <summary>
        /// Creates a Content-Length framed message for JSON-RPC 2.0 communication.
        /// </summary>
        /// <param name="jsonContent">The JSON content to frame</param>
        /// <returns>The framed message with Content-Length header</returns>
        private string CreateContentLengthFrame(string jsonContent)
        {
            if (string.IsNullOrEmpty(jsonContent))
            {
                return string.Empty;
            }
            
            // Calculate content length in bytes (UTF-8 encoding)
            int contentLength = Encoding.UTF8.GetByteCount(jsonContent);
            
            // Create the framed message: Content-Length: <n>\r\n\r\n<json_content>
            return $"Content-Length: {contentLength}\r\n\r\n{jsonContent}";
        }

        /// <summary>
        /// Determines if the given exception represents a normal client disconnection.
        /// </summary>
        /// <param name="ex">The exception to evaluate</param>
        /// <returns>True if the exception represents a normal disconnection, false otherwise</returns>
        private static bool IsNormalDisconnectionException(Exception ex)
        {
            switch (ex)
            {
                case SocketException sockEx:
                    return sockEx.SocketErrorCode is SocketError.ConnectionReset or
                                                     SocketError.ConnectionAborted or
                                                     SocketError.OperationAborted or
                                                     SocketError.Shutdown or
                                                     SocketError.NotConnected;
                    
                case ObjectDisposedException:
                    return true;
                    
                case IOException ioEx when ioEx.InnerException is SocketException innerSockEx:
                    return innerSockEx.SocketErrorCode is SocketError.ConnectionReset or
                                                          SocketError.ConnectionAborted or
                                                          SocketError.OperationAborted or
                                                          SocketError.Shutdown or
                                                          SocketError.NotConnected;
                
                case IOException ioEx:
                    // Check HResult codes for common disconnection scenarios
                    return NormalDisconnectionHResults.Contains(ioEx.HResult) ||
                           IsNormalDisconnectionByInnerException(ioEx);
                    
                default:
                    return false;
            }
        }

        /// <summary>
        /// Recursively checks inner exceptions for normal disconnection scenarios
        /// </summary>
        /// <param name="ex">The exception to check</param>
        /// <returns>True if any inner exception indicates a normal disconnection</returns>
        private static bool IsNormalDisconnectionByInnerException(Exception ex)
        {
            Exception innerEx = ex.InnerException;
            while (innerEx != null)
            {
                if (IsNormalDisconnectionException(innerEx))
                {
                    return true;
                }
                innerEx = innerEx.InnerException;
            }
            return false;
        }

        /// <summary>
        /// Releases resources.
        /// </summary>
        public void Dispose()
        {
            StopServer();
            cancellationTokenSource?.Dispose();
            tcpListener = null;
            serverTask = null;
        }
    }
} 