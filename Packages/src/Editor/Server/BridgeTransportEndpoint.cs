using System;
using System.IO;
using System.Runtime.InteropServices;
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
        private const string LIBC = "libc";

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
            string trimmedPath = fullPath.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return trimmedPath;
            }

            return ResolveUnixRealPath(trimmedPath);
        }

        private static string ResolveUnixRealPath(string path)
        {
            IntPtr resolvedPath = RealPath(path, IntPtr.Zero);
            if (resolvedPath == IntPtr.Zero)
            {
                return path;
            }

            try
            {
                string realPath = Marshal.PtrToStringAnsi(resolvedPath);
                Debug.Assert(!string.IsNullOrWhiteSpace(realPath), "realPath must not be empty");
                return realPath.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            }
            finally
            {
                Free(resolvedPath);
            }
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

        [DllImport(LIBC, EntryPoint = "realpath", SetLastError = true)]
        private static extern IntPtr RealPath(string path, IntPtr resolvedPath);

        [DllImport(LIBC, EntryPoint = "free")]
        private static extern void Free(IntPtr pointer);
    }
}
