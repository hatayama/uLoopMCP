using NUnit.Framework;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Tests for DomainReloadRecoveryUseCase session state fallback functionality.
    /// Validates that domain reload recovery works correctly even when server instance is null.
    /// </summary>
    public class DomainReloadRecoveryUseCaseTests
    {
        private bool _originalIsServerRunning;
        private int _originalServerPort;

        [SetUp]
        public void SetUp()
        {
            // Save original session state
            _originalIsServerRunning = McpEditorSettings.GetIsServerRunning();
            _originalServerPort = McpEditorSettings.GetServerPort();
        }

        [TearDown]
        public void TearDown()
        {
            // Restore original session state
            McpEditorSettings.SetIsServerRunning(_originalIsServerRunning);
            McpEditorSettings.SetServerPort(_originalServerPort);
            McpEditorSettings.SetIsAfterCompile(false);
            McpEditorSettings.SetIsDomainReloadInProgress(false);
            McpEditorSettings.SetIsReconnecting(false);
            McpEditorSettings.SetShowReconnectingUI(false);
            McpEditorSettings.SetShowPostCompileReconnectingUI(false);

            // Clean up lock file created by ExecuteBeforeDomainReload
            DomainReloadDetectionService.DeleteLockFile();
        }

        [Test]
        public void ExecuteBeforeDomainReload_ShouldUseSessionState_WhenServerInstanceIsNull()
        {
            // Arrange
            int expectedPort = 7499;
            McpEditorSettings.SetIsServerRunning(true);
            McpEditorSettings.SetServerPort(expectedPort);

            DomainReloadRecoveryUseCase useCase = new();

            // Act
            ServiceResult<string> result = useCase.ExecuteBeforeDomainReload(null);

            // Assert
            Assert.IsTrue(result.Success, "ExecuteBeforeDomainReload should succeed");
            Assert.IsTrue(McpEditorSettings.GetIsAfterCompile(), "IsAfterCompile should be set to true");
            Assert.AreEqual(expectedPort, McpEditorSettings.GetServerPort(), "Server port should be preserved from session state");
        }

        [Test]
        public void ExecuteBeforeDomainReload_ShouldNotSaveState_WhenBothInstanceAndSessionAreNotRunning()
        {
            // Arrange
            McpEditorSettings.SetIsServerRunning(false);
            McpEditorSettings.SetServerPort(7400);
            McpEditorSettings.SetIsAfterCompile(false);

            DomainReloadRecoveryUseCase useCase = new();

            // Act
            ServiceResult<string> result = useCase.ExecuteBeforeDomainReload(null);

            // Assert
            Assert.IsTrue(result.Success, "ExecuteBeforeDomainReload should succeed");
            Assert.IsFalse(McpEditorSettings.GetIsAfterCompile(), "IsAfterCompile should remain false when server was not running");
        }

        [Test]
        public void ExecuteBeforeDomainReload_ShouldPreferInstanceState_WhenInstanceIsRunning()
        {
            // Arrange
            int sessionPort = 7500;
            int instancePort = 7501;
            McpEditorSettings.SetIsServerRunning(true);
            McpEditorSettings.SetServerPort(sessionPort);

            // Create a running server instance
            McpBridgeServer server = null;
            try
            {
                server = new McpBridgeServer();
                server.StartServer(instancePort);

                DomainReloadRecoveryUseCase useCase = new();

                // Act
                ServiceResult<string> result = useCase.ExecuteBeforeDomainReload(server);

                // Assert
                Assert.IsTrue(result.Success, "ExecuteBeforeDomainReload should succeed");
                Assert.AreEqual(instancePort, McpEditorSettings.GetServerPort(),
                    "Server port should be from running instance, not session state");
            }
            finally
            {
                server?.Dispose();
            }
        }
    }
}
