using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    [TestFixture]
    public class CodexConfigTests
    {
        private static readonly Type CodexServiceType = typeof(CodexTomlConfigService);
        private static readonly Type PathResolverType = typeof(UnityMcpPathResolver);
        private static readonly Type ServerConfigFactoryType = typeof(McpServerConfigFactory);

        private static readonly BindingFlags PrivateStatic =
            BindingFlags.NonPublic | BindingFlags.Static;

        // ----------------------------------------------------------------
        // BuildBlock + ReadCurrentValues round-trip
        // ----------------------------------------------------------------

        [Test]
        public void Should_RoundTrip_RelativePath_WithForwardSlashes()
        {
            string inputPath = "Packages/src/TypeScriptServer~/dist/server.bundle.js";
            int inputPort = 12345;

            string toml = InvokeBuildBlock(inputPort, inputPath);
            (string arg0, int? port) = InvokeReadCurrentValues(toml);

            Assert.AreEqual(inputPath, arg0);
            Assert.AreEqual(inputPort, port);
        }

        [Test]
        public void Should_RoundTrip_RelativePath_WithBackslashes()
        {
            string inputPath = @"Packages\src\TypeScriptServer~\dist\server.bundle.js";
            int inputPort = 54321;

            string toml = InvokeBuildBlock(inputPort, inputPath);
            (string arg0, int? port) = InvokeReadCurrentValues(toml);

            Assert.AreEqual(inputPath, arg0);
            Assert.AreEqual(inputPort, port);
        }

        [Test]
        public void Should_RoundTrip_PortNumber()
        {
            string inputPath = "some/relative/path.js";
            int inputPort = 65535;

            string toml = InvokeBuildBlock(inputPort, inputPath);
            (string _, int? port) = InvokeReadCurrentValues(toml);

            Assert.AreEqual(inputPort, port);
        }

        // ----------------------------------------------------------------
        // NormalizeForCompare
        // ----------------------------------------------------------------

        [Test]
        public void NormalizeForCompare_Should_ConvertBackslashToForwardSlash()
        {
            string result = InvokeNormalizeForCompare(@"Packages\src\server.js");

            Assert.AreEqual("Packages/src/server.js", result);
        }

        [Test]
        public void NormalizeForCompare_Should_ReturnNull_WhenInputIsNull()
        {
            string result = InvokeNormalizeForCompare(null);

            Assert.IsNull(result);
        }

        [Test]
        public void NormalizeForCompare_Should_ReturnEmpty_WhenInputIsEmpty()
        {
            string result = InvokeNormalizeForCompare(string.Empty);

            Assert.AreEqual(string.Empty, result);
        }

        // ----------------------------------------------------------------
        // MakeRelativeToConfigurationRoot
        // ----------------------------------------------------------------

        [Test]
        public void MakeRelativeToConfigurationRoot_Should_ReturnRelativePath_WhenUnderRoot()
        {
            string projectRoot = UnityMcpPathResolver.GetProjectRoot();
            string absolutePath = System.IO.Path.Combine(projectRoot, "Packages", "src", "server.js");

            string result = UnityMcpPathResolver.MakeRelativeToConfigurationRoot(absolutePath);

            Assert.AreEqual("Packages/src/server.js", result);
        }

        [Test]
        public void MakeRelativeToConfigurationRoot_Should_ReturnAbsolutePath_WhenOutsideRoot()
        {
            string outsidePath = "/tmp/outside/path/server.js";

            string result = UnityMcpPathResolver.MakeRelativeToConfigurationRoot(outsidePath);

            Assert.AreEqual(outsidePath, result);
        }

        [Test]
        public void MakeRelativeToConfigurationRoot_Should_ReturnInput_WhenNullOrEmpty()
        {
            Assert.IsNull(UnityMcpPathResolver.MakeRelativeToConfigurationRoot(null));
            Assert.AreEqual(string.Empty, UnityMcpPathResolver.MakeRelativeToConfigurationRoot(string.Empty));
        }

        // ----------------------------------------------------------------
        // GetCodexConfigPath / GetCodexConfigDirectory
        // ----------------------------------------------------------------

        [Test]
        public void GetCodexConfigPath_Should_ResolveUnderProjectRoot()
        {
            string configPath = UnityMcpPathResolver.GetCodexConfigPath();
            string projectRoot = UnityMcpPathResolver.GetProjectRoot();
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            Assert.IsTrue(configPath.StartsWith(projectRoot),
                $"Codex config should be under project root, but was: {configPath}");
            Assert.IsFalse(configPath.StartsWith(homeDir) && !projectRoot.StartsWith(homeDir.TrimEnd('/','\\') + "/"),
                "Codex config should not resolve to home directory");
            Assert.IsTrue(configPath.EndsWith(".codex" + System.IO.Path.DirectorySeparatorChar + "config.toml")
                       || configPath.EndsWith(".codex/config.toml"),
                $"Codex config should end with .codex/config.toml, but was: {configPath}");
        }

        [Test]
        public void GetCodexConfigDirectory_Should_ResolveUnderProjectRoot()
        {
            string configDir = UnityMcpPathResolver.GetCodexConfigDirectory();
            string projectRoot = UnityMcpPathResolver.GetProjectRoot();

            Assert.IsTrue(configDir.StartsWith(projectRoot),
                $"Codex config dir should be under project root, but was: {configDir}");
            Assert.IsTrue(configDir.EndsWith(".codex"),
                $"Codex config dir should end with .codex, but was: {configDir}");
        }

        // ----------------------------------------------------------------
        // McpServerConfigFactory - Codex classification
        // ----------------------------------------------------------------

        [Test]
        public void GetServerPathForEditor_Should_ReturnRelativePath_ForCodex()
        {
            string projectRoot = UnityMcpPathResolver.GetProjectRoot();
            string absolutePath = System.IO.Path.Combine(projectRoot, "Packages", "src", "server.js");

            MethodInfo method = ServerConfigFactoryType.GetMethod("GetServerPathForEditor", PrivateStatic);
            Debug.Assert(method != null, "GetServerPathForEditor method not found");

            string result = (string)method.Invoke(null, new object[] { absolutePath, McpEditorType.Codex });

            Assert.AreNotEqual(absolutePath, result,
                "Codex should use relative path, not absolute");
            Assert.AreEqual("Packages/src/server.js", result);
        }

        // ----------------------------------------------------------------
        // Reflection helpers
        // ----------------------------------------------------------------

        private static string InvokeBuildBlock(int port, string serverPath)
        {
            MethodInfo method = CodexServiceType.GetMethod("BuildBlock", PrivateStatic);
            Debug.Assert(method != null, "BuildBlock method not found");
            return (string)method.Invoke(null, new object[] { port, serverPath });
        }

        private static (string arg0, int? port) InvokeReadCurrentValues(string content)
        {
            MethodInfo method = CodexServiceType.GetMethod("ReadCurrentValues", PrivateStatic);
            Debug.Assert(method != null, "ReadCurrentValues method not found");
            object result = method.Invoke(null, new object[] { content });
            // ValueTuple<string, int?> deconstruction
            System.Runtime.CompilerServices.ITuple tuple = (System.Runtime.CompilerServices.ITuple)result;
            return ((string)tuple[0], (int?)tuple[1]);
        }

        private static string InvokeNormalizeForCompare(string path)
        {
            MethodInfo method = CodexServiceType.GetMethod("NormalizeForCompare", PrivateStatic);
            Debug.Assert(method != null, "NormalizeForCompare method not found");
            return (string)method.Invoke(null, new object[] { path });
        }
    }
}
