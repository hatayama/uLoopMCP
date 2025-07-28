using System;
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Client Registration Schema for temporal cohesion of client connection and registration
    /// </summary>
    public class ClientRegistrationSchema : BaseToolSchema
    {
        /// <summary>
        /// Name of the MCP client tool
        /// </summary>
        public string ClientName { get; set; } = McpConstants.UNKNOWN_CLIENT_NAME;

        /// <summary>
        /// Client endpoint (TCP connection identifier)
        /// </summary>
        public string ClientEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Notification port for push notifications (optional)
        /// </summary>
        public int? NotificationPort { get; set; }
    }

    /// <summary>
    /// Client Registration Response
    /// </summary>
    public class ClientRegistrationResponse : BaseToolResponse
    {
        /// <summary>
        /// Success status message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Registered client name
        /// </summary>
        public string ClientName { get; set; } = string.Empty;

        /// <summary>
        /// Client endpoint
        /// </summary>
        public string ClientEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Whether notification port was updated
        /// </summary>
        public bool NotificationPortUpdated { get; set; }

        public ClientRegistrationResponse()
        {
        }

        public ClientRegistrationResponse(string message, string clientName, string clientEndpoint, bool notificationPortUpdated)
        {
            Message = message;
            ClientName = clientName;
            ClientEndpoint = clientEndpoint;
            NotificationPortUpdated = notificationPortUpdated;
        }
    }

    /// <summary>
    /// UseCase responsible for temporal cohesion of client registration processing
    /// Processing sequence: 1. Validation, 2. Client entry registration, 3. Notification port update, 4. UI notification
    /// 
    /// Design document reference: Packages/src/Editor/ARCHITECTURE.md
    /// 
    /// Related classes:
    /// - ConnectedToolsMonitoringService: Core service for client management
    /// - McpBridgeServer: TCP server that manages connections
    /// - SetClientNameTool: Tool that delegates to this UseCase
    /// 
    /// Key features:
    /// - Encapsulates temporal cohesion (time-ordered processing steps)
    /// - Ensures proper sequence: validate → register → update port → notify
    /// - Eliminates chicken-and-egg problems in client registration
    /// - Single responsibility: orchestrate client registration workflow
    /// </summary>
    public class ClientRegistrationUseCase : AbstractUseCase<ClientRegistrationSchema, ClientRegistrationResponse>
    {
        /// <summary>
        /// Execute client registration processing with proper temporal cohesion
        /// </summary>
        /// <param name="parameters">Registration parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Registration result</returns>
        public override Task<ClientRegistrationResponse> ExecuteAsync(ClientRegistrationSchema parameters, CancellationToken cancellationToken)
        {
            VibeLogger.LogInfo(
                "client_registration_usecase_started",
                "Client registration UseCase started",
                new { parameters.ClientName, parameters.ClientEndpoint, parameters.NotificationPort }
            );

            try
            {
                // Step 1: Validate input parameters
                var validationResult = ValidateParameters(parameters);
                if (!validationResult.IsValid)
                {
                    return Task.FromResult(new ClientRegistrationResponse(
                        validationResult.ErrorMessage, 
                        parameters.ClientName, 
                        parameters.ClientEndpoint, 
                        false
                    ));
                }

                // Step 2: Register client entry in monitoring service
                RegisterClientEntry(parameters.ClientName, parameters.ClientEndpoint);

                // Step 3: Update notification port if provided
                bool notificationPortUpdated = false;
                if (parameters.NotificationPort.HasValue)
                {
                    notificationPortUpdated = UpdateNotificationPort(parameters.ClientEndpoint, parameters.NotificationPort.Value);
                }

                // Step 4: Update McpBridgeServer for backward compatibility
                UpdateLegacyServerEntry(parameters.ClientEndpoint, parameters.ClientName);

                // Step 5: Success response
                string successMessage = string.Format(McpConstants.CLIENT_SUCCESS_MESSAGE_TEMPLATE, parameters.ClientName);
                
                VibeLogger.LogInfo(
                    "client_registration_usecase_completed",
                    "Client registration UseCase completed successfully",
                    new { 
                        parameters.ClientName, 
                        parameters.ClientEndpoint, 
                        notificationPortUpdated 
                    }
                );

                return Task.FromResult(new ClientRegistrationResponse(
                    successMessage, 
                    parameters.ClientName, 
                    parameters.ClientEndpoint, 
                    notificationPortUpdated
                ));
            }
            catch (Exception ex)
            {
                VibeLogger.LogException(
                    "client_registration_usecase_failed",
                    ex,
                    new { parameters.ClientName, parameters.ClientEndpoint }
                );

                return Task.FromResult(new ClientRegistrationResponse(
                    $"Client registration failed: {ex.Message}", 
                    parameters.ClientName, 
                    parameters.ClientEndpoint, 
                    false
                ));
            }
        }

        /// <summary>
        /// Step 1: Validate input parameters
        /// </summary>
        private (bool IsValid, string ErrorMessage) ValidateParameters(ClientRegistrationSchema parameters)
        {
            if (string.IsNullOrEmpty(parameters.ClientName))
            {
                return (false, "Client name cannot be empty");
            }

            if (string.IsNullOrEmpty(parameters.ClientEndpoint))
            {
                return (false, "Client endpoint cannot be empty");
            }

            if (parameters.ClientName == McpConstants.UNKNOWN_CLIENT_NAME)
            {
                return (false, "Client name cannot be 'Unknown Client'");
            }

            return (true, string.Empty);
        }

        /// <summary>
        /// Step 2: Register client entry in ConnectedToolsMonitoringService
        /// </summary>
        private void RegisterClientEntry(string clientName, string clientEndpoint)
        {
            VibeLogger.LogInfo(
                "client_registration_step2_register",
                "Registering client entry in ConnectedToolsMonitoringService",
                new { clientName, clientEndpoint }
            );

            ConnectedToolsMonitoringService.AddOrUpdateTool(
                clientName, 
                clientEndpoint, 
                0, // notification port will be updated in step 3 if provided
                DateTime.Now
            );
        }

        /// <summary>
        /// Step 3: Update notification port if provided
        /// </summary>
        private bool UpdateNotificationPort(string clientEndpoint, int notificationPort)
        {
            VibeLogger.LogInfo(
                "client_registration_step3_notification_port",
                "Updating notification port",
                new { clientEndpoint, notificationPort }
            );

            try
            {
                ConnectedToolsMonitoringService.UpdateNotificationPort(clientEndpoint, notificationPort);
                return true;
            }
            catch (Exception ex)
            {
                VibeLogger.LogWarning(
                    "client_registration_notification_port_failed",
                    "Failed to update notification port",
                    new { clientEndpoint, notificationPort, error = ex.Message }
                );
                return false;
            }
        }

        /// <summary>
        /// Step 4: Update McpBridgeServer for backward compatibility
        /// </summary>
        private void UpdateLegacyServerEntry(string clientEndpoint, string clientName)
        {
            VibeLogger.LogInfo(
                "client_registration_step4_legacy_server",
                "Updating legacy McpBridgeServer entry",
                new { clientEndpoint, clientName }
            );

            McpBridgeServer server = McpServerController.CurrentServer;
            if (server != null)
            {
                server.UpdateClientName(clientEndpoint, clientName);
            }
        }
    }
}