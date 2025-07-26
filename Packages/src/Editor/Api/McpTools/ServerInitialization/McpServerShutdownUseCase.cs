using System.Threading.Tasks;
using System.Threading;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// UseCase responsible for temporal cohesion of server shutdown processing
    /// Processing sequence: 1. Server stop, 2. Session state clear, 3. Resource disposal
    /// Related classes: McpServerStartupService, McpSessionManager
    /// Design reference: @Packages/docs/ARCHITECTURE_Unity.md - UseCase + Tool Pattern (DDD Integration)
    /// </summary>
    public class McpServerShutdownUseCase : AbstractUseCase<ServerShutdownSchema, ServerShutdownResponse>
    {
        private readonly McpServerStartupService _startupService;

        public McpServerShutdownUseCase(McpServerStartupService startupService)
        {
            _startupService = startupService ?? throw new System.ArgumentNullException(nameof(startupService));
        }
        /// <summary>
        /// Execute server shutdown processing
        /// </summary>
        /// <param name="parameters">Shutdown parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Shutdown result</returns>
        public override async Task<ServerShutdownResponse> ExecuteAsync(ServerShutdownSchema parameters, CancellationToken cancellationToken)
        {
            var response = new ServerShutdownResponse();
            var startTime = System.DateTime.UtcNow;

            try
            {
                // 1. Get current server instance
                McpBridgeServer currentServer = McpServerController.CurrentServer;
                if (currentServer == null)
                {
                    response.Success = true;
                    response.Message = "Server was not running";
                    return response;
                }

                // 2. Server stop processing - McpServerStartupService
                var stopResult = _startupService.StopServer(currentServer);
                if (!stopResult.Success)
                {
                    response.Success = false;
                    response.Message = stopResult.ErrorMessage;
                    return response;
                }

                // 3. Session state clear
                var sessionUpdateResult = _startupService.UpdateSessionState(false, 0);
                if (!sessionUpdateResult.Success)
                {
                    response.Success = false;
                    response.Message = sessionUpdateResult.ErrorMessage;
                    return response;  
                }

                // 4. Session clear with SessionManager
                McpSessionManager sessionManager = McpSessionManager.instance;
                sessionManager.ClearServerSession();

                // Success response
                response.Success = true;
                response.Message = "Server shutdown completed successfully";
            }
            catch (System.Exception ex)
            {
                response.Success = false;
                response.Message = $"Server shutdown failed: {ex.Message}";
            }
            finally
            {
                response.SetTimingInfo(startTime, System.DateTime.UtcNow);
            }

            return response;
        }
    }
}