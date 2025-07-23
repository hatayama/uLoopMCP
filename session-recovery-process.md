# Session Recovery Process Documentation

## Overview
Unity Domain Reload時にMCPクライアント接続情報を保持・復元するプロセスの詳細手順

## 1. GracefulShutdownUseCase (Domain Reload前)

### 実行タイミング
- `AssemblyReloadEvents.beforeAssemblyReload`イベント
- McpServerController.OnBeforeAssemblyReload()から呼び出し

### 処理手順 (6ステップ)

#### Step 1: Server停止チェック
```csharp
bool isServerRunning = server.IsRunning;
```
- サーバーが動作中かチェック
- 停止中の場合はStep 4-5へジャンプ

#### Step 2: 現在接続中のクライアント情報取得
```csharp
IReadOnlyCollection<ConnectedClient> connectedClients = server.GetConnectedClients();
```
- サーバーから実際に接続中のクライアント一覧を取得
- クライアント名、エンドポイント、接続時刻を含む

#### Step 3: クライアント情報をSessionDataに保存
```csharp
foreach (ConnectedClient client in connectedClients)
{
    sessionManager.SetPushServerEndpoint(client.Endpoint, pushEndpoint, client.ClientName);
}
```
- 各クライアントの情報をSessionData.yamlに永続化
- pushServerEndpointsマップに保存

#### Step 3.5: PendingRequestsJsonからクライアント名抽出 (補完処理)
```csharp
ExtractClientNamesFromPendingRequests(sessionManager);
```
- Domain Reload時にConnectedClient.ClientNameが"Unknown Client"になる問題に対応
- pendingRequestsJsonを解析してクライアント名を復元
- ユニークキー: `clientName@originalEndpoint` で重複回避
- Mock endpoint生成: `127.0.0.1:unknown_port_{hash}`

#### Step 4: サーバー停止実行
```csharp
serverToShutdown.Dispose();  // Line 271
```
- MCPサーバーを安全に停止（Dispose()パターン使用）
- TCP接続を適切に終了・リソース解放
- StopServerGracefully()メソッド内で実行

#### Step 5: ポート番号保存
```csharp
int portToSave = serverPort;
sessionManager.SetServerPort(portToSave);
```
- 再起動時に同じポートを使用するため保存

#### Step 6: 結果返却
```csharp
return GracefulShutdownResult.Success(portToSave);
```

## 2. UnityPushConnectionManager (Domain Reload中)

### OnAfterAssemblyReload処理
```csharp
// Mock endpointの保護
var mockEndpoints = allEndpoints?.Where(ep => ep.clientEndpoint.Contains("unknown_port_")).ToList();
sessionManager.ClearPushServerEndpoint();

// Mock endpointのみ復元
foreach (var mockEndpoint in mockEndpoints)
{
    sessionManager.SetPushServerEndpoint(mockEndpoint.clientEndpoint, 
        mockEndpoint.pushReceiveServerEndpoint, mockEndpoint.clientName);
}
```
- 実際のendpointはクリア（無効になるため）
- Mock endpointのみ保護・復元してSessionRecoveryで使用可能にする

## 3. SessionRecoveryUseCase (Domain Reload後)

### 実行タイミング
- `AssemblyReloadEvents.afterAssemblyReload`イベント
- McpServerController.OnAfterAssemblyReload()から呼び出し

### 処理手順 (3ステップ)

#### Step 1: SessionDataから保存されたポート番号取得
```csharp
int? savedPort = sessionManager.GetServerPort();
```
- GracefulShutdownで保存されたポート番号を復元

#### Step 2: 保存されたクライアント情報取得
```csharp
var storedEndpoints = sessionManager.GetAllPushServerEndpoints();
```
- SessionData.yamlから保存されたクライアント情報を取得
- Mock endpointと実際のendpointの両方が含まれる可能性

#### Step 3: UI情報復元
```csharp
foreach (var endpoint in storedEndpoints)
{
    var toolData = new ConnectedLLMToolData(
        endpoint.clientName,
        endpoint.clientEndpoint, 
        DateTime.Now
    );
    recoveredTools.Add(toolData);
}

McpEditorWindow.Instance?.RestoreConnectedTools(recoveredTools);
```
- 保存されたクライアント情報をUI表示用データに変換
- McpEditorWindowに復元データを送信
- UIに接続情報を再表示

## 4. McpEditorWindow (UI復元 + 重複解消)

### RestoreConnectedTools処理
```csharp
foreach (ConnectedLLMToolData toolData in restoredTools)
{
    ConnectedClient restoredClient = new(toolData.Endpoint, null, toolData.Name);
    AddConnectedTool(restoredClient);
}
```

### AddConnectedTool での重複解消
```csharp
// 実際の接続追加時にmock endpointを自動削除
if (!IsMockEndpoint(client.Endpoint))
{
    List<ConnectedLLMToolData> mockEndpointsToRemove = _connectedTools
        .Where(tool => tool.Name == client.ClientName && IsMockEndpoint(tool.Endpoint))
        .ToList();
        
    foreach (ConnectedLLMToolData mockTool in mockEndpointsToRemove)
    {
        _connectedTools.Remove(mockTool);
    }
}
```

## プロセス全体の流れ

```
1. Domain Reload開始
   ↓
2. GracefulShutdownUseCase実行
   - 現在の接続情報をSessionData.yamlに保存
   - PendingRequestsからクライアント名抽出→Mock endpoint作成
   - サーバー停止
   ↓
3. UnityPushConnectionManager.OnAfterAssemblyReload
   - 実際のendpointクリア
   - Mock endpointのみ保護・復元
   ↓
4. SessionRecoveryUseCase実行
   - 保存された情報（主にmock endpoint）をUIに復元
   ↓
5. 実際のクライアント再接続
   - AddConnectedToolでmock endpointを自動削除
   - 重複解消完了
```

## 重要なポイント

### Mock Endpointの役割
- Domain Reload中の一時的なプレースホルダー
- `unknown_port_` パターンで識別
- 実際の接続復活時に自動削除される

### 複数接続対応
- 同じクライアント名でも異なるendpointは別々に管理
- ユニークキー: `clientName@originalEndpoint`
- ハッシュベースの決定論的port番号生成

### エラー処理
- サーバー停止中でも既存SessionDataの情報は保持
- PendingRequestsが空の場合はスキップ
- Mock endpoint作成の重複チェック

## デバッグ用VibeLoggerイベント

### GracefulShutdownUseCase
- `graceful_shutdown_usecase_start`
- `graceful_shutdown_client_extracted` 
- `graceful_shutdown_mock_endpoint_created`
- `graceful_shutdown_usecase_success`

### SessionRecoveryUseCase  
- `session_recovery_usecase_start`
- `session_recovery_step_3_complete`
- `session_recovery_usecase_success`

### McpEditorWindow
- `real_connection_replaced_mock_endpoints`
- `delayed_cleanup_protected_mock_endpoints`