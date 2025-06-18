# Unity MCP Server

Unity と Cursor 間の橋渡しを行う Model Context Protocol (MCP) サーバーです。

## ビルドタイミング

### 自動ビルド
- **GitHub Actions**: mainブランチプッシュ時に自動ビルド・コミット
- **postinstall**: npm install実行時に自動ビルド
- **prepublishOnly**: パッケージ公開前に自動ビルド

### 手動ビルド

#### 🔧 npm installが必要な場合
```bash
# 新しい環境・初回セットアップ
npm install
npm run build

# package.json変更後
npm install
npm run build

# node_modules削除後
npm install
npm run build
```

#### ⚡ npm installが不要な場合
```bash
# 既にnode_modulesがある継続開発
npm run build  # 直接実行可能
```

#### 🔍 確認方法
```bash
# node_modulesの存在確認
ls node_modules/ > /dev/null 2>&1 && echo "OK: npm run build可能" || echo "NG: npm install必要"

# TypeScriptコンパイラの確認
npx tsc --version || echo "npm install必要"
```

### ビルド成果物
- `dist/server.js` - メインMCPサーバー
- `dist/unity-client.js` - Unity通信クライアント
- `dist/tools/` - 各種ツール
- `dist/types/` - 型定義

## 概要

このサーバーは、Cursor エディタから Unity エンジンを操作するためのツールセットを提供します。TCP/IP 通信を通じて Unity 側の MCP Bridge と連携し、コンパイル実行やログ取得などの操作を可能にします。

## アーキテクチャ

### 設計原則
- **高い凝集度**: 各コンポーネントが単一の責任を持つ
- **拡張性**: 新しいツールを簡単に追加できる
- **型安全性**: TypeScript の型システムを活用

### ディレクトリ構成

```
src/
├── types/
│   └── tool-types.ts          # ツール関連の型定義
├── tools/
│   ├── base-tool.ts           # ツールの基底クラス
│   ├── ping-tool.ts           # TypeScript側Pingツール
│   ├── unity-ping-tool.ts     # Unity側Pingツール
│   ├── compile-tool.ts        # Unityコンパイルツール
│   ├── logs-tool.ts           # Unityログ取得ツール
│   └── tool-registry.ts       # ツールの登録・管理
├── server.ts                  # MCPサーバーのメインクラス
└── unity-client.ts           # Unity側との通信クライアント
```

## 提供ツール

### 1. ping（開発時のみ）
- **説明**: TypeScript側のMCPサーバー接続テスト（開発時のみ有効）
- **パラメータ**: 
  - `message` (string): テストメッセージ
- **有効化条件**: `NODE_ENV=development` または `ENABLE_PING_TOOL=true`

### 2. unity.ping
- **説明**: Unity側への接続テスト（TCP/IP通信確認）
- **パラメータ**: 
  - `message` (string): Unity側に送信するメッセージ

### 3. action.compileUnity
- **説明**: Unityプロジェクトのコンパイル実行とエラー情報取得
- **パラメータ**: 
  - `forceRecompile` (boolean): 強制再コンパイルフラグ

### 4. context.getUnityLogs
- **説明**: Unityコンソールのログ情報取得
- **パラメータ**: 
  - `logType` (string): フィルタリングするログタイプ (Error, Warning, Log, All)
  - `maxCount` (number): 取得する最大ログ数

## セットアップ

### 前提条件
- Node.js 18以上
- Unity 2020.3以上
- Unity MCP Bridge パッケージがインストール済み

### インストール

```bash
cd Packages/src/TypeScriptServer
npm install
```

### ビルド

```bash
npm run build
```

### 実行

#### 本番環境（pingツール無効）
```bash
npm start
```

#### 開発環境（pingツール有効）
```bash
npm run dev
# または
npm run start:dev
# または環境変数で制御
ENABLE_PING_TOOL=true npm start
```

## Unity側への直接通信テスト

Unity側のMCPサーバーが7400番ポートで起動している場合、直接JSON-RPC通信でコマンドを実行できます。

### コンパイル実行
```bash
echo '{"jsonrpc":"2.0","id":1,"method":"compile","params":{"forceRecompile":false}}' | nc localhost 7400
```

### Ping送信
```bash
echo '{"jsonrpc":"2.0","id":1,"method":"ping","params":{"message":"test"}}' | nc localhost 7400
```

### ログ取得
```bash
echo '{"jsonrpc":"2.0","id":1,"method":"getLogs","params":{"logType":"All","maxCount":10}}' | nc localhost 7400
```

### テスト実行
```bash
echo '{"jsonrpc":"2.0","id":1,"method":"runtests","params":{"filterType":"all","filterValue":"","saveXml":false}}' | nc localhost 7400
```

### 注意事項
- Unity側で「Window > Unity MCP > Start Server」を実行してMCPサーバーを起動している必要があります
- デフォルトポートは7400番です（`McpServerConfig.DEFAULT_PORT`）

## テストスクリプト

Unity側との通信を確認するための各種テストスクリプトです。

### 1. コンパイルテスト (test-compile.js)
Unity側との通信を確認し、実際にコンパイルを実行するテストスクリプトです。

```bash
# TypeScriptサーバーディレクトリに移動
cd Packages/src/TypeScriptServer

# 通常コンパイル
node test/test-compile.js

# 強制再コンパイル
node test/test-compile.js --force
# または
node test/test-compile.js -f

# ヘルプ表示
node test/test-compile.js --help
```

### 2. ログテスト (test-logs.js)
Unity コンソールのログを取得・表示するテストスクリプトです。

```bash
# 全ログ10件取得
node test/test-logs.js

# エラーログのみ取得
node test/test-logs.js --type Error

# 警告ログ20件取得
node test/test-logs.js -t Warning -c 20

# ヘルプ表示
node test/test-logs.js --help
```

### 3. 全ログ統計テスト (test-all-logs.js)
大量のログを取得し、統計情報を表示するテストスクリプトです。

```bash
# 全ログ100件取得+統計表示
node test/test-all-logs.js

# 全ログ200件取得
node test/test-all-logs.js -c 200

# 統計情報のみ表示
node test/test-all-logs.js --stats

# ヘルプ表示
node test/test-all-logs.js --help
```

### 4. 接続テスト (test-unity-connection.js)
Unity側との基本的な接続・通信をテストするスクリプトです。

```bash
# 全機能テスト（ping + compile + logs）
node test/test-unity-connection.js

# pingテストのみ実行
node test/test-unity-connection.js --quick

# 詳細出力で実行
node test/test-unity-connection.js --verbose

# ヘルプ表示
node test/test-unity-connection.js --help
```

### 実行例

**コンパイルテスト:**
```
=== Unity Compile Test ===
Force Recompile: OFF

1. Connecting to Unity...
✓ Connected successfully!

2. Executing compile...
✓ Compile completed!
Success: true
Errors: 0
Warnings: 0
Completed at: 2025-06-18T23:20:14.775Z

3. Disconnecting...
✓ Disconnected
```

**接続テスト（クイック）:**
```
=== Unity Connection Test ===
Verbose: OFF
Quick Test: ON

1. Connecting to Unity...
✓ Connected successfully!

2. Testing ping...
✓ Ping response: Unity MCP Bridge received: Hello from connection test!

✓ Quick test completed successfully!

5. Disconnecting...
✓ Disconnected
```

### 前提条件
- Unity側でMCPサーバーが起動済み（Window > Unity MCP > Start Server）
- Unity側がlocalhostの7400番ポートで待機中

### 機能
- Unity側への接続テスト
- 通常コンパイル・強制再コンパイルの実行
- ログ取得（タイプ別フィルタリング、統計表示）
- コマンドライン引数による動作制御
- エラー/警告の詳細取得と表示
- 自動切断

## 新しいツールの追加方法

### 1. ツールクラスの作成

`src/tools/` ディレクトリに新しいツールクラスを作成します：

```typescript
import { z } from 'zod';
import { BaseTool } from './base-tool.js';

export class MyNewTool extends BaseTool {
  readonly name = 'my.newTool';
  readonly description = '新しいツールの説明';
  readonly inputSchema = {
    type: 'object',
    properties: {
      param1: {
        type: 'string',
        description: 'パラメータ1の説明'
      }
    }
  };

  protected validateArgs(args: unknown) {
    const schema = z.object({
      param1: z.string()
    });
    return schema.parse(args || {});
  }

  protected async execute(args: { param1: string }): Promise<string> {
    // ツールの実際の処理をここに実装
    return `処理結果: ${args.param1}`;
  }

  // 必要に応じてレスポンスフォーマットをカスタマイズ
  protected formatResponse(result: string): ToolResponse {
    return {
      content: [
        {
          type: 'text',
          text: result
        }
      ]
    };
  }
}
```

### 2. ツールレジストリへの登録

`src/tools/tool-registry.ts` の `registerDefaultTools` メソッドに追加：

```typescript
private registerDefaultTools(context: ToolContext): void {
  this.register(new PingTool(context));
  this.register(new UnityPingTool(context));
  this.register(new CompileTool(context));
  this.register(new LogsTool(context));
  this.register(new MyNewTool(context)); // 追加
}
```

### 3. 型定義の追加（必要に応じて）

新しい型が必要な場合は `src/types/tool-types.ts` に追加します。

## 開発ガイドライン

### コーディング規約
- 型宣言は必須（`var` 禁止、明示的な型宣言を推奨）
- 早期return でネストを浅く保つ
- record型を活用した値オブジェクトの使用
- エラーハンドリングは基底クラスで統一

### テンプレートメソッドパターン

`BaseTool` クラスは以下のテンプレートメソッドパターンを提供：

1. **validateArgs**: 引数のバリデーション
2. **execute**: 実際の処理
3. **formatResponse**: 成功レスポンスのフォーマット
4. **formatErrorResponse**: エラーレスポンスのフォーマット

## トラブルシューティング

### Unity接続エラー
- Unity MCP Bridge が起動しているか確認
- Window > uMPC で設定したportが使用可能か確認
- Unity側で "Window > Unity MCP > Start Server" を実行

### コンパイルエラー
```bash
npm run build
```
でTypeScriptのコンパイルエラーを確認

### 依存関係エラー
```bash
# 依存関係の再インストール
rm -rf node_modules package-lock.json
npm install
```

### 型エラー
- `src/types/tool-types.ts` で型定義を確認
- MCPサーバーの戻り値型に合致しているか確認

## ライセンス

MIT License
 