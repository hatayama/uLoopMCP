using System.Threading.Tasks;
using System.Threading;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// UseCase responsible for temporal cohesion of server initialization processing
    /// Processing sequence: 1. Configuration validation, 2. Port allocation, 3. Server startup, 4. State update
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
            ServerInitializationResponse response = new ServerInitializationResponse();

            try
            {
                bool useProjectIpc = parameters.Port == -1;
                int serverPort = -1;

                if (!useProjectIpc)
                {
                    ServiceResult<int> portResult = _configService.ResolvePort(parameters.Port);
                    if (!portResult.Success)
                    {
                        response.Success = false;
                        response.Message = portResult.ErrorMessage;
                        return Task.FromResult(response);
                    }
                    int actualPort = portResult.Data;

                    ServiceResult<bool> validationResult = _configService.ValidateConfiguration(actualPort);
                    if (!validationResult.Success)
                    {
                        _notificationService.ShowInvalidPortDialog(actualPort);

                        response.Success = false;
                        response.Message = validationResult.ErrorMessage;
                        return Task.FromResult(response);
                    }

                    ValidationResult portSecurityValidation = _securityService.ValidatePortSecurity(actualPort);
                    if (!portSecurityValidation.IsValid)
                    {
                        response.Success = false;
                        response.Message = portSecurityValidation.ErrorMessage;
                        return Task.FromResult(response);
                    }

                    ServiceResult<int> availablePortResult = _portService.FindAvailablePort(actualPort);
                    if (!availablePortResult.Success)
                    {
                        response.Success = false;
                        response.Message = availablePortResult.ErrorMessage;
                        return Task.FromResult(response);
                    }
                    serverPort = availablePortResult.Data;

                    if (serverPort != actualPort)
                    {
                        ServiceResult<bool> conflictResult = _portService.HandlePortConflict(actualPort, serverPort);
                        if (!conflictResult.Success || !conflictResult.Data)
                        {
                            response.Success = false;
                            response.Message = "Port conflict resolution cancelled by user";
                            return Task.FromResult(response);
                        }
                    }
                }

                ValidationResult editorStateValidation = _securityService.ValidateEditorState();
                if (!editorStateValidation.IsValid)
                {
                    response.Success = false;
                    response.Message = editorStateValidation.ErrorMessage;
                    return Task.FromResult(response);
                }

                // 4. Server startup - McpServerStartupService
                ServiceResult<McpBridgeServer> serverResult = _startupService.StartServer(
                    serverPort,
                    !parameters.PreserveStartupLockUntilExplicitRelease);
                if (!serverResult.Success)
                {
                    response.Success = false;
                    response.Message = serverResult.ErrorMessage;
                    return Task.FromResult(response);
                }
                McpBridgeServer serverInstance = serverResult.Data;

                // 5. Session state update
                string projectRootPath = UnityMcpPathResolver.GetProjectRoot();
                ServiceResult<bool> sessionUpdateResult =
                    _startupService.UpdateSessionState(true, serverInstance.Port, projectRootPath);
                if (!sessionUpdateResult.Success)
                {
                    response.Success = false;
                    response.Message = sessionUpdateResult.ErrorMessage;
                    return Task.FromResult(response);
                }

                // Success response
                response.Success = true;
                response.ServerPort = serverInstance.Port;
                response.IsRunning = true;
                response.ServerInstance = serverInstance;
                response.Message = "Server initialization completed successfully";

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
        }
    }
}
