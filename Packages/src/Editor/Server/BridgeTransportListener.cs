using System;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace io.github.hatayama.uLoopMCP
{
    internal sealed class BridgeClientConnection : IDisposable
    {
        public string Endpoint { get; }
        public Stream Stream { get; }
        public int Port { get; }

        public BridgeClientConnection(string endpoint, Stream stream, int port)
        {
            Endpoint = endpoint;
            Stream = stream;
            Port = port;
        }

        public void Dispose()
        {
            Stream.Dispose();
        }
    }

    internal interface IBridgeTransportListener : IDisposable
    {
        BridgeTransportEndpoint Endpoint { get; }
        void Start();
        BridgeClientConnection AcceptClient(CancellationToken ct);
        void Stop();
    }

    internal static class BridgeTransportListenerFactory
    {
        public static IBridgeTransportListener Create(BridgeTransportEndpoint endpoint)
        {
            switch (endpoint.Kind)
            {
                case BridgeTransportKind.Tcp:
                    return new TcpBridgeTransportListener(endpoint);
                case BridgeTransportKind.UnixDomainSocket:
                    return new UnixDomainSocketBridgeTransportListener(endpoint);
                case BridgeTransportKind.WindowsNamedPipe:
                    return new WindowsNamedPipeBridgeTransportListener(endpoint);
                default:
                    throw new ArgumentOutOfRangeException(nameof(endpoint));
            }
        }
    }

    internal sealed class TcpBridgeTransportListener : IBridgeTransportListener
    {
        private TcpListener _listener;

        public BridgeTransportEndpoint Endpoint { get; }

        public TcpBridgeTransportListener(BridgeTransportEndpoint endpoint)
        {
            Endpoint = endpoint;
        }

        public void Start()
        {
            _listener = new TcpListener(IPAddress.Loopback, Endpoint.Port);
            _listener.Start();
        }

        public BridgeClientConnection AcceptClient(CancellationToken ct)
        {
            TcpClient client = _listener.AcceptTcpClient();
            string clientEndpoint = client.Client.RemoteEndPoint?.ToString() ?? McpServerConfig.UNKNOWN_CLIENT_ENDPOINT;
            int clientPort = (client.Client.RemoteEndPoint as IPEndPoint)?.Port ?? 0;
            return new BridgeClientConnection(clientEndpoint, client.GetStream(), clientPort);
        }

        public void Stop()
        {
            _listener?.Stop();
            _listener = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }

    internal sealed class UnixDomainSocketBridgeTransportListener : IBridgeTransportListener
    {
        private Socket _listener;
        private long _nextClientId;

        public BridgeTransportEndpoint Endpoint { get; }

        public UnixDomainSocketBridgeTransportListener(BridgeTransportEndpoint endpoint)
        {
            Endpoint = endpoint;
        }

        public void Start()
        {
            string directory = Path.GetDirectoryName(Endpoint.Path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(Endpoint.Path))
            {
                File.Delete(Endpoint.Path);
            }

            _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            _listener.Bind(new UnixDomainSocketEndPoint(Endpoint.Path));
            _listener.Listen(100);
        }

        public BridgeClientConnection AcceptClient(CancellationToken ct)
        {
            Socket client = _listener.Accept();
            string clientEndpoint = $"{Endpoint.Path}#{Interlocked.Increment(ref _nextClientId)}";
            return new BridgeClientConnection(clientEndpoint, new NetworkStream(client, ownsSocket: true), 0);
        }

        public void Stop()
        {
            _listener?.Close();
            _listener = null;
            if (File.Exists(Endpoint.Path))
            {
                File.Delete(Endpoint.Path);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }

    internal sealed class WindowsNamedPipeBridgeTransportListener : IBridgeTransportListener
    {
        private NamedPipeServerStream _activePipe;
        private long _nextClientId;

        public BridgeTransportEndpoint Endpoint { get; }

        public WindowsNamedPipeBridgeTransportListener(BridgeTransportEndpoint endpoint)
        {
            Endpoint = endpoint;
        }

        public void Start()
        {
        }

        public BridgeClientConnection AcceptClient(CancellationToken ct)
        {
            NamedPipeServerStream pipe = new NamedPipeServerStream(
                Endpoint.PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            _activePipe = pipe;
            pipe.WaitForConnection();
            _activePipe = null;
            string clientEndpoint = $"{Endpoint.Path}#{Interlocked.Increment(ref _nextClientId)}";
            return new BridgeClientConnection(clientEndpoint, pipe, 0);
        }

        public void Stop()
        {
            _activePipe?.Dispose();
            _activePipe = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
