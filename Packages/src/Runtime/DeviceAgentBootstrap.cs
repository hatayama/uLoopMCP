using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Bootstraps the Device Agent on application startup.
    /// Only active in Development Builds with ULOOP_DEVICE_AGENT defined.
    /// Uses RuntimeInitializeOnLoadMethod to start before any scene loads.
    /// </summary>
    public sealed class DeviceAgentBootstrap : MonoBehaviour
    {
        private static DeviceAgentBootstrap _instance;
        private DeviceAgentServer _server;
        private DeviceToolRegistry _registry;
        private Stopwatch _uptime;

        // Session-scoped object ID map shared across tools
        private readonly Dictionary<int, GameObject> _objectIdMap = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoStart()
        {
            if (!UnityEngine.Debug.isDebugBuild)
            {
                return;
            }

            // Duplicate detection
            if (_instance != null)
            {
                return;
            }

            GameObject go = new("[uLoop DeviceAgent]");
            DontDestroyOnLoad(go);

            MainThreadDispatcher dispatcher = go.AddComponent<MainThreadDispatcher>();
            MainThreadDispatcher.Initialize(dispatcher);

            DeviceAgentBootstrap bootstrap = go.AddComponent<DeviceAgentBootstrap>();
            _instance = bootstrap;

            bootstrap.StartAgent();
        }

        private void StartAgent()
        {
            _uptime = Stopwatch.StartNew();

            _registry = new DeviceToolRegistry();

            // Register built-in tools
            _registry.Register(new PingTool(_uptime));
            _registry.Register(new FindGameObjectsTool(_objectIdMap));
            _registry.Register(new GetHierarchyTool());
            _registry.Register(new GetScreenshotTool());

            // Auth token: read from Resources if available, otherwise use a default dev token
            string token = LoadAuthToken();

            _server = new DeviceAgentServer(_registry, token);
            _server.Start();
        }

        private static string LoadAuthToken()
        {
            TextAsset tokenAsset = Resources.Load<TextAsset>("uloop-device-token");
            if (tokenAsset != null && !string.IsNullOrWhiteSpace(tokenAsset.text))
            {
                return tokenAsset.text.Trim();
            }

            // Fallback: generate a random token for this session
            string sessionToken = System.Guid.NewGuid().ToString("N");
            Debug.Log($"[DeviceAgent] No token file found in Resources/uloop-device-token.txt. Using session token: {sessionToken}");
            return sessionToken;
        }

        public static DeviceToolRegistry Registry => _instance?._registry;
        public static Dictionary<int, GameObject> ObjectIdMap => _instance?._objectIdMap;

        private void OnDestroy()
        {
            _server?.Dispose();
            _server = null;
            _uptime?.Stop();

            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            _server?.Stop();
        }
    }
}
