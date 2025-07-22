using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    [FilePath("UserSettings/uLoopMCP/SessionData.yaml", FilePathAttribute.Location.ProjectFolder)]
    public sealed class McpSessionManager : ScriptableSingleton<McpSessionManager>
    {
        [SerializeField] private bool _isServerRunning;
        [SerializeField] private int _serverPort;
        [SerializeField] private bool _isAfterCompile;
        [SerializeField] private bool _isDomainReloadInProgress;
        [SerializeField] private bool _isReconnecting;
        [SerializeField] private bool _showReconnectingUI;
        [SerializeField] private bool _showPostCompileReconnectingUI;
        [SerializeField] private int _selectedEditorType;
        [SerializeField] private float _communicationLogHeight;
        [SerializeField] private string _communicationLogsJson;
        [SerializeField] private string _pendingRequestsJson;
        [SerializeField] private string _compileWindowLogText;
        [SerializeField] private bool _compileWindowHasData;
        [SerializeField] private List<string> _pendingCompileRequestIds;
        [SerializeField] private List<CompileRequestData> _compileRequests;
        
        // Push通知サーバー関連の新機能
        [SerializeField] private string _pushServerEndpoint; // 後方互換性のため保持
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
            public string clientName;
            public int clientPort;
            public string endpoint;
            
            public ClientEndpointPair(string clientName, int clientPort, string endpoint)
            {
                this.clientName = clientName;
                this.clientPort = clientPort;
                this.endpoint = endpoint;
            }
            
            // ユニークキーを生成
            public string GetUniqueKey()
            {
                return $"{clientName}:{clientPort}";
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
            get => _selectedEditorType == 0 ? McpEditorType.Cursor : (McpEditorType)_selectedEditorType;
            set => _selectedEditorType = (int)value;
        }

        public float CommunicationLogHeight
        {
            get => _communicationLogHeight == 0 ? McpUIConstants.DEFAULT_COMMUNICATION_LOG_HEIGHT : _communicationLogHeight;
            set => _communicationLogHeight = value;
        }

        // Communication Log related
        public string CommunicationLogsJson
        {
            get => string.IsNullOrEmpty(_communicationLogsJson) ? "[]" : _communicationLogsJson;
            set => _communicationLogsJson = value;
        }

        public string PendingRequestsJson
        {
            get => string.IsNullOrEmpty(_pendingRequestsJson) ? "{}" : _pendingRequestsJson;
            set => _pendingRequestsJson = value;
        }

        // CompileWindow related
        public string CompileWindowLogText
        {
            get => _compileWindowLogText ?? "";
            set => _compileWindowLogText = value;
        }

        public bool CompileWindowHasData
        {
            get => _compileWindowHasData;
            set => _compileWindowHasData = value;
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

        public void ClearCommunicationLogs()
        {
            _communicationLogsJson = "[]";
            _pendingRequestsJson = "{}";
        }

        public void ClearCompileWindowData()
        {
            _compileWindowLogText = "";
            _compileWindowHasData = false;
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
            }
            else
            {
                _compileRequests.Add(new CompileRequestData { requestId = requestId, json = json });
            }
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
        
        // Safe instance access method to avoid constructor conflicts
        public static McpSessionManager GetSafeInstance()
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
        public void SetPushServerEndpoint(string endpoint)
        {
            Debug.Log($"[uLoopMCP] [DEBUG] McpSessionManager.SetPushServerEndpoint(): Setting endpoint to '{endpoint}' (Previous: '{_pushServerEndpoint}')");
            _pushServerEndpoint = endpoint;
            
            // 保存実行
            Save(true);
            
            // フィールド値の再確認
            Debug.Log($"[uLoopMCP] [DEBUG] SetPushServerEndpoint(): Field value after save: '{_pushServerEndpoint}' (Expected: '{endpoint}')");
            Debug.Log("[uLoopMCP] [DEBUG] McpSessionManager.SetPushServerEndpoint(): Save operation completed");
        }

        public string GetPushServerEndpoint()
        {
            Debug.Log($"[uLoopMCP] [DEBUG] McpSessionManager.GetPushServerEndpoint(): Returning '{_pushServerEndpoint}' (IsNullOrEmpty: {string.IsNullOrEmpty(_pushServerEndpoint)})");
            return _pushServerEndpoint;
        }

        public void ClearPushServerEndpoint()
        {
            Debug.Log($"[uLoopMCP] [DEBUG] ClearPushServerEndpoint(): Clearing {_pushServerEndpoints?.Count ?? 0} endpoints");
            _pushServerEndpoint = null;
            _pushServerEndpoints?.Clear();
            Save(true);
            Debug.Log("[uLoopMCP] [DEBUG] ClearPushServerEndpoint(): All endpoints cleared and saved");
        }

        // 複数エンドポイント管理の新機能
        public void SetPushServerEndpoint(string clientName, int clientPort, string endpoint)
        {
            string uniqueKey = $"{clientName}:{clientPort}";
            Debug.Log($"[uLoopMCP] [DEBUG] McpSessionManager.SetPushServerEndpoint(): Setting endpoint for client '{uniqueKey}' to '{endpoint}'");
            
            if (_pushServerEndpoints == null)
            {
                _pushServerEndpoints = new();
            }
            
            // 既存のクライアントエンドポイントを検索（ユニークキーで）
            ClientEndpointPair existingPair = null;
            foreach (ClientEndpointPair pair in _pushServerEndpoints)
            {
                if (pair.GetUniqueKey() == uniqueKey)
                {
                    existingPair = pair;
                    break;
                }
            }
            
            if (existingPair != null)
            {
                Debug.Log($"[uLoopMCP] [DEBUG] SetPushServerEndpoint(): Updating existing endpoint for '{uniqueKey}' from '{existingPair.endpoint}' to '{endpoint}'");
                existingPair.endpoint = endpoint;
            }
            else
            {
                Debug.Log($"[uLoopMCP] [DEBUG] SetPushServerEndpoint(): Adding new endpoint for '{uniqueKey}': '{endpoint}'");
                _pushServerEndpoints.Add(new(clientName, clientPort, endpoint));
            }
            
            // 後方互換性: 最初のエンドポイントを古いフィールドにも保存
            if (_pushServerEndpoints.Count > 0)
            {
                _pushServerEndpoint = _pushServerEndpoints[0].endpoint;
            }
            
            Save(true);
            Debug.Log($"[uLoopMCP] [DEBUG] SetPushServerEndpoint(): Client '{uniqueKey}' endpoint saved successfully");
        }
        
        public string GetPushServerEndpoint(string clientName, int clientPort)
        {
            string uniqueKey = $"{clientName}:{clientPort}";
            
            if (_pushServerEndpoints == null)
            {
                Debug.Log($"[uLoopMCP] [DEBUG] McpSessionManager.GetPushServerEndpoint(): No endpoints stored for client '{uniqueKey}', returning legacy endpoint '{_pushServerEndpoint}'");
                return _pushServerEndpoint;
            }
            
            foreach (ClientEndpointPair pair in _pushServerEndpoints)
            {
                if (pair.GetUniqueKey() == uniqueKey)
                {
                    Debug.Log($"[uLoopMCP] [DEBUG] McpSessionManager.GetPushServerEndpoint(): Found endpoint for client '{uniqueKey}': '{pair.endpoint}'");
                    return pair.endpoint;
                }
            }
            
            Debug.Log($"[uLoopMCP] [DEBUG] McpSessionManager.GetPushServerEndpoint(): No endpoint found for client '{uniqueKey}', returning legacy endpoint '{_pushServerEndpoint}'");
            return _pushServerEndpoint; // フォールバック: 後方互換性
        }
        
        public List<ClientEndpointPair> GetAllPushServerEndpoints()
        {
            return _pushServerEndpoints ?? new();
        }
        
        public void RemovePushServerEndpoint(string clientName, int clientPort)
        {
            if (_pushServerEndpoints == null) return;
            
            string uniqueKey = $"{clientName}:{clientPort}";
            
            for (int i = _pushServerEndpoints.Count - 1; i >= 0; i--)
            {
                if (_pushServerEndpoints[i].GetUniqueKey() == uniqueKey)
                {
                    Debug.Log($"[uLoopMCP] [DEBUG] RemovePushServerEndpoint(): Removing endpoint for client '{uniqueKey}': '{_pushServerEndpoints[i].endpoint}'");
                    _pushServerEndpoints.RemoveAt(i);
                    break;
                }
            }
            
            // 後方互換性: 残りのエンドポイントがあれば最初のものを設定
            if (_pushServerEndpoints.Count > 0)
            {
                _pushServerEndpoint = _pushServerEndpoints[0].endpoint;
            }
            else
            {
                _pushServerEndpoint = null;
            }
            
            Save(true);
        }
        
        public void RemovePushServerEndpoint(string clientEndpoint)
        {
            if (_pushServerEndpoints == null) return;
            
            for (int i = _pushServerEndpoints.Count - 1; i >= 0; i--)
            {
                if (_pushServerEndpoints[i].clientName == clientEndpoint) // clientNameフィールドにclientEndpoint（127.0.0.1:58194）が保存されてる
                {
                    Debug.Log($"[uLoopMCP] [DEBUG] RemovePushServerEndpoint(): Removing endpoint for client '{clientEndpoint}': '{_pushServerEndpoints[i].endpoint}'");
                    _pushServerEndpoints.RemoveAt(i);
                    break;
                }
            }
            
            // 後方互換性: 残りのエンドポイントがあれば最初のものを設定
            if (_pushServerEndpoints.Count > 0)
            {
                _pushServerEndpoint = _pushServerEndpoints[0].endpoint;
            }
            else
            {
                _pushServerEndpoint = null;
            }
            
            Save(true);
        }

        // クライアントエンドポイント（文字列）をキーとする新しいメソッド
        public void SetPushServerEndpoint(string clientEndpoint, string endpoint)
        {
            Debug.Log($"[uLoopMCP] [DEBUG] McpSessionManager.SetPushServerEndpoint(): Setting endpoint for client '{clientEndpoint}' to '{endpoint}'");
            
            if (_pushServerEndpoints == null)
            {
                _pushServerEndpoints = new();
            }
            
            // 既存のクライアントエンドポイントを検索
            ClientEndpointPair existingPair = null;
            foreach (ClientEndpointPair pair in _pushServerEndpoints)
            {
                if (pair.clientName == clientEndpoint) // clientNameフィールドにclientEndpoint（127.0.0.1:57857）を保存
                {
                    existingPair = pair;
                    break;
                }
            }
            
            if (existingPair != null)
            {
                Debug.Log($"[uLoopMCP] [DEBUG] SetPushServerEndpoint(): Updating existing endpoint for '{clientEndpoint}' from '{existingPair.endpoint}' to '{endpoint}'");
                existingPair.endpoint = endpoint;
            }
            else
            {
                Debug.Log($"[uLoopMCP] [DEBUG] SetPushServerEndpoint(): Adding new endpoint for '{clientEndpoint}': '{endpoint}'");
                _pushServerEndpoints.Add(new(clientEndpoint, 0, endpoint)); // clientPortは使わないので0
            }
            
            // 後方互換性: 最初のエンドポイントを古いフィールドにも保存
            if (_pushServerEndpoints.Count > 0)
            {
                _pushServerEndpoint = _pushServerEndpoints[0].endpoint;
            }
            
            Save(true);
            Debug.Log($"[uLoopMCP] [DEBUG] SetPushServerEndpoint(): Client '{clientEndpoint}' endpoint saved successfully");
        }
        
        public string GetPushServerEndpoint(string clientEndpoint)
        {
            if (_pushServerEndpoints == null)
            {
                Debug.Log($"[uLoopMCP] [DEBUG] McpSessionManager.GetPushServerEndpoint(): No endpoints stored for client '{clientEndpoint}', returning legacy endpoint '{_pushServerEndpoint}'");
                return _pushServerEndpoint;
            }
            
            foreach (ClientEndpointPair pair in _pushServerEndpoints)
            {
                if (pair.clientName == clientEndpoint) // clientNameフィールドにclientEndpoint（127.0.0.1:57857）が保存されてる
                {
                    Debug.Log($"[uLoopMCP] [DEBUG] McpSessionManager.GetPushServerEndpoint(): Found endpoint for client '{clientEndpoint}': '{pair.endpoint}'");
                    return pair.endpoint;
                }
            }
            
            Debug.Log($"[uLoopMCP] [DEBUG] McpSessionManager.GetPushServerEndpoint(): No endpoint found for client '{clientEndpoint}', returning legacy endpoint '{_pushServerEndpoint}'");
            return _pushServerEndpoint; // フォールバック: 後方互換性
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