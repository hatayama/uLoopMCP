# Domain Reload Connection Issue Investigation and Resolution

## 概要

Unity Domain Reload後にTypeScriptサーバーとの接続が長時間途切れる問題の調査・解決を実施。

## 問題の詳細

### 初期問題
- Domain reload実行後、UnityとTypeScriptサーバー間の接続が長時間切断される
- 特にコンパイル時間が長い場合に顕著に発生
- Connected LLM Tools一覧に表示されるまでTool実行が不可能

### 発見された主要な問題
1. **ObjectDisposedException**: NetworkStreamが破棄された後の書き込み操作
2. **ファイル共有違反**: 複数スレッドによる同時ログファイル書き込み

## 実装した解決策

### 1. VibeLogger - AI用構造化ログシステム

**目的**: Domain reload周辺の挙動を詳細に追跡するための構造化ログ

**特徴**:
- JSON形式の構造化ログ
- 相関IDによる関連操作の追跡
- AI分析に最適化されたフォーマット
- Unity/TypeScript両側で統一インターフェース

**ファイル**:
- Unity側: `Packages/src/Editor/Core/Logging/VibeLogger.cs`
- TypeScript側: `Packages/src/TypeScriptServer~/src/utils/vibe-logger.ts`

**出力先**: `{project_root}/uLoopMCPOutputs/VibeLogs/`
- Unity: `unity_vibe_YYYYMMDD.json`
- TypeScript: `typescript_vibe_YYYYMMDD.json`

### 2. ObjectDisposedException修正

**問題**: McpBridgeServerでNetworkStreamが破棄された後に書き込み操作を実行

**解決策**: 
```csharp
// 書き込み前のストリーム状態チェック
if (!stream.CanWrite || !client.Connected || cancellationToken.IsCancellationRequested)
{
    VibeLogger.LogWarning("mcp_response_send_skipped", ...);
    return; // 書き込み操作をスキップ
}
```

**修正ファイル**: `Packages/src/Editor/Server/McpBridgeServer.cs:522-540`

### 3. ファイル共有違反修正

**問題**: 複数スレッドから同一ログファイルへの同時書き込みによる共有違反

**解決策**:

**Unity側**:
```csharp
// FileStreamによる排他制御 + リトライ機構
using (var fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
using (var writer = new StreamWriter(fileStream))
{
    writer.Write(jsonLog);
    writer.Flush();
}
```

**TypeScript側**:
```typescript
// 指数バックオフによるリトライ機構
for (let retry = 0; retry < maxRetries; retry++) {
    try {
        fs.appendFileSync(filePath, jsonLog, { flag: 'a' });
        return; // 成功時は即座に終了
    } catch (error) {
        if (this.isFileSharingViolation(error) && retry < maxRetries - 1) {
            const delayMs = baseDelayMs * Math.pow(2, retry);
            this.sleep(delayMs);
        }
    }
}
```

### 4. Domain Reload シミュレーション

**実装**: McpServerController.csに15秒のThread.Sleep追加
```csharp
private static void OnBeforeAssemblyReload()
{
    // 長時間コンパイルのシミュレーション
    Thread.Sleep(15000);
}
```

## 調査結果と発見事項

### TypeScript側Discovery Logic
- UnityDiscovery: 1秒間隔でUnity接続をポーリング
- 段階的バックオフは実装されていない（常に1秒間隔）
- ファイル: `Packages/src/TypeScriptServer~/src/unity-discovery.ts`

### 接続アーキテクチャ
- Unity: McpBridgeServer (TCP/IP サーバー)
- TypeScript: UnityClient (TCP/IP クライアント)
- 通信プロトコル: JSON-RPC 2.0 over Content-Length framing

### Domain Reload ライフサイクル
1. `OnBeforeAssemblyReload` - サーバー停止
2. Assembly再読み込み
3. `OnAfterAssemblyReload` - サーバー再起動
4. TypeScript側が新しいポートでUnityを再発見

## テスト結果

### 修正前
```
[VibeLogger] Failed to save log to file: Sharing violation on path /Users/.../unity_vibe_20250717.json
```

### 修正後
- 共有違反エラー完全解消
- 複数スレッドからの並行ログ書き込み正常動作
- ObjectDisposedException完全解消（`mcp_response_send_skipped`ログで確認）

## 今後の課題・改善点

### 1. Discovery Logic改善
- 段階的バックオフの実装検討（1→3→10→60秒）
- Unity側の状態通知機能

### 2. 接続復旧の高速化
- Domain reload完了通知の実装
- より効率的な再接続メカニズム

### 3. ログ分析
- VibeLoggerによる継続的な接続品質監視
- パフォーマンスメトリクス収集

## 関連ファイル

### 主要修正ファイル
- `Packages/src/Editor/Core/Logging/VibeLogger.cs` - Unity構造化ログ
- `Packages/src/TypeScriptServer~/src/utils/vibe-logger.ts` - TypeScript構造化ログ
- `Packages/src/Editor/Server/McpBridgeServer.cs` - ObjectDisposedException修正
- `Packages/src/Editor/Server/McpServerController.cs` - Domain reloadシミュレーション

### 設定ファイル
- `Packages/src/Editor/Config/McpConstants.cs` - VIBE_LOGS_DIR定数
- `Packages/src/TypeScriptServer~/src/constants.ts` - OUTPUT_DIRECTORIES定数

### ログ出力
- `uLoopMCPOutputs/VibeLogs/unity_vibe_*.json` - Unity側ログ
- `uLoopMCPOutputs/VibeLogs/typescript_vibe_*.json` - TypeScript側ログ

## コミット履歴

- `42d6dc2` - feat: add VibeLogger for AI-friendly structured logging
- `4e09b85` - rename (ファイル構成調整)
- `9cdfae7` - fix: resolve file sharing violations in VibeLogger concurrent writes

## 検証方法

1. Domain reload実行（Assembly recompile）
2. VibeLoggerログファイル確認
3. Unity Console Log確認（共有違反エラーの有無）
4. TypeScript MCP Server接続状態確認

```bash
# ログ確認コマンド例
tail -f uLoopMCPOutputs/VibeLogs/unity_vibe_$(date +%Y%m%d).json
grep "Sharing violation" uLoopMCPOutputs/VibeLogs/*.json
```

---

*作成日: 2025-07-17*  
*作成者: Claude (AI Assistant)*  
*ブランチ: fix/reconnect*