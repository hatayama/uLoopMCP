using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    public class McpServerRecoveryFallbackTests
    {
        private bool _originalIsServerRunning;
        private int _originalServerPort;
        private int _originalCustomPort;
        private bool _originalHasCompletedFirstLaunch;
        private bool _originalIsAfterCompile;

        [SetUp]
        public void SetUp()
        {
            _originalIsServerRunning = McpEditorSettings.GetIsServerRunning();
            _originalServerPort = McpEditorSettings.GetServerPort();
            _originalCustomPort = McpEditorSettings.GetCustomPort();
            _originalHasCompletedFirstLaunch = McpEditorSettings.GetHasCompletedFirstLaunch();
            _originalIsAfterCompile = McpEditorSettings.GetIsAfterCompile();

            McpBridgeServer currentServer = McpServerController.CurrentServer;
            if (currentServer != null)
            {
                currentServer.Dispose();
            }
        }

        [TearDown]
        public void TearDown()
        {
            McpBridgeServer currentServer = McpServerController.CurrentServer;
            if (currentServer != null)
            {
                currentServer.Dispose();
            }

            McpEditorSettings.SetIsServerRunning(_originalIsServerRunning);
            McpEditorSettings.SetServerPort(_originalServerPort);
            McpEditorSettings.SetCustomPort(_originalCustomPort);
            McpEditorSettings.SetHasCompletedFirstLaunch(_originalHasCompletedFirstLaunch);
            McpEditorSettings.SetIsAfterCompile(_originalIsAfterCompile);
            McpEditorSettings.ClearReconnectingFlags();
            McpEditorSettings.ClearPostCompileReconnectingUI();
        }

        [Test]
        public async Task StartRecoveryIfNeededAsync_ShouldUseFallbackPort_WhenSavedPortIsInUse()
        {
            CancellationToken ct = CancellationToken.None;
            await WaitForStartupProtectionToClearAsync(ct);

            TcpListener blockedPortListener = new TcpListener(IPAddress.Loopback, 0);
            try
            {
                blockedPortListener.Start();

                int blockedPort = ((IPEndPoint)blockedPortListener.LocalEndpoint).Port;
                int expectedFallbackPort = FindExpectedFallbackPort(blockedPort);
                Assert.AreNotEqual(blockedPort, expectedFallbackPort, "Fallback port should be different from the blocked port.");

                await McpServerController.StartRecoveryIfNeededAsync(blockedPort, false, ct);

                Assert.IsTrue(McpServerController.IsServerRunning, "Server should be running after recovery.");
                Assert.AreEqual(expectedFallbackPort, McpServerController.ServerPort, "Recovery should use fallback port.");
                Assert.AreEqual(expectedFallbackPort, McpEditorSettings.GetServerPort(), "Session state should persist fallback port.");
            }
            finally
            {
                blockedPortListener.Stop();
            }
        }

        [Test]
        public async Task StartRecoveryIfNeededAsync_ShouldUseSavedPort_WhenSavedPortIsAvailable()
        {
            CancellationToken ct = CancellationToken.None;
            await WaitForStartupProtectionToClearAsync(ct);

            int availablePort = GetFreePort();
            await McpServerController.StartRecoveryIfNeededAsync(availablePort, false, ct);

            Assert.IsTrue(McpServerController.IsServerRunning, "Server should be running after recovery.");
            Assert.AreEqual(availablePort, McpServerController.ServerPort, "Recovery should keep saved port when it is available.");
            Assert.AreEqual(availablePort, McpEditorSettings.GetServerPort(), "Session state should persist saved port.");
        }

        private static int GetFreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            try
            {
                listener.Start();
                int freePort = ((IPEndPoint)listener.LocalEndpoint).Port;
                return freePort;
            }
            finally
            {
                listener.Stop();
            }
        }

        private static int FindExpectedFallbackPort(int currentPort)
        {
            const int maxAttempts = 10;

            for (int offset = 1; offset <= maxAttempts; offset++)
            {
                int candidatePort = currentPort + offset;
                if (!McpPortValidator.ValidatePort(candidatePort, "for test fallback"))
                {
                    continue;
                }

                if (NetworkUtility.IsPortInUse(candidatePort))
                {
                    continue;
                }

                return candidatePort;
            }

            Assert.Fail($"No fallback port found within {maxAttempts} attempts from {currentPort}.");
            return currentPort;
        }

        private static async Task WaitForStartupProtectionToClearAsync(CancellationToken ct)
        {
            const int timeoutMs = 7000;
            const int delayMs = 100;
            int elapsedMs = 0;

            while (McpServerController.IsStartupProtectionActive() && elapsedMs < timeoutMs)
            {
                await Task.Delay(delayMs, ct);
                elapsedMs += delayMs;
            }

            Assert.IsFalse(McpServerController.IsStartupProtectionActive(), "Startup protection should be inactive before recovery test starts.");
        }
    }
}
