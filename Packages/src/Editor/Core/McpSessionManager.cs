using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    [FilePath("UserSettings/uLoopMCP/SessionData.yaml", FilePathAttribute.Location.ProjectFolder)]
    public sealed class McpSessionManager : ScriptableSingleton<McpSessionManager>
    {
        // Client endpoint configuration constants
        private const string CLIENT_ENDPOINT_SEPARATOR = ":";
        private const int DEFAULT_CLIENT_PORT = 0;
        [SerializeField] private bool _isServerRunning;
        [SerializeField] private int _serverPort;
        [SerializeField] private bool _isAfterCompile;
        [SerializeField] private bool _isDomainReloadInProgress;
        [SerializeField] private bool _isReconnecting;
        [SerializeField] private bool _showReconnectingUI;
        [SerializeField] private bool _showPostCompileReconnectingUI;
        [SerializeField] private int _selectedEditorType;
        [SerializeField] private float _communicationLogHeight;
        [SerializeField] private string _pendingRequestsJson;
        [SerializeField] private List<string> _pendingCompileRequestIds;
        [SerializeField] private List<CompileRequestData> _compileRequests;
        
        // Push通知サーバー関連の新機能
        [SerializeField] private bool _isPushServerConnected;
        [SerializeField] private long _lastConnectionTimeTicks;
        [SerializeField] private List<ClientEndpointPair> _pushServerEndpoints; // 複数エンドポイント管理
        
        [System.Serializable]
        public class CompileRequestData
        {
            public string requestId;
            public string json;
        }

        [System.Serializable]
        public class ClientEndpointPair
        {
            public string clientName;                    // Actual client name ("Claude Code", "Cursor")
            public string clientEndpoint;                // Client TCP endpoint ("127.0.0.1:58194")
            public string pushReceiveServerEndpoint;     // Push notification receive server endpoint ("localhost:12345")
            
            public ClientEndpointPair(string clientName, string clientEndpoint, string pushReceiveServerEndpoint)
            {
                this.clientName = clientName;
                this.clientEndpoint = clientEndpoint;
                this.pushReceiveServerEndpoint = pushReceiveServerEndpoint;
            }
            
            // For backward compatibility - constructor with clientIdentifier (can be either name or endpoint)
            public ClientEndpointPair(string clientIdentifier, int clientPort, string endpoint)
            {
                // If clientIdentifier contains ":" it's an endpoint, otherwise it's a name
                if (clientIdentifier.Contains(CLIENT_ENDPOINT_SEPARATOR))
                {
                    this.clientEndpoint = clientIdentifier;
                    this.clientName = "Unknown";
                }
                else
                {
                    this.clientName = clientIdentifier;
                    this.clientEndpoint = $"127.0.0.1{CLIENT_ENDPOINT_SEPARATOR}{clientPort}"; // Construct from port
                }
                this.pushReceiveServerEndpoint = endpoint;
            }
            
            
            // ユニークキーを生成（clientEndpointを使用）
            public string GetUniqueKey()
            {
                return clientEndpoint;
            }
        }

        // private McpSessionManager()
        // {
        //     Debug.Log($"[hatayama] VAR:");
        // }

        // Server related
        public bool IsServerRunning
        {
            get => _isServerRunning;
            set => _isServerRunning = value;
        }

        public int ServerPort
        {
            get => _serverPort == 0 ? McpServerConfig.DEFAULT_PORT : _serverPort;
            set => _serverPort = value;
        }

        public bool IsAfterCompile
        {
            get => _isAfterCompile;
            set => _isAfterCompile = value;
        }

        public bool IsDomainReloadInProgress
        {
            get => _isDomainReloadInProgress;
            set => _isDomainReloadInProgress = value;
        }

        public bool IsReconnecting
        {
            get => _isReconnecting;
            set => _isReconnecting = value;
        }

        public bool ShowReconnectingUI
        {
            get => _showReconnectingUI;
            set => _showReconnectingUI = value;
        }

        public bool ShowPostCompileReconnectingUI
        {
            get => _showPostCompileReconnectingUI;
            set => _showPostCompileReconnectingUI = value;
        }

        // UI related
        public McpEditorType SelectedEditorType
        {
            get 
            {
                bool isDefaultValue = _selectedEditorType == 0;
                return isDefaultValue ? McpEditorType.Cursor : (McpEditorType)_selectedEditorType;
            }
            set => _selectedEditorType = (int)value;
        }

        public float CommunicationLogHeight
        {
            get 
            {
                bool useDefaultHeight = _communicationLogHeight == 0;
                return useDefaultHeight ? McpUIConstants.DEFAULT_COMMUNICATION_LOG_HEIGHT : _communicationLogHeight;
            }
            set => _communicationLogHeight = value;
        }

        // Pending requests JSON data
        public string PendingRequestsJson
        {
            get 
            {
                bool isEmptyJson = string.IsNullOrEmpty(_pendingRequestsJson);
                return isEmptyJson ? "{}" : _pendingRequestsJson;
            }
            set => _pendingRequestsJson = value;
        }

        // CompileSessionState related properties
        public string[] PendingCompileRequestIds
        {
            get => (_pendingCompileRequestIds ?? new List<string>()).ToArray();
            set => _pendingCompileRequestIds = new List<string>(value);
        }

        // Methods

        public void ClearServerSession()
        {
            _isServerRunning = false;
        }

        public void ClearAfterCompileFlag()
        {
            _isAfterCompile = false;
        }

        public void ClearReconnectingFlags()
        {
            _isReconnecting = false;
            _showReconnectingUI = false;
        }

        public void ClearPostCompileReconnectingUI()
        {
            _showPostCompileReconnectingUI = false;
        }

        public void ClearDomainReloadFlag()
        {
            _isDomainReloadInProgress = false;
        }

        public void ClearPendingRequests()
        {
            _pendingRequestsJson = "{}";
        }

        public string GetCompileRequestJson(string requestId)
        {
            if (_compileRequests == null) _compileRequests = new List<CompileRequestData>();
            CompileRequestData request = _compileRequests.Find(r => r.requestId == requestId);
            return request?.json;
        }

        public void SetCompileRequestJson(string requestId, string json)
        {
            if (_compileRequests == null) _compileRequests = new List<CompileRequestData>();
            
            CompileRequestData existingRequest = _compileRequests.Find(r => r.requestId == requestId);
            if (existingRequest != null)
            {
                existingRequest.json = json;
                return;
            }
            
            _compileRequests.Add(new CompileRequestData { requestId = requestId, json = json });
        }

        public void ClearAllCompileRequests()
        {
            if (_compileRequests == null) _compileRequests = new List<CompileRequestData>();
            if (_pendingCompileRequestIds == null) _pendingCompileRequestIds = new List<string>();
            _compileRequests.Clear();
            _pendingCompileRequestIds.Clear();
        }

        public void AddPendingCompileRequest(string requestId)
        {
            if (_pendingCompileRequestIds == null) _pendingCompileRequestIds = new List<string>();
            if (!_pendingCompileRequestIds.Contains(requestId))
            {
                _pendingCompileRequestIds.Add(requestId);
            }
        }

        public void RemovePendingCompileRequest(string requestId)
        {
            if (_pendingCompileRequestIds == null) _pendingCompileRequestIds = new List<string>();
            _pendingCompileRequestIds.Remove(requestId);
        }

        private static McpSessionManager _safeInstance;
        private static bool _instanceRequested = false;
        
        // Safe instance access method to avoid constructor conflicts - async version
        public static async Task<McpSessionManager> GetSafeInstanceAsync()
        {
            // MainThreadに切り替え
            await MainThreadSwitcher.SwitchToMainThread();
            
            return GetSafeInstanceInternal();
        }
        
        // Internal implementation - must be called on main thread
        private static McpSessionManager GetSafeInstanceInternal()
        {
            if (_safeInstance != null)
            {
                return _safeInstance;
            }
            
            if (_instanceRequested)
            {
                Debug.LogWarning("[uLoopMCP] [WARNING] McpSessionManager instance already being constructed, returning null to avoid conflict");
                return null;
            }
            
            try
            {
                _instanceRequested = true;
                _safeInstance = instance;
                return _safeInstance;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[uLoopMCP] [ERROR] Failed to get McpSessionManager instance: {ex.Message}");
                return null;
            }
            finally
            {
                _instanceRequested = false;
            }
        }

        // Push通知サーバー情報管理
        public void ClearPushServerEndpoint()
        {
            _pushServerEndpoints?.Clear();
            Save(true);
        }
        
        public void CleanupInvalidPushServerEndpoints()
        {
            if (_pushServerEndpoints == null) return;
            
            // Remove entries with invalid endpoints or unknown push endpoints
            for (int i = _pushServerEndpoints.Count - 1; i >= 0; i--)
            {
                ClientEndpointPair pair = _pushServerEndpoints[i];
                
                // Remove if pushReceiveServerEndpoint is "Unknown" or empty
                if (string.IsNullOrEmpty(pair.pushReceiveServerEndpoint) || 
                    pair.pushReceiveServerEndpoint == "Unknown")
                {
                    UnityEngine.Debug.Log($"[uLoopMCP] Removing invalid endpoint entry: {pair.clientName} - {pair.clientEndpoint}");
                    _pushServerEndpoints.RemoveAt(i);
                    continue;
                }
                
                // Remove if clientEndpoint format is invalid (should start with 127.0.0.1:)
                if (!pair.clientEndpoint.StartsWith("127.0.0.1:"))
                {
                    UnityEngine.Debug.Log($"[uLoopMCP] Removing invalid client endpoint format: {pair.clientName} - {pair.clientEndpoint}");
                    _pushServerEndpoints.RemoveAt(i);
                }
            }
            
            Save(true);
        }

        // 複数エンドポイント管理の新機能
        public void SetPushServerEndpoint(string clientName, int clientPort, string pushReceiveServerEndpoint)
        {
            string clientEndpoint = BuildClientEndpoint(clientName, clientPort);
            
            EnsureEndpointsListInitialized();
            UpdateOrAddEndpoint(clientName, clientEndpoint, pushReceiveServerEndpoint);
            
            Save(true);
        }
        
        private void EnsureEndpointsListInitialized()
        {
            if (_pushServerEndpoints == null)
            {
                _pushServerEndpoints = new();
            }
        }
        
        private void UpdateOrAddEndpoint(string clientName, string clientEndpoint, string pushReceiveServerEndpoint)
        {
            string uniqueKey = clientEndpoint;
            ClientEndpointPair existingPair = FindEndpointPair(uniqueKey);
            
            if (existingPair != null)
            {
                existingPair.pushReceiveServerEndpoint = pushReceiveServerEndpoint;
                existingPair.clientName = clientName; // Update client name if provided
            }
            else
            {
                _pushServerEndpoints.Add(new(clientName, clientEndpoint, pushReceiveServerEndpoint));
            }
        }
        
        private ClientEndpointPair FindEndpointPair(string uniqueKey)
        {
            foreach (ClientEndpointPair pair in _pushServerEndpoints)
            {
                if (pair.GetUniqueKey() == uniqueKey)
                {
                    return pair;
                }
            }
            return null;
        }
        
        /// <summary>
        /// Builds client endpoint string from client name and port number
        /// </summary>
        /// <param name="clientName">Client name (unused but kept for future extensibility)</param>
        /// <param name="clientPort">Client port number</param>
        /// <returns>Constructed client endpoint string</returns>
        private string BuildClientEndpoint(string clientName, int clientPort)
        {
            return $"127.0.0.1{CLIENT_ENDPOINT_SEPARATOR}{clientPort}";
        }
        
        
        public string GetPushServerEndpoint(string clientName, int clientPort)
        {
            string clientEndpoint = BuildClientEndpoint(clientName, clientPort);
            
            if (_pushServerEndpoints == null)
            {
                return null;
            }
            
            foreach (ClientEndpointPair pair in _pushServerEndpoints)
            {
                if (pair.GetUniqueKey() == clientEndpoint)
                {
                    return pair.pushReceiveServerEndpoint;
                }
            }
            
            return null;
        }
        
        public List<ClientEndpointPair> GetAllPushServerEndpoints()
        {
            return _pushServerEndpoints ?? new();
        }
        
        public void RemovePushServerEndpoint(string clientName, int clientPort)
        {
            if (_pushServerEndpoints == null) return;
            
            string clientEndpoint = BuildClientEndpoint(clientName, clientPort);
            
            for (int i = _pushServerEndpoints.Count - 1; i >= 0; i--)
            {
                if (_pushServerEndpoints[i].GetUniqueKey() == clientEndpoint)
                {
                    _pushServerEndpoints.RemoveAt(i);
                    break;
                }
            }
            
            Save(true);
        }
        
        public void RemovePushServerEndpoint(string clientEndpoint)
        {
            UnityEngine.Debug.Log($"[uLoopMCP] RemovePushServerEndpoint called with: {clientEndpoint}");
            
            if (_pushServerEndpoints == null) 
            {
                UnityEngine.Debug.Log($"[uLoopMCP] _pushServerEndpoints is null, nothing to remove");
                return;
            }
            
            UnityEngine.Debug.Log($"[uLoopMCP] Current endpoint count: {_pushServerEndpoints.Count}");
            
            for (int i = _pushServerEndpoints.Count - 1; i >= 0; i--)
            {
                UnityEngine.Debug.Log($"[uLoopMCP] Checking endpoint {i}: {_pushServerEndpoints[i].clientEndpoint}");
                if (_pushServerEndpoints[i].clientEndpoint == clientEndpoint)
                {
                    UnityEngine.Debug.Log($"[uLoopMCP] Found matching endpoint, removing: {_pushServerEndpoints[i].clientName}");
                    _pushServerEndpoints.RemoveAt(i);
                    break;
                }
            }
            
            UnityEngine.Debug.Log($"[uLoopMCP] Endpoint count after removal: {_pushServerEndpoints.Count}");
            Save(true);
        }

        // クライアントエンドポイント（文字列）をキーとする新しいメソッド
        public void SetPushServerEndpoint(string clientEndpoint, string pushReceiveServerEndpoint, string clientName = "Unknown")
        {
            if (_pushServerEndpoints == null)
            {
                _pushServerEndpoints = new();
            }
            
            // Note: Allow multiple instances of the same client name (e.g., multiple Claude Code windows)
            
            // 既存のクライアントエンドポイントを検索
            ClientEndpointPair currentPair = null;
            foreach (ClientEndpointPair pair in _pushServerEndpoints)
            {
                if (pair.clientEndpoint == clientEndpoint) // clientEndpointフィールドを使用
                {
                    currentPair = pair;
                    break;
                }
            }
            
            if (currentPair != null)
            {
                currentPair.pushReceiveServerEndpoint = pushReceiveServerEndpoint;
                currentPair.clientName = clientName; // Update client name if provided
            }
            else
            {
                _pushServerEndpoints.Add(new(clientName, clientEndpoint, pushReceiveServerEndpoint));
            }
            
            Save(true);
        }
        
        public string GetPushServerEndpoint(string clientEndpoint)
        {
            if (_pushServerEndpoints == null)
            {
                return null;
            }
            
            foreach (ClientEndpointPair pair in _pushServerEndpoints)
            {
                if (pair.clientEndpoint == clientEndpoint) // clientEndpointフィールドを使用
                {
                    return pair.pushReceiveServerEndpoint;
                }
            }
            
            return null;
        }

        public void SetPushServerConnected(bool connected)
        {
            _isPushServerConnected = connected;
            if (connected)
            {
                _lastConnectionTimeTicks = DateTime.Now.Ticks;
            }
            Save(true);
        }

        public bool IsPushServerConnected()
        {
            return _isPushServerConnected;
        }

        public DateTime GetLastConnectionTime()
        {
            return new DateTime(_lastConnectionTimeTicks);
        }

        public void UpdateLastConnectionTime()
        {
            _lastConnectionTimeTicks = DateTime.Now.Ticks;
            Save(true);
        }
    }
}