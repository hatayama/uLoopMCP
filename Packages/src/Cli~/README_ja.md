[English](README.md)

# uloop-cli

[![npm version](https://img.shields.io/npm/v/uloop-cli.svg)](https://www.npmjs.com/package/uloop-cli)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Node.js](https://img.shields.io/badge/Node.js-20+-green.svg)](https://nodejs.org/)

**[uLoopMCP](https://github.com/hatayama/uLoopMCP) の CLI コンパニオン** - AIエージェントにUnityプロジェクトのコンパイル、テスト、操作を任せましょう。

> **前提条件**: このCLIを使用するには、Unityプロジェクトに [uLoopMCP](https://github.com/hatayama/uLoopMCP) がインストールされ、サーバーが起動している必要があります。セットアップ手順は [メインリポジトリ](https://github.com/hatayama/uLoopMCP) を参照してください。

## インストール

```bash
npm install -g uloop-cli
```

## クイックスタート

### ステップ 1: Skills のインストール

Skills を使うと、LLMツール（Claude Code、Cursor など）がUnity操作を自動的に呼び出せるようになります。

```bash
# Claude Code 用にインストール（プロジェクトレベル）
uloop skills install --claude

# OpenAI Codex 用にインストール（プロジェクトレベル）
uloop skills install --codex

# グローバルにインストールすることも可能
uloop skills install --claude --global
uloop skills install --codex --global
```

### ステップ 2: LLM ツールで使う

Skills をインストールすると、LLMツールが以下のような指示を自動的に処理できるようになります：

| あなたの指示 | 使用される Skill |
|---|---|
| 「コンパイルエラーを直して」 | `/uloop-compile` |
| 「テストを実行して、失敗の原因を教えて」 | `/uloop-run-tests` |
| 「シーンの階層構造を確認して」 | `/uloop-get-hierarchy` |
| 「プレハブを検索して」 | `/uloop-unity-search` |

> **MCP の設定は不要です！** uLoopMCP Window でサーバーが起動していれば、LLMツールは Skills を通じてUnityと直接通信します。

## 利用可能な Skills

Skills はUnityプロジェクト内の uLoopMCP パッケージから動的に読み込まれます。以下は uLoopMCP が提供するデフォルトの Skills です：

- `/uloop-compile` - コンパイルの実行
- `/uloop-get-logs` - コンソールログの取得
- `/uloop-run-tests` - テストの実行
- `/uloop-clear-console` - コンソールのクリア
- `/uloop-focus-window` - Unity Editor を前面に表示
- `/uloop-get-hierarchy` - シーン階層の取得
- `/uloop-unity-search` - Unity 検索
- `/uloop-get-menu-items` - メニューアイテムの取得
- `/uloop-execute-menu-item` - メニューアイテムの実行
- `/uloop-find-game-objects` - GameObject の検索
- `/uloop-capture-window` - EditorWindow のキャプチャ
- `/uloop-control-play-mode` - Play Mode の制御
- `/uloop-execute-dynamic-code` - 動的 C# コードの実行
- `/uloop-get-provider-details` - 検索プロバイダー詳細の取得

プロジェクトで定義したカスタム Skills も自動的に検出されます。

## CLI の直接使用

Skills を使わずに、CLI を直接呼び出すこともできます：

```bash
# 利用可能なツールの一覧
uloop list

# Unity からツール定義をローカルキャッシュに同期
uloop sync

# コンパイルの実行
uloop compile

# 強制再コンパイルし、Domain Reload の完了を待機
uloop compile --force-recompile true --wait-for-domain-reload true

# ログの取得
uloop get-logs --max-count 10

# テストの実行
uloop run-tests --filter-type all

# 動的コードの実行
uloop execute-dynamic-code --code 'using UnityEngine; Debug.Log("Hello from CLI!");'
```

## シェル補完

Bash/Zsh/PowerShell のタブ補完をインストールできます：

```bash
# シェルを自動検出してインストール
uloop completion --install

# シェルを明示的に指定
uloop completion --shell bash --install        # Git Bash / MINGW64
uloop completion --shell powershell --install  # PowerShell
```

## ポートの指定

`--port` オプションを指定することで、複数の Unity インスタンスを操作できます：

```bash
uloop compile --port 8700
uloop compile --port 8701
```

`--port` を省略した場合、現在のプロジェクトに設定されたポートが自動的に使用されます。

ポート番号は各 Unity の uLoopMCP Window で確認できます。

## 動作要件

- **Node.js 20.0 以降**
- **Unity 2022.3 以降**（[uLoopMCP](https://github.com/hatayama/uLoopMCP) がインストール済みであること）
- uLoopMCP サーバーが起動していること（Window > uLoopMCP > Start Server）

## リンク

- [uLoopMCP リポジトリ](https://github.com/hatayama/uLoopMCP) - メインパッケージとドキュメント
- [ツールリファレンス](https://github.com/hatayama/uLoopMCP/blob/main/Packages/src/TOOL_REFERENCE.md) - 詳細なツール仕様

## ライセンス

MIT License
