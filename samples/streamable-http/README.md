# Streamable HTTP MCP Server Sample

MCP SDK の `StreamableHTTPServerTransport` を使った最小限のサンプル実装。

## 特徴

- **Stateless パターン**: セッション管理なし、リクエストごとに新しいサーバーインスタンス
- **完全独立**: 本番コードに影響なし
- **プロキシモード**: バックエンド停止時も graceful なエラーを返す

## セットアップ

```bash
cd samples/streamable-http

# 依存関係をインストール
npm install

# TypeScript をコンパイル（出力先: /tmp/streamable-http-sample/dist/）
npm run build
```

## サーバー起動・停止

### 起動モード

| モード | コマンド | ポート | 説明 |
|--------|----------|--------|------|
| 直接接続 | `npm run dev` | 3001 | MCP サーバーに直接接続 |
| プロキシ経由 | `npm run dev:with-proxy` | 3000 | プロキシ経由で接続（バックエンド: 3001） |

### 直接接続モード（デフォルト）

```bash
cd samples/streamable-http
npm run dev
```

- Cursor の接続先: `http://localhost:3001/mcp`
- サーバーが落ちると Cursor が MCP を「使えない」とマークする可能性あり

### プロキシ経由モード

```bash
cd samples/streamable-http
npm run dev:with-proxy
```

- Cursor の接続先: `http://localhost:3000/mcp`（プロキシ）
- バックエンドが落ちてても、プロキシが「一時的に利用不可」を返す
- バックエンドが復活したら自動的に転送再開

### 停止

```bash
# フォアグラウンドで実行中の場合
Ctrl+C

# バックグラウンドで実行中の場合（直接接続モード）
lsof -ti:3001 | xargs kill

# バックグラウンドで実行中の場合（プロキシモード）
lsof -ti:3000,3001 | xargs kill
```

### ヘルスチェック

```bash
# 直接接続モード
curl http://localhost:3001/health
# => {"status":"ok"}

# プロキシモード
curl http://localhost:3000/health
# => {"status":"ok","proxy":true,"backend":"up"}
# => {"status":"ok","proxy":true,"backend":"down"}  # バックエンド落ちてる時
```

## 動作確認

### 1. curl でテスト

```bash
./client/test-client.sh
```

### 2. MCP Inspector でテスト

```bash
npx @modelcontextprotocol/inspector --cli http://localhost:3001/mcp --transport http --method tools/list
```

### 3. Cursor でテスト

`~/.cursor/mcp.json` に追加:

```json
{
  "mcpServers": {
    "sample-http-server": {
      "url": "http://localhost:3001/mcp"
    }
  }
}
```

プロキシモードの場合は `http://localhost:3000/mcp` を指定。

### 4. サーバー再起動テスト

Streamable HTTP の利点を確認:

1. `npm run dev` でサーバー起動
2. Cursor で `hello` ツールを使用
3. サーバー停止（Ctrl+C）
4. サーバー再起動（`npm run dev`）
5. Cursor で再度ツールを使用 → **Cursor 再起動なしで動く！**

### 5. プロキシモードでのバックエンド停止テスト

1. `npm run dev:with-proxy` で起動（プロキシ: 3000、バックエンド: 3001）
2. Cursor で `hello` ツールを使用（正常動作）
3. バックエンドのみ停止: `lsof -ti:3001 | xargs kill`
4. Cursor で `hello` ツールを使用 → `"MCP server temporarily unavailable"` エラー
5. バックエンド再起動: `npm run dev &`
6. Cursor で `hello` ツールを使用 → **正常動作に復帰！**

## エンドポイント

| Method | Path | 説明 |
|--------|------|------|
| POST | `/mcp` | MCP リクエスト受付 |
| GET | `/mcp` | 405 (stateless) |
| DELETE | `/mcp` | 405 (stateless) |
| GET | `/health` | ヘルスチェック |

## 実装されているツール

### hello

挨拶メッセージを返します。

- **入力**: `name` (string) - 挨拶する相手の名前
- **出力**: `"Hello, {name}!"` というテキスト

## 注意事項

### Accept ヘッダー

Streamable HTTP の仕様により、クライアントは `Accept: application/json, text/event-stream` ヘッダーを送る必要があります。

```bash
curl -X POST "http://localhost:3001/mcp" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'
```

## 参考資料

- [MCP TypeScript SDK](https://github.com/modelcontextprotocol/typescript-sdk)
- [Streamable HTTP Transport 仕様](https://modelcontextprotocol.io/specification/2025-03-26/basic/transports)
