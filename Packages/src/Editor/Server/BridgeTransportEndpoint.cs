using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    internal enum BridgeTransportKind
    {
        Tcp,
        UnixDomainSocket,
        WindowsNamedPipe
    }

    internal sealed class BridgeTransportEndpoint
    {
        public BridgeTransportKind Kind { get; }
        public int Port { get; }
        public string Path { get; }
        public string PipeName { get; }

        private BridgeTransportEndpoint(BridgeTransportKind kind, int port, string path, string pipeName)
        {
            Kind = kind;
            Port = port;
            Path = path;
            PipeName = pipeName;
        }

        public static BridgeTransportEndpoint CreateTcp(int port)
        {
            Debug.Assert(port > 0, "port must be positive");
            return new BridgeTransportEndpoint(BridgeTransportKind.Tcp, port, string.Empty, string.Empty);
        }

        public static BridgeTransportEndpoint CreateProjectIpc(string projectRoot)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(projectRoot), "projectRoot must not be empty");

            string canonicalProjectRoot = CanonicalizeProjectRoot(projectRoot);
            string endpointName = "uLoopMCP-" + CreateEndpointHash(canonicalProjectRoot);
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                string pipeName = "uloop-" + endpointName;
                return new BridgeTransportEndpoint(
                    BridgeTransportKind.WindowsNamedPipe,
                    0,
                    @"\\.\pipe\" + pipeName,
                    pipeName);
            }

            return new BridgeTransportEndpoint(
                BridgeTransportKind.UnixDomainSocket,
                0,
                System.IO.Path.Combine("/tmp/uloop", endpointName + ".sock"),
                string.Empty);
        }

        public string DisplayName()
        {
            return Kind == BridgeTransportKind.Tcp ? $"127.0.0.1:{Port}" : Path;
        }

        private static string CanonicalizeProjectRoot(string projectRoot)
        {
            string fullPath = System.IO.Path.GetFullPath(projectRoot);
            return fullPath.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        }

        private static string CreateEndpointHash(string canonicalProjectRoot)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(canonicalProjectRoot);
            byte[] hashBytes = sha256.ComputeHash(bytes);
            StringBuilder builder = new StringBuilder(16);
            for (int i = 0; i < 8; i++)
            {
                builder.Append(hashBytes[i].ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
