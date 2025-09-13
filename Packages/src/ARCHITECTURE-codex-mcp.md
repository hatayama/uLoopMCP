## Codex MCP 設定機能 仕様書

### 目的
- Codex (`~/.codex/config.toml`) に uLoopMCP の MCP サーバ設定を自動生成・更新する
- Unity エディタの「LLM Tool Settings」から他エディタと同等の UX で扱えるようにする
- TOML ライブラリは使わず、文字列ベースの安全なアップサートで対応する

### 対象範囲
- 追加エディタ種別: `Codex`
- 設定ファイル: `${HOME}/.codex/config.toml`
- 生成内容（例）:

```toml
[mcp_servers.uLoopMCP]
command = "node"
args = ["/absolute/path/to/Packages/src/TypeScriptServer~/dist/server.bundle.js"]
env = { "UNITY_TCP_PORT" = "8712" }
```

### 既存連携ポイント
- UI: `Packages/src/Editor/UI/McpEditorWindowView.cs`
  - 「LLM Tool Settings」表示
  - 「Open {Editor} Settings File」
- プレゼンター: `Packages/src/Editor/UI/McpEditorWindow.cs`
  - `ConfigureEditor()` が設定生成/更新を実行
  - `CreateEditorConfigData()` が `IsUpdateNeeded(port)` 判定を参照してボタン活性/非活性を制御
- パス解決: `Packages/src/Editor/Config/UnityMcpPathResolver.cs`
- 設定サービス: `Packages/src/Editor/Config/McpConfigService*.cs`
 - ポート自動同期: `Packages/src/Editor/Config/McpPortChangeUpdater.cs` がポート変更時に全エディタ設定を更新（Codex も対象）

### 追加/変更点
- `McpEditorType` に `Codex` を追加
- `EditorConfigProvider` に Codex を追加
  - 表示名: "Codex"
  - `GetConfigPath(Codex)` → `~/.codex/config.toml`
  - `GetConfigDirectory(Codex)` → `~/.codex`
- `UnityMcpPathResolver`
  - `GetCodexConfigPath()` と `GetCodexConfigDirectory()` を追加
  - `GetTypeScriptServerPath()` を流用し、server.bundle.js の絶対パスを常に取得（UPM 配布/ローカル両対応）
- `McpServerConfigFactory.GetServerPathForEditor(...)`
  - 絶対パス扱いの対象に `Codex` を追加（`Cursor`/`VSCode`/`Windsurf`/`Codex` が absolute）
- `McpConfigServiceFactory`
  - `Codex` のとき TOML 文字列アップサート実装を用いるサービスを返す
- `McpConfigService`（Codex 分）
  - `IsConfigured()`, `IsUpdateNeeded(int port)`, `AutoConfigure(int port)`, `GetConfiguredPort()` を TOML 文字列処理で提供
  - `UpdateDevelopmentSettings(int port, bool developmentMode, bool enableMcpLogs)` を実装
    - `env["UNITY_TCP_PORT"]` を `port` に置換（uLoopMCP セクションが無ければ `AutoConfigure` 後に再実行）
    - `args[0]` の server.bundle.js 絶対パスは変更しない（常に現行の絶対パス維持）
    - 既存のデバッグ系環境変数は JSON 実装と同等の方針で扱う（未設定なら何もしない）

### TOML アップサート仕様（ライブラリ不使用）
- 対象セクション検出
  - 正規表現: `(?ms)^\[mcp_servers\.uLoopMCP\]\s*.*?(?=^\[|\z)`
  - 見つかればその範囲を新ブロックで丸ごと置換
  - 見つからなければ `[mcp_servers.*]` の最後の直後に差し込み
  - MCP 系セクションが一切ない場合は末尾に追記
- 出力ブロック
  - `command = "node"`
  - `args = ["{ABSOLUTE_SERVER_BUNDLE_PATH}"]`
    - 値は `UnityMcpPathResolver.GetTypeScriptServerPath()` の戻り値（絶対パス）
  - `env = { "UNITY_TCP_PORT" = "{PORT}" }`
- 文字列エスケープ
  - `\\` → `\\\\`, `"` → `\\"`（最小限）
- 既存保持
  - 他セクション・コメントは原則保持
  - `uLoopMCP` セクションが複数ある場合は 1 つに正規化

### 更新要否判定（UI ボタン活性ロジック）
- `IsUpdateNeeded(int portToCheck)` は `config.toml` の `uLoopMCP` セクションから下記を抽出して比較
  - `args[0]` のパス == `UnityMcpPathResolver.GetTypeScriptServerPath()`
  - `env["UNITY_TCP_PORT"]` == `portToCheck`
- いずれか不一致なら `true`（＝「Configure Codex」ボタンをアクティブ）
- 抽出用の簡易正規表現例
  - `args`: `(?ms)^\[mcp_servers\.uLoopMCP\].*?^args\s*=\s*\[\s*\"(?<arg0>[^\"\r\n]+)\"`
  - `port`: `env\s*=\s*\{[^}]*\"UNITY_TCP_PORT\"\s*=\s*\"(?<port>\d+)\"`

### ポート・パス決定
- サーバ起動中: `portToCheck = McpServerController.ServerPort`
- 停止中: `portToCheck = UI.CustomPort`
- server.bundle.js: `UnityMcpPathResolver.GetTypeScriptServerPath()`（UPM/ローカルの両方で絶対パス）

### ポート変更の自動同期（TOML）
- トリガ
  - UI でポート変更: `McpEditorModel.UpdateCustomPort` → `McpPortChangeUpdater.UpdateAllConfigurationsForPortChange`
  - サーバ起動時のポート衝突等で自動変更: サーバ側から同メソッドを呼び出し
- Codex の更新処理
  - 既に `~/.codex/config.toml` に `uLoopMCP` が存在する場合、当該セクションの `env["UNITY_TCP_PORT"]` を新ポートへ置換
  - セクションが無い場合は `AutoConfigure(newPort)` を実行して作成後に置換を試行
  - `args[0]` の絶対パスは変更しない（パスは常に `UnityMcpPathResolver.GetTypeScriptServerPath()` で生成される）
- UI 反映
  - 自動更新後に `IsUpdateNeeded(port)` が再評価され、差分が解消されていればボタンは非活性（"Settings Already Configured"）になる

### Open Settings File
- `UnityMcpPathResolver.GetCodexConfigPath()` を `EditorUtility.OpenWithDefaultApp()` に渡す
- ファイル非存在時はダイアログで誘導（まず Configure 実行）

### エラーハンドリング/安全性
- `~/.codex` ディレクトリが無ければ作成
- 書き込みは全体文字列の 1 回書き込み
- 既存内容が空/破損でも fail-safe に新規生成
- 文字列置換のためコメント位置の完全保持は非保証（機能要件外）

### テスト計画
- 絶対パス検出: UPM 配布/ローカルの両方で正しいパスが出力される
- `IsUpdateNeeded` の判定
  - 既存と同一 → 非アクティブ
  - パス差異/ポート差異 → アクティブ
- 既存 `uLoopMCP` あり/なし/複数 の各ケースでアップサート結果を検証
- 「Open Settings File」存在/非存在時の挙動
- 回帰: 他エディタ（`Cursor`/`VSCode` 等）の表示・ボタン挙動が変わらない
 - ポート自動同期: UI/サーバいずれのポート変更でも Codex TOML の `UNITY_TCP_PORT` が即時更新される

### 将来の拡張（任意）
- TOML パーサ導入（Tomlyn）でコメント保持や厳密型サポート
- マイグレーション（複数キー→単一キー正規化の履歴ログ化）


