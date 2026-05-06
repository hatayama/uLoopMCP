using NUnit.Framework;

namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Tests for DomainReloadRecoveryUseCase session state fallback functionality.
    /// Validates that domain reload recovery works correctly even when server instance is null.
    /// </summary>
    public class DomainReloadRecoveryUseCaseTests
    {
        private bool _originalIsServerRunning;

        [SetUp]
        public void SetUp()
        {
            // Save original session state
            _originalIsServerRunning = UnityCliLoopEditorSettings.GetIsServerRunning();
        }

        [TearDown]
        public void TearDown()
        {
            // Restore original session state
            UnityCliLoopEditorSettings.SetIsServerRunning(_originalIsServerRunning);
            UnityCliLoopEditorSettings.SetIsAfterCompile(false);
            UnityCliLoopEditorSettings.SetIsDomainReloadInProgress(false);
            UnityCliLoopEditorSettings.SetIsReconnecting(false);
            UnityCliLoopEditorSettings.SetShowReconnectingUI(false);
            UnityCliLoopEditorSettings.SetShowPostCompileReconnectingUI(false);

            // Clean up lock file created by ExecuteBeforeDomainReload
            DomainReloadDetectionService.DeleteLockFile();
        }

        [Test]
        public void ExecuteBeforeDomainReload_ShouldUseSessionState_WhenServerInstanceIsNull()
        {
            // Arrange
            UnityCliLoopEditorSettings.SetIsServerRunning(true);

            DomainReloadRecoveryUseCase useCase = new();

            // Act
            ServiceResult<string> result = useCase.ExecuteBeforeDomainReload(null);

            // Assert
            Assert.IsTrue(result.Success, "ExecuteBeforeDomainReload should succeed");
            Assert.IsTrue(UnityCliLoopEditorSettings.GetIsAfterCompile(), "IsAfterCompile should be set to true");
        }

        [Test]
        public void ExecuteBeforeDomainReload_ShouldNotSaveState_WhenBothInstanceAndSessionAreNotRunning()
        {
            // Arrange
            UnityCliLoopEditorSettings.SetIsServerRunning(false);
            UnityCliLoopEditorSettings.SetIsAfterCompile(false);

            DomainReloadRecoveryUseCase useCase = new();

            // Act
            ServiceResult<string> result = useCase.ExecuteBeforeDomainReload(null);

            // Assert
            Assert.IsTrue(result.Success, "ExecuteBeforeDomainReload should succeed");
            Assert.IsFalse(UnityCliLoopEditorSettings.GetIsAfterCompile(), "IsAfterCompile should remain false when server was not running");
        }

        [Test]
        public void ExecuteBeforeDomainReload_ShouldPreferInstanceState_WhenInstanceIsRunning()
        {
            // Arrange
            UnityCliLoopEditorSettings.SetIsServerRunning(true);

            // Create a running server instance
            McpBridgeServer server = null;
            try
            {
                server = new McpBridgeServer();
                server.StartServer();

                DomainReloadRecoveryUseCase useCase = new();

                // Act
                ServiceResult<string> result = useCase.ExecuteBeforeDomainReload(server);

                // Assert
                Assert.IsTrue(result.Success, "ExecuteBeforeDomainReload should succeed");
                Assert.IsFalse(server.IsRunning, "Running server instance should be stopped before domain reload");
            }
            finally
            {
                server?.Dispose();
            }
        }
    }
}
