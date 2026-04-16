using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace io.github.hatayama.uLoopMCP
{
    public class SessionRecoveryServiceTests
    {
        private bool _originalIsAfterCompile;
        private bool _originalIsServerRunning;
        private int _originalCustomPort;
        private bool _startedServerInTest;

        [SetUp]
        public void SetUp()
        {
            _originalIsAfterCompile = McpEditorSettings.GetIsAfterCompile();
            _originalIsServerRunning = McpEditorSettings.GetIsServerRunning();
            _originalCustomPort = McpEditorSettings.GetCustomPort();
            _startedServerInTest = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ServerStartingLockService.DeleteLockFile();

            if (_startedServerInTest)
            {
                McpServerController.StopServer();
                yield return WaitForCondition(
                    () => !McpServerController.IsServerRunning,
                    "Temporary MCP server should stop during teardown.");
            }

            McpEditorSettings.SetIsAfterCompile(_originalIsAfterCompile);
            McpEditorSettings.SetIsServerRunning(_originalIsServerRunning);
            McpEditorSettings.SetCustomPort(_originalCustomPort);
        }

        [UnityTest]
        public IEnumerator RestoreServerStateIfNeeded_WhenCurrentServerIsAlreadyRunning_ShouldPreserveExistingStartupLock()
        {
            if (!McpServerController.IsServerRunning)
            {
                int port = GetFreePort();
                McpServerController.StartServer(port);
                _startedServerInTest = true;

                yield return WaitForCondition(
                    () => McpServerController.IsServerRunning,
                    "MCP server should be running before session recovery executes.");
                yield return WaitForCondition(
                    () => !File.Exists(GetServerStartingLockPath()),
                    "Startup prewarm should release its own serverstarting.lock before the test creates a new generation.");
            }

            string createdToken = ServerStartingLockService.CreateLockFile();
            string lockPath = GetServerStartingLockPath();

            Assert.That(createdToken, Is.Not.Null.And.Not.Empty);
            Assert.That(File.Exists(lockPath), Is.True);
            Assert.That(File.ReadAllText(lockPath), Is.EqualTo(createdToken));

            ValidationResult result = SessionRecoveryService.RestoreServerStateIfNeeded();

            Assert.That(result.IsValid, Is.True);
            Assert.That(
                File.Exists(lockPath),
                Is.True,
                "Recovery must not delete another startup generation's lock while the current server is already running.");
            Assert.That(File.ReadAllText(lockPath), Is.EqualTo(createdToken));
        }

        private static IEnumerator WaitForCondition(System.Func<bool> predicate, string failureMessage)
        {
            const int maxFrames = 600;

            for (int frame = 0; frame < maxFrames; frame++)
            {
                if (predicate())
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(failureMessage);
        }

        private static int GetFreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static string GetServerStartingLockPath()
        {
            return Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Temp", "serverstarting.lock"));
        }
    }
}
