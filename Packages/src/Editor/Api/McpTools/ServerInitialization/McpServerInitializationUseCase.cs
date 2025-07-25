using System.Threading.Tasks;
using System.Threading;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// サーバー初期化処理の時間的凝集を担当するUseCase
    /// 処理順序：1. 設定検証, 2. ポート確保, 3. サーバー起動, 4. 状態更新
    /// 関連クラス：McpServerConfigurationService, PortAllocationService, McpServerStartupService, SecurityValidationService
    /// 設計書参照：DDDリファクタリング仕様 - UseCase Layer
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
        /// サーバー初期化処理を実行する
        /// </summary>
        /// <param name="parameters">初期化パラメータ</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <returns>初期化結果</returns>
        public override async Task<ServerInitializationResponse> ExecuteAsync(ServerInitializationSchema parameters, CancellationToken cancellationToken)
        {
            var response = new ServerInitializationResponse();
            var startTime = System.DateTime.UtcNow;

            try
            {
                // 1. 設定検証 - McpServerConfigurationService
                var portResult = _configService.ResolvePort(parameters.Port);
                if (!portResult.Success)
                {
                    response.Success = false;
                    response.Message = portResult.ErrorMessage;
                    return response;
                }
                int actualPort = portResult.Data;

                var validationResult = _configService.ValidateConfiguration(actualPort);
                if (!validationResult.Success)
                {
                    _notificationService.ShowInvalidPortDialog(actualPort);
                    
                    response.Success = false;
                    response.Message = validationResult.ErrorMessage;
                    return response;
                }

                // 2. セキュリティ検証 - SecurityValidationService
                var editorStateValidation = _securityService.ValidateEditorState();
                if (!editorStateValidation.IsValid)
                {
                    response.Success = false;
                    response.Message = editorStateValidation.ErrorMessage;
                    return response;
                }

                var portSecurityValidation = _securityService.ValidatePortSecurity(actualPort);
                if (!portSecurityValidation.IsValid)
                {
                    response.Success = false;
                    response.Message = portSecurityValidation.ErrorMessage;
                    return response;
                }

                // 3. ポート確保 - PortAllocationService
                var availablePortResult = _portService.FindAvailablePort(actualPort);
                if (!availablePortResult.Success)
                {
                    response.Success = false;
                    response.Message = availablePortResult.ErrorMessage;
                    return response;
                }
                int availablePort = availablePortResult.Data;

                // ポート競合の処理
                if (availablePort != actualPort)
                {
                    var conflictResult = _portService.HandlePortConflict(actualPort, availablePort);
                    if (!conflictResult.Success || !conflictResult.Data)
                    {
                        response.Success = false;
                        response.Message = "Port conflict resolution cancelled by user";
                        return response;
                    }
                }

                // 4. サーバー起動 - McpServerStartupService
                var serverResult = _startupService.StartServer(availablePort);
                if (!serverResult.Success)
                {
                    response.Success = false;
                    response.Message = serverResult.ErrorMessage;
                    return response;
                }
                McpBridgeServer serverInstance = serverResult.Data;

                // 5. セッション状態更新
                var sessionUpdateResult = _startupService.UpdateSessionState(true, availablePort);
                if (!sessionUpdateResult.Success)
                {
                    response.Success = false;
                    response.Message = sessionUpdateResult.ErrorMessage;
                    return response;
                }

                // 成功レスポンス
                response.Success = true;
                response.ServerPort = availablePort;
                response.IsRunning = true;
                response.ServerInstance = serverInstance;
                response.Message = "Server initialization completed successfully";

                return response;
            }
            catch (System.Exception ex)
            {
                response.Success = false;
                response.Message = $"Server initialization failed: {ex.Message}";
                return response;
            }
            finally
            {
                response.SetTimingInfo(startTime, System.DateTime.UtcNow);
            }
        }
    }
}