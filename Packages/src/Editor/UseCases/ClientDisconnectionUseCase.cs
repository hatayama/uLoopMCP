using UnityEngine;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Client Disconnection UseCase - Encapsulates temporal cohesion for client disconnection handling
    /// 
    /// Design document reference: .kiro/specs/mcp-client-disconnection/design.md
    /// 
    /// Related classes:
    /// - McpSessionManager: Session data persistence and cleanup
    /// - McpEditorWindowEventHandler: UI event handling and repaint requests
    /// - McpBridgeServer: Client connection management
    /// 
    /// This UseCase class follows the single-use pattern:
    /// 1. new() - create instance
    /// 2. execute() - perform all disconnection steps in temporal order
    /// 3. instance is discarded after use (not reused)
    /// 
    /// Temporal cohesion benefits:
    /// - All disconnection steps are contained in one place
    /// - Clear execution order and dependencies
    /// - Single point of failure handling
    /// - Easy to test and reason about
    /// </summary>
    public class ClientDisconnectionUseCase
    {
        private readonly string correlationId;
        private readonly string clientEndpoint;
        private readonly System.Action requestRepaintCallback;

        public ClientDisconnectionUseCase(string clientEndpoint, System.Action requestRepaintCallback = null)
        {
            this.clientEndpoint = clientEndpoint;
            this.requestRepaintCallback = requestRepaintCallback;
            this.correlationId = VibeLogger.GenerateCorrelationId();
        }

        /// <summary>
        /// Execute complete client disconnection handling process
        /// This method contains all disconnection steps in temporal order
        /// Should be called only once per instance
        /// </summary>
        public ClientDisconnectionResult Execute()
        {
            VibeLogger.LogInfo(
                "client_disconnection_usecase_start",
                "Starting client disconnection handling process",
                new
                {
                    client_endpoint = clientEndpoint,
                    has_repaint_callback = requestRepaintCallback != null
                },
                correlationId,
                "UseCase pattern: Single-use client disconnection with temporal cohesion",
                "Track this correlation ID for complete disconnection flow"
            );

            try
            {
                // Step 1: Log disconnection event
                LogDisconnectionEvent();

                // Step 2: Validate session manager availability
                var sessionManager = ValidateSessionManagerAvailability();
                
                if (sessionManager == null)
                {
                    return ClientDisconnectionResult.SessionManagerUnavailable();
                }

                // Step 3: Remove push server endpoint from session data
                RemovePushServerEndpoint(sessionManager);

                // Step 4: Verify endpoint removal
                bool removalSuccessful = VerifyEndpointRemoval(sessionManager);

                // Step 5: Request UI repaint if callback provided
                RequestUIRepaintIfProvided();

                VibeLogger.LogInfo(
                    "client_disconnection_usecase_success",
                    "Client disconnection handling completed successfully",
                    new
                    {
                        client_endpoint = clientEndpoint,
                        endpoint_removed = removalSuccessful,
                        ui_repaint_requested = requestRepaintCallback != null
                    },
                    correlationId,
                    "UseCase completed - client properly disconnected and UI updated"
                );

                return ClientDisconnectionResult.Success(removalSuccessful);
            }
            catch (System.Exception error)
            {
                VibeLogger.LogException(
                    "client_disconnection_usecase_failure",
                    error,
                    new
                    {
                        client_endpoint = clientEndpoint,
                        error_message = error.Message,
                        error_type = error.GetType().Name
                    },
                    correlationId,
                    "Critical error during client disconnection handling",
                    "Investigate disconnection process to ensure proper cleanup"
                );

                return ClientDisconnectionResult.Error(error.Message, error);
            }
        }

        /// <summary>
        /// Step 1: Log disconnection event for debugging
        /// </summary>
        private void LogDisconnectionEvent()
        {
            VibeLogger.LogDebug(
                "client_disconnection_step_1",
                "Logging client disconnection event",
                new { client_endpoint = clientEndpoint },
                correlationId,
                "Step 1: Disconnection event logging"
            );

            Debug.Log($"[uLoopMCP] McpEditorWindowEventHandler.OnClientDisconnected called: {clientEndpoint}");
            Debug.Log($"[uLoopMCP] Client disconnected: {clientEndpoint}");

            VibeLogger.LogDebug(
                "client_disconnection_step_1_complete",
                "Disconnection event logged",
                new { client_endpoint = clientEndpoint },
                correlationId,
                "Step 1 complete: Event logged for debugging"
            );
        }

        /// <summary>
        /// Step 2: Validate session manager availability
        /// </summary>
        private McpSessionManager ValidateSessionManagerAvailability()
        {
            VibeLogger.LogDebug(
                "client_disconnection_step_2",
                "Validating session manager availability",
                new { },
                correlationId,
                "Step 2: Session manager validation"
            );

            McpSessionManager sessionManager = McpSessionManager.instance;

            if (sessionManager == null)
            {
                Debug.LogError($"[uLoopMCP] McpSessionManager instance is null, cannot remove endpoint: {clientEndpoint}");

                VibeLogger.LogError(
                    "client_disconnection_step_2_failed",
                    "Session manager is unavailable",
                    new { client_endpoint = clientEndpoint },
                    correlationId,
                    "Step 2 failed: Cannot proceed without session manager"
                );

                return null;
            }

            VibeLogger.LogDebug(
                "client_disconnection_step_2_complete",
                "Session manager validated",
                new { session_manager_available = true },
                correlationId,
                "Step 2 complete: Session manager ready for cleanup"
            );

            return sessionManager;
        }

        /// <summary>
        /// Step 3: Remove push server endpoint from session data
        /// </summary>
        private void RemovePushServerEndpoint(McpSessionManager sessionManager)
        {
            VibeLogger.LogDebug(
                "client_disconnection_step_3",
                "Removing push server endpoint from session data",
                new { client_endpoint = clientEndpoint },
                correlationId,
                "Step 3: Session data cleanup"
            );

            Debug.Log($"[uLoopMCP] Attempting to remove push server endpoint for: {clientEndpoint}");

            // This is the critical operation that removes the client from SessionData.yaml
            sessionManager.RemovePushServerEndpoint(clientEndpoint);

            Debug.Log($"[uLoopMCP] Removed push server endpoint for: {clientEndpoint}");

            VibeLogger.LogDebug(
                "client_disconnection_step_3_complete",
                "Push server endpoint removed",
                new { client_endpoint = clientEndpoint },
                correlationId,
                "Step 3 complete: Client endpoint removed from session data"
            );
        }

        /// <summary>
        /// Step 4: Verify endpoint removal was successful
        /// </summary>
        private bool VerifyEndpointRemoval(McpSessionManager sessionManager)
        {
            VibeLogger.LogDebug(
                "client_disconnection_step_4",
                "Verifying endpoint removal",
                new { client_endpoint = clientEndpoint },
                correlationId,
                "Step 4: Removal verification"
            );

            // Check if the endpoint still exists in session data
            string remainingEndpoint = sessionManager.GetPushServerEndpoint(clientEndpoint);
            bool wasRemoved = remainingEndpoint == null;

            VibeLogger.LogDebug(
                "client_disconnection_step_4_complete",
                "Endpoint removal verified",
                new
                {
                    client_endpoint = clientEndpoint,
                    was_removed = wasRemoved,
                    remaining_endpoint = remainingEndpoint
                },
                correlationId,
                "Step 4 complete: Removal status confirmed"
            );

            if (!wasRemoved)
            {
                VibeLogger.LogWarning(
                    "client_disconnection_removal_incomplete",
                    "Client endpoint was not properly removed",
                    new
                    {
                        client_endpoint = clientEndpoint,
                        remaining_endpoint = remainingEndpoint
                    },
                    correlationId,
                    "Warning: Endpoint removal may have failed - investigate session data consistency"
                );
            }

            return wasRemoved;
        }

        /// <summary>
        /// Step 5: Request UI repaint if callback provided
        /// </summary>
        private void RequestUIRepaintIfProvided()
        {
            if (requestRepaintCallback == null)
            {
                VibeLogger.LogDebug(
                    "client_disconnection_step_5_skipped",
                    "No repaint callback provided - skipping",
                    new { },
                    correlationId,
                    "Step 5 skipped: No UI repaint needed"
                );
                return;
            }

            VibeLogger.LogDebug(
                "client_disconnection_step_5",
                "Requesting UI repaint",
                new { },
                correlationId,
                "Step 5: UI repaint request"
            );

            try
            {
                requestRepaintCallback();

                VibeLogger.LogDebug(
                    "client_disconnection_step_5_complete",
                    "UI repaint requested successfully",
                    new { },
                    correlationId,
                    "Step 5 complete: UI will be updated to reflect disconnection"
                );
            }
            catch (System.Exception error)
            {
                VibeLogger.LogError(
                    "client_disconnection_step_5_error",
                    "Failed to request UI repaint",
                    new
                    {
                        error_message = error.Message
                    },
                    correlationId,
                    "Step 5 error: UI repaint failed but disconnection continues"
                );

                // Don't throw - UI repaint failure shouldn't fail the entire disconnection
            }
        }

        /// <summary>
        /// Execute disconnection process on main thread using EditorApplication.delayCall
        /// This is a convenience method that wraps the UseCase execution in the proper thread context
        /// </summary>
        public static void ExecuteOnMainThread(string clientEndpoint, System.Action requestRepaintCallback = null)
        {
            EditorApplication.delayCall += () =>
            {
                ClientDisconnectionUseCase useCase = new(clientEndpoint, requestRepaintCallback);
                ClientDisconnectionResult result = useCase.Execute();
                
                // Result is already logged by the UseCase - no additional action needed
                // The calling code can check the result if needed for additional handling
            };
        }
    }

    /// <summary>
    /// Result object for client disconnection operation
    /// </summary>
    public class ClientDisconnectionResult
    {
        public bool IsSuccess { get; private set; }
        public string ErrorMessage { get; private set; }
        public System.Exception Exception { get; private set; }
        public bool EndpointRemoved { get; private set; }
        public ClientDisconnectionReason Reason { get; private set; }

        private ClientDisconnectionResult(bool isSuccess, ClientDisconnectionReason reason, string errorMessage = null, System.Exception exception = null, bool endpointRemoved = false)
        {
            IsSuccess = isSuccess;
            Reason = reason;
            ErrorMessage = errorMessage;
            Exception = exception;
            EndpointRemoved = endpointRemoved;
        }

        public static ClientDisconnectionResult Success(bool endpointRemoved)
        {
            return new ClientDisconnectionResult(true, ClientDisconnectionReason.DisconnectedSuccessfully, endpointRemoved: endpointRemoved);
        }

        public static ClientDisconnectionResult SessionManagerUnavailable()
        {
            return new ClientDisconnectionResult(false, ClientDisconnectionReason.SessionManagerUnavailable);
        }

        public static ClientDisconnectionResult Error(string errorMessage, System.Exception exception)
        {
            return new ClientDisconnectionResult(false, ClientDisconnectionReason.DisconnectionError, errorMessage, exception);
        }
    }

    /// <summary>
    /// Enumeration of possible client disconnection outcomes
    /// </summary>
    public enum ClientDisconnectionReason
    {
        DisconnectedSuccessfully,
        SessionManagerUnavailable,
        DisconnectionError
    }
}