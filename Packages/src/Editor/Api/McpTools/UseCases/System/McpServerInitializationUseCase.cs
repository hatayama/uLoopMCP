using System.Threading.Tasks;
using System.Threading;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// UseCase responsible for temporal cohesion of server initialization processing
    /// Processing sequence: 1. Security validation, 2. HTTP port validation, 3. TCP server startup (auto port), 4. State update
    /// Related classes: McpServerConfigurationService, PortAllocationService, McpServerStartupService, SecurityValidationService
    /// Design reference: @Packages/docs/ARCHITECTURE_Unity.md - UseCase + Tool Pattern (DDD Integration)
    /// </summary>
    public class McpServerInitializationUseCase : AbstractUseCase<ServerInitializationSchema, ServerInitializationResponse>
    {
        private readonly McpServerConfigurationService _configService;
        private readonly SecurityValidationService _securityService;
        private readonly PortAllocationService _portService;
        private readonly McpServerStartupService _startupService;
        private readonly InitializationNotificationService _notificationService;

        public McpServerInitializationUseCase()
        {
            _configService = new McpServerConfigurationService();
            _securityService = new SecurityValidationService();
            _portService = new PortAllocationService();
            _startupService = new McpServerStartupService();
            _notificationService = new InitializationNotificationService();
        }

        public McpServerInitializationUseCase(
            McpServerConfigurationService configService,
            SecurityValidationService securityService,
            PortAllocationService portService,
            McpServerStartupService startupService,
            InitializationNotificationService notificationService)
        {
            _configService = configService ?? throw new System.ArgumentNullException(nameof(configService));
            _securityService = securityService ?? throw new System.ArgumentNullException(nameof(securityService));
            _portService = portService ?? throw new System.ArgumentNullException(nameof(portService));
            _startupService = startupService ?? throw new System.ArgumentNullException(nameof(startupService));
            _notificationService = notificationService ?? throw new System.ArgumentNullException(nameof(notificationService));
        }

        /// <summary>
        /// Execute server initialization processing
        /// </summary>
        /// <param name="parameters">Initialization parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Initialization result</returns>
        public override Task<ServerInitializationResponse> ExecuteAsync(ServerInitializationSchema parameters, CancellationToken cancellationToken)
        {
            ServerInitializationResponse response = new();
            System.DateTime startTime = System.DateTime.UtcNow;

            try
            {
                // 1. Security validation - SecurityValidationService
                ValidationResult editorStateValidation = _securityService.ValidateEditorState();
                if (!editorStateValidation.IsValid)
                {
                    response.Success = false;
                    response.Message = editorStateValidation.ErrorMessage;
                    return Task.FromResult(response);
                }

                // 2. HTTP port validation (for MCP client connections)
                int httpPort = McpEditorSettings.GetHttpPort();
                ServiceResult<bool> validationResult = _configService.ValidateConfiguration(httpPort);
                if (!validationResult.Success)
                {
                    _notificationService.ShowInvalidPortDialog(httpPort);
                    response.Success = false;
                    response.Message = validationResult.ErrorMessage;
                    return Task.FromResult(response);
                }

                ValidationResult portSecurityValidation = _securityService.ValidatePortSecurity(httpPort);
                if (!portSecurityValidation.IsValid)
                {
                    response.Success = false;
                    response.Message = portSecurityValidation.ErrorMessage;
                    return Task.FromResult(response);
                }

                // 3. HTTP port availability check
                ServiceResult<int> availablePortResult = _portService.FindAvailablePort(httpPort);
                if (!availablePortResult.Success)
                {
                    response.Success = false;
                    response.Message = availablePortResult.ErrorMessage;
                    return Task.FromResult(response);
                }
                int availableHttpPort = availablePortResult.Data;

                // Handle HTTP port conflict
                if (availableHttpPort != httpPort)
                {
                    ServiceResult<bool> conflictResult = _portService.HandlePortConflict(httpPort, availableHttpPort);
                    if (!conflictResult.Success || !conflictResult.Data)
                    {
                        response.Success = false;
                        response.Message = "Port conflict resolution cancelled by user";
                        return Task.FromResult(response);
                    }
                    // Update HTTP port setting if user accepted alternative
                    McpEditorSettings.SetHttpPort(availableHttpPort);
                }

                // 4. TCP Server startup (auto port allocation by OS)
                ServiceResult<McpBridgeServer> serverResult = _startupService.StartServer();
                if (!serverResult.Success)
                {
                    response.Success = false;
                    response.Message = serverResult.ErrorMessage;
                    return Task.FromResult(response);
                }
                McpBridgeServer serverInstance = serverResult.Data;
                int tcpPort = serverInstance.Port;

                // 5. Session state update (store TCP port for reference)
                ServiceResult<bool> sessionUpdateResult = _startupService.UpdateSessionState(true, tcpPort);
                if (!sessionUpdateResult.Success)
                {
                    response.Success = false;
                    response.Message = sessionUpdateResult.ErrorMessage;
                    return Task.FromResult(response);
                }

                // Success response
                response.Success = true;
                response.ServerPort = tcpPort;
                response.IsRunning = true;
                response.ServerInstance = serverInstance;
                response.Message = $"Server initialized. TCP port: {tcpPort}, HTTP port: {availableHttpPort}";

                return Task.FromResult(response);
            }
            catch (System.Exception ex)
            {
                // Log the full exception for debugging
                UnityEngine.Debug.LogError($"Server initialization failed: {ex}");

                response.Success = false;
                response.Message = "Server initialization failed. Please check the logs for details.";
                return Task.FromResult(response);
            }
            finally
            {
                response.SetTimingInfo(startTime, System.DateTime.UtcNow);
            }
        }
    }
}