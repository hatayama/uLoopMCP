using UnityEngine;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Server Startup UseCase - Encapsulates temporal cohesion for server startup process
    /// 
    /// Design document reference: .kiro/specs/mcp-server-startup/design.md
    /// 
    /// Related classes:
    /// - McpBridgeServer: The actual server implementation
    /// - McpPortValidator: Port validation logic
    /// - McpPortChangeUpdater: Configuration update utilities
    /// - McpSessionManager: Session state management
    /// - PushNotificationSerializer: Push notification session management
    /// 
    /// This UseCase class follows the single-use pattern:
    /// 1. new() - create instance
    /// 2. execute() - perform all startup steps in temporal order
    /// 3. instance is discarded after use (not reused)
    /// 
    /// Temporal cohesion benefits:
    /// - All startup steps are contained in one place
    /// - Clear execution order and dependencies
    /// - Single point of failure handling
    /// - Easy to test and reason about
    /// </summary>
    public class ServerStartupUseCase
    {
        private readonly string correlationId;
        private readonly int requestedPort;

        public ServerStartupUseCase(int port = -1)
        {
            this.requestedPort = port == -1 ? McpEditorSettings.GetCustomPort() : port;
            this.correlationId = VibeLogger.GenerateCorrelationId();
        }

        /// <summary>
        /// Execute complete server startup process
        /// This method contains all startup steps in temporal order
        /// Should be called only once per instance
        /// </summary>
        public ServerStartupResult Execute()
        {
            VibeLogger.LogInfo(
                "server_startup_usecase_start",
                $"Starting server startup process for port {requestedPort}",
                new
                {
                    requested_port = requestedPort,
                    current_server_running = McpServerController.IsServerRunning,
                    current_server_port = McpServerController.ServerPort
                },
                correlationId,
                "UseCase pattern: Single-use server startup with temporal cohesion",
                "Track this correlation ID for complete startup flow"
            );

            try
            {
                // Step 1: Validate requested port
                if (!ValidateRequestedPort())
                {
                    return ServerStartupResult.InvalidPort();
                }

                // Step 2: Find available port
                int availablePort = FindAvailablePort();

                // Step 3: Handle port conflict if necessary
                if (!HandlePortConflictIfNecessary(availablePort))
                {
                    return ServerStartupResult.UserCancelled();
                }

                // Step 4: Validate server configuration
                ValidateServerConfiguration(availablePort);

                // Step 5: Stop existing server if running
                StopExistingServerIfRunning();

                // Step 6: Create and start new server
                McpBridgeServer newServer = CreateAndStartNewServer(availablePort);

                // Step 7: Initialize push notification session
                InitializePushNotificationSession();

                // Step 8: Save session state
                SaveSessionState(availablePort);

                VibeLogger.LogInfo(
                    "server_startup_usecase_success",
                    $"Server startup completed successfully on port {availablePort}",
                    new
                    {
                        final_port = availablePort,
                        server_instance_id = newServer.GetHashCode()
                    },
                    correlationId,
                    "UseCase completed - server is now running and ready for connections"
                );

                return ServerStartupResult.Success(availablePort, newServer);
            }
            catch (System.Exception error)
            {
                VibeLogger.LogError(
                    "server_startup_usecase_failure",
                    $"Server startup failed: {error.Message}",
                    new
                    {
                        requested_port = requestedPort,
                        error_message = error.Message,
                        error_type = error.GetType().Name
                    },
                    correlationId,
                    "UseCase failed - server startup aborted with error"
                );

                return ServerStartupResult.Error(error.Message);
            }
        }

        /// <summary>
        /// Step 1: Validate requested port
        /// </summary>
        private bool ValidateRequestedPort()
        {
            VibeLogger.LogDebug(
                "server_startup_step_1",
                "Validating requested port",
                new { port = requestedPort },
                correlationId,
                "Step 1: Port validation - checking if port is valid for server startup"
            );

            if (!McpPortValidator.ValidatePort(requestedPort, "for server startup"))
            {
                EditorUtility.DisplayDialog(
                    "Invalid Port",
                    $"Port {requestedPort} is not valid for server startup.\n\nPort must be 1024 or higher and not a reserved system port.",
                    "OK"
                );

                VibeLogger.LogWarning(
                    "server_startup_step_1_failed",
                    "Port validation failed",
                    new { port = requestedPort },
                    correlationId,
                    "Step 1 failed: Invalid port - user notified via dialog"
                );

                return false;
            }

            VibeLogger.LogDebug(
                "server_startup_step_1_complete",
                "Port validation passed",
                new { port = requestedPort },
                correlationId,
                "Step 1 complete: Port is valid for server startup"
            );

            return true;
        }

        /// <summary>
        /// Step 2: Find available port starting from requested port
        /// </summary>
        private int FindAvailablePort()
        {
            VibeLogger.LogDebug(
                "server_startup_step_2",
                "Finding available port",
                new { starting_port = requestedPort },
                correlationId,
                "Step 2: Port availability check - finding first available port"
            );

            int availablePort = McpServerController.FindAvailablePort(requestedPort);

            VibeLogger.LogDebug(
                "server_startup_step_2_complete",
                "Available port found",
                new 
                { 
                    requested_port = requestedPort,
                    available_port = availablePort,
                    port_changed = availablePort != requestedPort
                },
                correlationId,
                "Step 2 complete: Available port identified"
            );

            return availablePort;
        }

        /// <summary>
        /// Step 3: Handle port conflict if necessary (user confirmation + config update)
        /// </summary>
        private bool HandlePortConflictIfNecessary(int availablePort)
        {
            if (availablePort == requestedPort)
            {
                // No conflict, proceed
                return true;
            }

            VibeLogger.LogDebug(
                "server_startup_step_3",
                "Handling port conflict",
                new 
                { 
                    requested_port = requestedPort,
                    available_port = availablePort
                },
                correlationId,
                "Step 3: Port conflict resolution - requesting user confirmation"
            );

            bool userConfirmed = EditorUtility.DisplayDialog(
                "Port Conflict",
                $"Port {requestedPort} is already in use.\n\nWould you like to use port {availablePort} instead?",
                "OK",
                "Cancel"
            );

            if (!userConfirmed)
            {
                VibeLogger.LogInfo(
                    "server_startup_step_3_cancelled",
                    "User cancelled port conflict resolution",
                    new 
                    { 
                        requested_port = requestedPort,
                        available_port = availablePort
                    },
                    correlationId,
                    "Step 3 cancelled: User chose not to use alternative port"
                );

                return false;
            }

            // Update configurations with new port
            McpPortChangeUpdater.UpdateAllConfigurationsForPortChange(availablePort, "Server port conflict resolution");

            VibeLogger.LogDebug(
                "server_startup_step_3_complete",
                "Port conflict resolved",
                new 
                { 
                    requested_port = requestedPort,
                    final_port = availablePort,
                    configurations_updated = true
                },
                correlationId,
                "Step 3 complete: Port conflict resolved and configurations updated"
            );

            return true;
        }

        /// <summary>
        /// Step 4: Validate server configuration
        /// </summary>
        private void ValidateServerConfiguration(int port)
        {
            VibeLogger.LogDebug(
                "server_startup_step_4",
                "Validating server configuration",
                new { port },
                correlationId,
                "Step 4: Server configuration validation"
            );

            McpServerController.ValidateServerConfiguration(port);

            VibeLogger.LogDebug(
                "server_startup_step_4_complete",
                "Server configuration validated",
                new { port },
                correlationId,
                "Step 4 complete: Server configuration is valid"
            );
        }

        /// <summary>
        /// Step 5: Stop existing server if running
        /// </summary>
        private void StopExistingServerIfRunning()
        {
            bool serverWasRunning = McpServerController.IsServerRunning;

            if (!serverWasRunning)
            {
                return;
            }

            VibeLogger.LogDebug(
                "server_startup_step_5",
                "Stopping existing server",
                new 
                { 
                    current_port = McpServerController.ServerPort,
                    server_instance = McpServerController.CurrentServer?.GetHashCode()
                },
                correlationId,
                "Step 5: Existing server cleanup - stopping current server to release port"
            );

            McpServerController.StopServer();

            VibeLogger.LogDebug(
                "server_startup_step_5_complete",
                "Existing server stopped",
                new { port_released = true },
                correlationId,
                "Step 5 complete: Existing server stopped and port released"
            );
        }

        /// <summary>
        /// Step 6: Create and start new server
        /// </summary>
        private McpBridgeServer CreateAndStartNewServer(int port)
        {
            VibeLogger.LogDebug(
                "server_startup_step_6",
                "Creating and starting new server",
                new { port },
                correlationId,
                "Step 6: New server creation and startup"
            );

            McpBridgeServer newServer = new();
            McpServerController.SetServerInstance(newServer);
            newServer.StartServer(port);

            VibeLogger.LogDebug(
                "server_startup_step_6_complete",
                "New server started successfully",
                new 
                { 
                    port,
                    server_instance_id = newServer.GetHashCode(),
                    is_running = newServer.IsRunning
                },
                correlationId,
                "Step 6 complete: New server is running and accepting connections"
            );

            return newServer;
        }

        /// <summary>
        /// Step 7: Initialize push notification session
        /// </summary>
        private void InitializePushNotificationSession()
        {
            VibeLogger.LogDebug(
                "server_startup_step_7",
                "Initializing push notification session",
                new { },
                correlationId,
                "Step 7: Push notification session setup"
            );

            PushNotificationSerializer.StartNewSession();

            VibeLogger.LogDebug(
                "server_startup_step_7_complete",
                "Push notification session initialized",
                new { },
                correlationId,
                "Step 7 complete: Push notifications ready"
            );
        }

        /// <summary>
        /// Step 8: Save session state
        /// </summary>
        private void SaveSessionState(int port)
        {
            VibeLogger.LogDebug(
                "server_startup_step_8",
                "Saving session state",
                new { port },
                correlationId,
                "Step 8: Session state persistence"
            );

            McpSessionManager sessionManager = McpSessionManager.instance;
            sessionManager.IsServerRunning = true;
            sessionManager.ServerPort = port;

            VibeLogger.LogDebug(
                "server_startup_step_8_complete",
                "Session state saved",
                new 
                { 
                    port,
                    is_server_running = true
                },
                correlationId,
                "Step 8 complete: Session state persisted successfully"
            );
        }
    }

    /// <summary>
    /// Result object for server startup operation
    /// </summary>
    public class ServerStartupResult
    {
        public bool IsSuccess { get; private set; }
        public string ErrorMessage { get; private set; }
        public int Port { get; private set; }
        public McpBridgeServer Server { get; private set; }
        public ServerStartupFailureReason FailureReason { get; private set; }

        private ServerStartupResult(bool isSuccess, string errorMessage, int port, McpBridgeServer server, ServerStartupFailureReason failureReason)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            Port = port;
            Server = server;
            FailureReason = failureReason;
        }

        public static ServerStartupResult Success(int port, McpBridgeServer server)
        {
            return new(true, null, port, server, ServerStartupFailureReason.None);
        }

        public static ServerStartupResult InvalidPort()
        {
            return new(false, "Invalid port specified", -1, null, ServerStartupFailureReason.InvalidPort);
        }

        public static ServerStartupResult UserCancelled()
        {
            return new(false, "User cancelled port conflict resolution", -1, null, ServerStartupFailureReason.UserCancelled);
        }

        public static ServerStartupResult Error(string errorMessage)
        {
            return new(false, errorMessage, -1, null, ServerStartupFailureReason.InternalError);
        }
    }

    /// <summary>
    /// Enumeration of possible failure reasons for server startup
    /// </summary>
    public enum ServerStartupFailureReason
    {
        None,
        InvalidPort,
        UserCancelled,
        InternalError
    }
}