# uloop CLI

Unity Editor と通信するためのCLIツール。MCPサーバー（TypeScriptServer~）とは完全に独立。

## アーキテクチャ

- **TypeScriptServer~への依存: ゼロ**
- Unityとの通信は `direct-unity-client.ts` で直接TCP接続
- MCPサーバーを経由せず、Unity TCPサーバーと直接やり取り

## ディレクトリ構造

```
src/
├── cli.ts                 # エントリーポイント（commander.js）
├── version.ts             # バージョン管理（release-pleaseで自動更新）
├── execute-tool.ts        # ツール実行ロジック
├── direct-unity-client.ts # Unity TCP直接通信
├── simple-framer.ts       # TCPフレーミング
├── port-resolver.ts       # ポート検出
├── tool-cache.ts          # ツールキャッシュ（.uloop/tools.json）
├── arg-parser.ts          # 引数パース
├── default-tools.json     # デフォルトツール定義
├── skills/                # Claude Code スキル機能
│   ├── skills-command.ts
│   ├── skills-manager.ts
│   ├── bundled-skills.ts
│   └── skill-definitions/ # 13個のスキル定義（.md）
└── __tests__/
    └── cli-e2e.test.ts    # E2Eテスト
```

## ビルド

```bash
npm run build    # dist/cli.bundle.cjs を生成
npm run lint     # ESLint実行
```

## E2Eテスト

E2Eテストは実際にUnity Editorと通信するため、以下の前提条件が必要：

1. Unity Editorが起動していること
2. uLoopMCPパッケージがインストールされていること
3. CLIがビルド済みであること（`npm run build`）

```bash
npm run test:cli # E2Eテスト実行（Unity起動必須）
```

## npm公開

このディレクトリが `uloop-cli` パッケージとしてnpmに公開される。
バージョンは `Packages/src/package.json` と同期（release-pleaseで管理）。

## 注意事項

- `version.ts` はTypeScriptServer~のものとは別ファイル（コピーではない）
- ビルド成果物 `dist/cli.bundle.cjs` は `.gitignore` で除外
- `node_modules/` も `.gitignore` で除外
