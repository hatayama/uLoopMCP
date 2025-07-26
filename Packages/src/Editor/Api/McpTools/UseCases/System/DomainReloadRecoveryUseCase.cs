using System.Threading.Tasks;
using System.Threading;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// UseCase responsible for temporal cohesion of Domain Reload recovery processing
    /// Processing sequence: 1. Pre-stop processing, 2. Recovery processing, 3. Notification processing
    /// Related classes: DomainReloadDetectionService, SessionRecoveryService, ClientNotificationService
    /// Design reference: @Packages/docs/ARCHITECTURE_Unity.md - UseCase + Tool Pattern (DDD Integration)
    /// </summary>
    public class DomainReloadRecoveryUseCase
    {
        /// <summary>
        /// Execute processing before Domain Reload starts
        /// </summary>
        /// <param name="currentServer">Current server instance</param>
        /// <returns>Processing result</returns>
        public ServiceResult<string> ExecuteBeforeDomainReload(McpBridgeServer currentServer)
        {
            // 1. Generate tracking ID for related operations
            string correlationId = VibeLogger.GenerateCorrelationId();

            // 2. Check server state
            bool serverRunning = currentServer?.IsRunning ?? false;
            int? serverPort = currentServer?.Port;

            // 3. Detect and record Domain Reload start
            DomainReloadDetectionService.StartDomainReload(correlationId, serverRunning, serverPort);

            // 4. If server is running, execute stop processing
            if (currentServer?.IsRunning == true)
            {
                int portToSave = currentServer.Port;
                
                try
                {
                    // 4.1. Notify client of server stop
                    ClientNotificationService.LogServerStoppingBeforeDomainReload(correlationId, portToSave);

                    // 4.2. Stop server
                    currentServer.Dispose();

                    // 4.3. Notify client of stop completion
                    ClientNotificationService.LogServerStoppedAfterDomainReload(correlationId);

                    return ServiceResult<string>.SuccessResult(correlationId);
                }
                catch (System.Exception ex)
                {
                    // 4.4. Error notification
                    ClientNotificationService.LogServerShutdownError(correlationId, ex, portToSave);

                    // Server stop failure is a critical error (causes port conflicts)
                    throw new System.InvalidOperationException(
                        $"Failed to properly shutdown MCP server before assembly reload. This may cause port conflicts on restart.", ex);
                }
            }

            return ServiceResult<string>.SuccessResult(correlationId);
        }

        /// <summary>
        /// Execute recovery processing after Domain Reload completion
        /// </summary>
        /// <returns>Processing result</returns>
        public async Task<ServiceResult<string>> ExecuteAfterDomainReloadAsync()
        {
            // 1. Generate tracking ID for related operations
            string correlationId = VibeLogger.GenerateCorrelationId();

            // 2. Record Domain Reload completion
            DomainReloadDetectionService.CompleteDomainReload(correlationId);

            // 3. Start timeout for reconnection UI display if needed
            if (DomainReloadDetectionService.ShouldShowReconnectingUI())
            {
                SessionRecoveryService.StartReconnectionUITimeoutAsync().Forget();
            }

            // 4. Update MCP settings to current debug state
            McpDebugStateUpdater.UpdateAllConfigurationsForDebugState();

            // 5. Restore server state
            ValidationResult restoreResult = SessionRecoveryService.RestoreServerStateIfNeeded();
            if (!restoreResult.IsValid)
            {
                return ServiceResult<string>.FailureResult($"Server restoration failed: {restoreResult.ErrorMessage}");
            }

            // 6. Process pending compile requests (currently disabled)
            ProcessPendingCompileRequests();

            // 7. Send tool change notification if server is running
            if (McpServerController.IsServerRunning)
            {
                await ClientNotificationService.SendToolNotificationAfterCompilationAsync();
            }

            return ServiceResult<string>.SuccessResult(correlationId);
        }

        /// <summary>
        /// Process pending compile requests
        /// Note: Currently disabled by feature flag (to avoid main thread errors)
        /// </summary>
        private void ProcessPendingCompileRequests()
        {
            // Feature flag control (currently disabled)
            bool enablePendingCompileProcessing = false; // TODO: Load from config file or editor settings
            
            if (enablePendingCompileProcessing)
            {
                // Planned to be enabled after main thread issue resolution
                // CompileSessionState.StartForcedRecompile();
                VibeLogger.LogInfo(
                    "pending_compile_processing", 
                    "Processing pending compile requests", 
                    correlationId: VibeLogger.GenerateCorrelationId()
                );
            }
            else
            {
                VibeLogger.LogInfo(
                    "pending_compile_processing_disabled", 
                    "Pending compile request processing is disabled via feature flag", 
                    correlationId: VibeLogger.GenerateCorrelationId()
                );
            }
        }
    }
}