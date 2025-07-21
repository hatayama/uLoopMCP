using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    [FilePath("UserSettings/uLoopMCP/SessionData.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class McpSessionManager : ScriptableSingleton<McpSessionManager>
    {
        [SerializeField] private bool _isServerRunning;
        [SerializeField] private int _serverPort = McpServerConfig.DEFAULT_PORT;
        [SerializeField] private bool _isAfterCompile;
        [SerializeField] private bool _isDomainReloadInProgress;
        [SerializeField] private bool _isReconnecting;
        [SerializeField] private bool _showReconnectingUI;
        [SerializeField] private bool _showPostCompileReconnectingUI;
        [SerializeField] private int _selectedEditorType = (int)McpEditorType.Cursor;
        [SerializeField] private float _communicationLogHeight = McpUIConstants.DEFAULT_COMMUNICATION_LOG_HEIGHT;
        [SerializeField] private string _communicationLogsJson = "[]";
        [SerializeField] private string _pendingRequestsJson = "{}";
        [SerializeField] private string _compileWindowLogText = "";
        [SerializeField] private bool _compileWindowHasData;
        [SerializeField] private List<string> _pendingCompileRequestIds = new();
        [SerializeField] private List<CompileRequestData> _compileRequests = new();
        
        // Push通知サーバー関連の新機能
        [SerializeField] private string _pushServerEndpoint;
        [SerializeField] private bool _isPushServerConnected;
        [SerializeField] private long _lastConnectionTimeTicks;

        [System.Serializable]
        public class CompileRequestData
        {
            public string requestId;
            public string json;
        }

        // Server related
        public bool IsServerRunning
        {
            get => _isServerRunning;
            set => _isServerRunning = value;
        }

        public int ServerPort
        {
            get => _serverPort;
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
            get => (McpEditorType)_selectedEditorType;
            set => _selectedEditorType = (int)value;
        }

        public float CommunicationLogHeight
        {
            get => _communicationLogHeight;
            set => _communicationLogHeight = value;
        }

        // Communication Log related
        public string CommunicationLogsJson
        {
            get => _communicationLogsJson;
            set => _communicationLogsJson = value;
        }

        public string PendingRequestsJson
        {
            get => _pendingRequestsJson;
            set => _pendingRequestsJson = value;
        }

        // CompileWindow related
        public string CompileWindowLogText
        {
            get => _compileWindowLogText;
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
            get => _pendingCompileRequestIds.ToArray();
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
            CompileRequestData request = _compileRequests.Find(r => r.requestId == requestId);
            return request?.json;
        }

        public void SetCompileRequestJson(string requestId, string json)
        {
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
            _compileRequests.Clear();
            _pendingCompileRequestIds.Clear();
        }

        public void AddPendingCompileRequest(string requestId)
        {
            if (!_pendingCompileRequestIds.Contains(requestId))
            {
                _pendingCompileRequestIds.Add(requestId);
            }
        }

        public void RemovePendingCompileRequest(string requestId)
        {
            _pendingCompileRequestIds.Remove(requestId);
        }

        // Push通知サーバー情報管理
        public void SetPushServerEndpoint(string endpoint)
        {
            _pushServerEndpoint = endpoint;
            Save(true);
        }

        public string GetPushServerEndpoint()
        {
            return _pushServerEndpoint;
        }

        public void ClearPushServerEndpoint()
        {
            _pushServerEndpoint = null;
            Save(true);
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