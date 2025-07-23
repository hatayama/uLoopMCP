using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Session Recovery UseCase - Encapsulates temporal cohesion for session recovery at Unity startup
    /// 
    /// Design document reference: .kiro/specs/mcp-session-recovery/design.md
    /// 
    /// Related classes:
    /// - McpSessionManager: Session data persistence and retrieval
    /// - ConnectedLLMToolData: UI tool representation
    /// - McpEditorWindow: UI state management
    /// 
    /// This UseCase class follows the single-use pattern:
    /// 1. new() - create instance
    /// 2. execute() - perform all recovery steps in temporal order
    /// 3. instance is discarded after use (not reused)
    /// 
    /// Temporal cohesion benefits:
    /// - All recovery steps are contained in one place
    /// - Clear execution order and dependencies
    /// - Single point of failure handling
    /// - Easy to test and reason about
    /// </summary>
    public class SessionRecoveryUseCase
    {
        private readonly string correlationId;
        private readonly System.Action<List<ConnectedLLMToolData>> updateUIToolsCallback;

        public SessionRecoveryUseCase(System.Action<List<ConnectedLLMToolData>> updateUIToolsCallback)
        {
            this.updateUIToolsCallback = updateUIToolsCallback;
            this.correlationId = VibeLogger.GenerateCorrelationId();
        }

        /// <summary>
        /// Execute complete session recovery process for Unity startup
        /// This method contains all recovery steps in temporal order
        /// Should be called only once per instance
        /// </summary>
        public SessionRecoveryResult Execute()
        {
            VibeLogger.LogInfo(
                "session_recovery_usecase_start",
                "Starting session recovery process for Unity startup",
                new
                {
                    has_ui_callback = updateUIToolsCallback != null
                },
                correlationId,
                "UseCase pattern: Single-use session recovery with temporal cohesion",
                "Track this correlation ID for complete recovery flow"
            );

            try
            {
                // Step 1: Validate session manager availability
                McpSessionManager sessionManager = ValidateSessionManagerAvailability();
                
                if (sessionManager == null)
                {
                    return SessionRecoveryResult.SessionManagerUnavailable();
                }

                // Step 2: Retrieve stored client endpoints from yaml
                var storedEndpoints = RetrieveStoredClientEndpoints(sessionManager);

                if (storedEndpoints.Count == 0)
                {
                    VibeLogger.LogInfo(
                        "session_recovery_usecase_no_stored_data",
                        "No stored client endpoints found - clean startup",
                        new { },
                        correlationId,
                        "UseCase completed - no session data to recover"
                    );

                    return SessionRecoveryResult.NoStoredSession();
                }

                // Step 3: Convert endpoints to UI tool data
                List<ConnectedLLMToolData> recoveredToolData = ConvertEndpointsToToolData(storedEndpoints);

                // Step 4: Update UI with recovered tools
                UpdateUIWithRecoveredTools(recoveredToolData);

                // Step 5: Log recovery success
                LogRecoverySuccess(recoveredToolData);

                VibeLogger.LogInfo(
                    "session_recovery_usecase_success",
                    "Session recovery completed successfully",
                    new
                    {
                        recovered_tools_count = recoveredToolData.Count,
                        tool_names = recoveredToolData.Select(t => t.Name).ToArray()
                    },
                    correlationId,
                    "UseCase completed - session successfully recovered and UI updated"
                );

                // Step 4: Handle domain reload recovery for push client connections
                HandleDomainReloadRecovery();

                return SessionRecoveryResult.Success(recoveredToolData.Count);
            }
            catch (System.Exception error)
            {
                VibeLogger.LogException(
                    "session_recovery_usecase_failure",
                    error,
                    new
                    {
                        error_message = error.Message,
                        error_type = error.GetType().Name
                    },
                    correlationId,
                    "Critical error during session recovery",
                    "Investigate recovery process to ensure proper UI state restoration"
                );

                return SessionRecoveryResult.Error(error.Message, error);
            }
        }

        /// <summary>
        /// Step 1: Validate session manager availability
        /// </summary>
        private McpSessionManager ValidateSessionManagerAvailability()
        {
            VibeLogger.LogDebug(
                "session_recovery_step_1",
                "Validating session manager availability",
                new { },
                correlationId,
                "Step 1: Session manager validation"
            );

            McpSessionManager sessionManager = McpSessionManager.instance;

            if (sessionManager == null)
            {
                VibeLogger.LogError(
                    "session_recovery_step_1_failed",
                    "Session manager is unavailable",
                    new { },
                    correlationId,
                    "Step 1 failed: Cannot proceed without session manager"
                );

                return null;
            }

            VibeLogger.LogDebug(
                "session_recovery_step_1_complete",
                "Session manager validated",
                new { session_manager_available = true },
                correlationId,
                "Step 1 complete: Session manager ready for data retrieval"
            );

            return sessionManager;
        }

        /// <summary>
        /// Step 2: Retrieve stored client endpoints from session data
        /// </summary>
        private List<McpSessionManager.ClientEndpointPair> RetrieveStoredClientEndpoints(McpSessionManager sessionManager)
        {
            VibeLogger.LogDebug(
                "session_recovery_step_2",
                "Retrieving stored client endpoints",
                new { },
                correlationId,
                "Step 2: Session data retrieval"
            );

            // Get all push server endpoints from session data (yaml)
            var storedEndpoints = sessionManager.GetAllPushServerEndpoints();

            VibeLogger.LogDebug(
                "session_recovery_step_2_complete",
                "Stored client endpoints retrieved",
                new
                {
                    endpoints_count = storedEndpoints.Count,
                    client_names = storedEndpoints.Select(e => e.clientName).ToArray()
                },
                correlationId,
                "Step 2 complete: Session data loaded from yaml"
            );

            return storedEndpoints;
        }

        /// <summary>
        /// Step 3: Convert client endpoints to UI tool data
        /// </summary>
        private List<ConnectedLLMToolData> ConvertEndpointsToToolData(List<McpSessionManager.ClientEndpointPair> endpoints)
        {
            VibeLogger.LogDebug(
                "session_recovery_step_3",
                "Converting endpoints to UI tool data",
                new { endpoints_count = endpoints.Count },
                correlationId,
                "Step 3: Data transformation for UI"
            );

            var recoveredTools = new List<ConnectedLLMToolData>();

            foreach (var endpoint in endpoints)
            {
                // Create tool data with current timestamp (since original connection time is not stored)
                var toolData = new ConnectedLLMToolData(
                    endpoint.clientName,
                    endpoint.clientEndpoint,
                    System.DateTime.Now // Use current time as approximate connection time
                );

                recoveredTools.Add(toolData);

                VibeLogger.LogDebug(
                    "session_recovery_tool_converted",
                    "Converted endpoint to tool data",
                    new
                    {
                        client_name = endpoint.clientName,
                        client_endpoint = endpoint.clientEndpoint,
                        push_endpoint = endpoint.pushReceiveServerEndpoint
                    },
                    correlationId,
                    "Tool data created for UI display"
                );
            }

            VibeLogger.LogDebug(
                "session_recovery_step_3_complete",
                "Endpoint conversion completed",
                new
                {
                    converted_tools_count = recoveredTools.Count,
                    tool_names = recoveredTools.Select(t => t.Name).ToArray()
                },
                correlationId,
                "Step 3 complete: UI tool data ready for display"
            );

            return recoveredTools;
        }

        /// <summary>
        /// Step 4: Update UI with recovered tools
        /// </summary>
        private void UpdateUIWithRecoveredTools(List<ConnectedLLMToolData> recoveredTools)
        {
            if (updateUIToolsCallback == null)
            {
                VibeLogger.LogDebug(
                    "session_recovery_step_4_skipped",
                    "No UI update callback provided - skipping",
                    new { },
                    correlationId,
                    "Step 4 skipped: No UI callback available"
                );
                return;
            }

            VibeLogger.LogDebug(
                "session_recovery_step_4",
                "Updating UI with recovered tools",
                new { tools_count = recoveredTools.Count },
                correlationId,
                "Step 4: UI state update"
            );

            try
            {
                updateUIToolsCallback(recoveredTools);

                VibeLogger.LogDebug(
                    "session_recovery_step_4_complete",
                    "UI updated successfully with recovered tools",
                    new { tools_count = recoveredTools.Count },
                    correlationId,
                    "Step 4 complete: UI now displays recovered session tools"
                );
            }
            catch (System.Exception error)
            {
                VibeLogger.LogError(
                    "session_recovery_step_4_error",
                    "Failed to update UI with recovered tools",
                    new
                    {
                        tools_count = recoveredTools.Count,
                        error_message = error.Message
                    },
                    correlationId,
                    "Step 4 error: UI update failed but recovery data is available"
                );

                // Re-throw because UI update failure is critical for this UseCase
                throw;
            }
        }

        /// <summary>
        /// Step 5: Log recovery success for debugging
        /// </summary>
        private void LogRecoverySuccess(List<ConnectedLLMToolData> recoveredTools)
        {
            VibeLogger.LogDebug(
                "session_recovery_step_5",
                "Logging recovery success details",
                new { tools_count = recoveredTools.Count },
                correlationId,
                "Step 5: Recovery completion logging"
            );

            foreach (var tool in recoveredTools)
            {
                Debug.Log($"[uLoopMCP] Session recovery: Restored tool '{tool.Name}' with endpoint '{tool.Endpoint}'");
            }

            VibeLogger.LogDebug(
                "session_recovery_step_5_complete",
                "Recovery success logged",
                new
                {
                    tools_count = recoveredTools.Count,
                    detailed_logging_complete = true
                },
                correlationId,
                "Step 5 complete: Recovery process fully documented"
            );
        }

        /// <summary>
        /// Handle domain reload recovery for push client connections
        /// Migrated from UnityPushConnectionManager to centralize recovery logic
        /// </summary>
        private async void HandleDomainReloadRecovery()
        {
            VibeLogger.LogDebug(
                "session_recovery_domain_reload_start",
                "Starting domain reload recovery for push client connections",
                new { },
                correlationId,
                "Domain reload recovery: Restoring push client connectivity"
            );

            try
            {
                // Initialize push client connection after domain reload
                // This may fail if UnityPushClient is disposed, which is expected during domain reload
                try
                {
                    await UnityPushConnectionManager.RestartPushClientAsync();
                }
                catch (System.ObjectDisposedException ex)
                {
                    VibeLogger.LogDebug(
                        "session_recovery_push_client_disposed",
                        "UnityPushClient was disposed during domain reload - this is expected",
                        new { error_message = ex.Message },
                        correlationId,
                        "Push client will be recreated when needed"
                    );
                }

                // Send domain reload recovery notification if connected
                if (UnityPushConnectionManager.IsPushClientConnected)
                {
                    await UnityPushConnectionManager.SendPushNotificationAsync(
                        PushNotificationSerializer.CreateDomainReloadRecoveredNotification()
                    );

                    VibeLogger.LogInfo(
                        "session_recovery_domain_reload_complete",
                        "Domain reload recovery completed successfully",
                        new { push_client_connected = true },
                        correlationId,
                        "Push client reconnected and recovery notification sent"
                    );
                }
                else
                {
                    VibeLogger.LogDebug(
                        "session_recovery_domain_reload_partial",
                        "Domain reload recovery completed but push client not connected",
                        new { push_client_connected = false },
                        correlationId,
                        "Session recovered - push client will be initialized when LLM connects"
                    );
                }
            }
            catch (System.Exception error)
            {
                VibeLogger.LogError(
                    "session_recovery_domain_reload_error",
                    "Failed during domain reload recovery",
                    new
                    {
                        error_message = error.Message,
                        error_type = error.GetType().Name
                    },
                    correlationId,
                    "Domain reload recovery failed - push client may not be available"
                );
            }
        }
    }

    /// <summary>
    /// Result object for session recovery operation
    /// </summary>
    public class SessionRecoveryResult
    {
        public bool IsSuccess { get; private set; }
        public string ErrorMessage { get; private set; }
        public System.Exception Exception { get; private set; }
        public int RecoveredToolsCount { get; private set; }
        public SessionRecoveryReason Reason { get; private set; }

        private SessionRecoveryResult(bool isSuccess, SessionRecoveryReason reason, string errorMessage = null, System.Exception exception = null, int recoveredToolsCount = 0)
        {
            IsSuccess = isSuccess;
            Reason = reason;
            ErrorMessage = errorMessage;
            Exception = exception;
            RecoveredToolsCount = recoveredToolsCount;
        }

        public static SessionRecoveryResult Success(int recoveredToolsCount)
        {
            return new SessionRecoveryResult(true, SessionRecoveryReason.SessionRecovered, recoveredToolsCount: recoveredToolsCount);
        }

        public static SessionRecoveryResult NoStoredSession()
        {
            return new SessionRecoveryResult(true, SessionRecoveryReason.NoStoredSession);
        }

        public static SessionRecoveryResult SessionManagerUnavailable()
        {
            return new SessionRecoveryResult(false, SessionRecoveryReason.SessionManagerUnavailable);
        }

        public static SessionRecoveryResult Error(string errorMessage, System.Exception exception)
        {
            return new SessionRecoveryResult(false, SessionRecoveryReason.RecoveryError, errorMessage, exception);
        }
    }

    /// <summary>
    /// Enumeration of possible session recovery outcomes
    /// </summary>
    public enum SessionRecoveryReason
    {
        SessionRecovered,
        NoStoredSession,
        SessionManagerUnavailable,
        RecoveryError
    }
}