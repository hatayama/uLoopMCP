# Unity CLI Loop

[English](README.md)

[![Unity](https://img.shields.io/badge/Unity-2022.3+-red.svg)](https://unity3d.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.md)<br>
![ClaudeCode](https://img.shields.io/badge/Claude_Code-555?logo=claude)
![Cursor](https://img.shields.io/badge/Cursor-111?logo=Cursor)
![Codex](https://img.shields.io/badge/Codex-111?logo=data:image/svg+xml;base64,PHN2ZyByb2xlPSJpbWciIHZpZXdCb3g9IjAgMCAyNCAyNCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48cGF0aCBmaWxsPSJ3aGl0ZSIgZD0iTTIyLjI4MTkgOS44MjExYTUuOTg0NyA1Ljk4NDcgMCAwIDAtLjUxNTctNC45MTA4IDYuMDQ2MiA2LjA0NjIgMCAwIDAtNi41MDk4LTIuOUE2LjA2NTEgNi4wNjUxIDAgMCAwIDQuOTgwNyA0LjE4MThhNS45ODQ3IDUuOTg0NyAwIDAgMC0zLjk5NzcgMi45IDYuMDQ2MiA2LjA0NjIgMCAwIDAgLjc0MjcgNy4wOTY2IDUuOTggNS45OCAwIDAgMCAuNTExIDQuOTEwNyA2LjA1MSA2LjA1MSAwIDAgMCA2LjUxNDYgMi45MDAxQTUuOTg0NyA1Ljk4NDcgMCAwIDAgMTMuMjU5OSAyNGE2LjA1NTcgNi4wNTU3IDAgMCAwIDUuNzcxOC00LjIwNTggNS45ODk0IDUuOTg5NCAwIDAgMCAzLjk5NzctMi45MDAxIDYuMDU1NyA2LjA1NTcgMCAwIDAtLjc0NzUtNy4wNzI5em0tOS4wMjIgMTIuNjA4MWE0LjQ3NTUgNC40NzU1IDAgMCAxLTIuODc2NC0xLjA0MDhsLjE0MTktLjA4MDQgNC43NzgzLTIuNzU4MmEuNzk0OC43OTQ4IDAgMCAwIC4zOTI3LS42ODEzdi02LjczNjlsMi4wMiAxLjE2ODZhLjA3MS4wNzEgMCAwIDEgLjAzOC4wNTJ2NS41ODI2YTQuNTA0IDQuNTA0IDAgMCAxLTQuNDk0NSA0LjQ5NDR6bS05LjY2MDctNC4xMjU0YTQuNDcwOCA0LjQ3MDggMCAwIDEtLjUzNDYtMy4wMTM3bC4xNDIuMDg1MiA0Ljc4MyAyLjc1ODJhLjc3MTIuNzcxMiAwIDAgMCAuNzgwNiAwbDUuODQyOC0zLjM2ODV2Mi4zMzI0YS4wODA0LjA4MDQgMCAwIDEtLjAzMzIuMDYxNUw5Ljc0IDE5Ljk1MDJhNC40OTkyIDQuNDk5MiAwIDAgMS02LjE0MDgtMS42NDY0ek0yLjM0MDggNy44OTU2YTQuNDg1IDQuNDg1IDAgMCAxIDIuMzY1NS0xLjk3MjhWMTEuNmEuNzY2NC43NjY0IDAgMCAwIC4zODc5LjY3NjVsNS44MTQ0IDMuMzU0My0yLjAyMDEgMS4xNjg1YS4wNzU3LjA3NTcgMCAwIDEtLjA3MSAwbC00LjgzMDMtMi43ODY1QTQuNTA0IDQuNTA0IDAgMCAxIDIuMzQwOCA3Ljg3MnptMTYuNTk2MyAzLjg1NThMMTMuMTAzOCA4LjM2NCAxNS4xMTkyIDcuMmEuMDc1Ny4wNzU3IDAgMCAxIC4wNzEgMGw0LjgzMDMgMi43OTEzYTQuNDk0NCA0LjQ5NDQgMCAwIDEtLjY3NjUgOC4xMDQydi01LjY3NzJhLjc5Ljc5IDAgMCAwLS40MDctLjY2N3ptMi4wMTA3LTMuMDIzMWwtLjE0Mi0uMDg1Mi00Ljc3MzUtMi43ODE4YS43NzU5Ljc3NTkgMCAwIDAtLjc4NTQgMEw5LjQwOSA5LjIyOTdWNi44OTc0YS4wNjYyLjA2NjIgMCAwIDEgLjAyODQtLjA2MTVsNC44MzAzLTIuNzg2NmE0LjQ5OTIgNC40OTkyIDAgMCAxIDYuNjgwMiA0LjY2ek04LjMwNjUgMTIuODYzbC0yLjAyLTEuMTYzOGEuMDgwNC4wODA0IDAgMCAxLS4wMzgtLjA1NjdWNi4wNzQyYTQuNDk5MiA0LjQ5OTIgMCAwIDEgNy4zNzU3LTMuNDUzN2wtLjE0Mi4wODA1TDguNzA0IDUuNDU5YS43OTQ4Ljc5NDggMCAwIDAtLjM5MjcuNjgxM3ptMS4wOTc2LTIuMzY1NGwyLjYwMi0xLjQ5OTggMi42MDY5IDEuNDk5OHYyLjk5OTRsLTIuNTk3NCAxLjQ5OTctMi42MDY3LTEuNDk5N1oiLz48L3N2Zz4=)
![Antigravity](https://img.shields.io/badge/Antigravity-111?logo=google)
![GitHubCopilot](https://img.shields.io/badge/GitHub_Copilot-111?logo=githubcopilot)

<img width="1000" alt="logo" align="center" src="https://github.com/user-attachments/assets/864aa12e-8fd0-4b3a-9794-15cecdf95f94" />
<p align="right"><sub>(Logo inspired by Daft Punk's <i>Homework</i> album art)</sub></p>

CLIを通じて、AIエージェントがUnityプロジェクトのコンパイル・テスト・操作を各種LLMツールから実行できるようにします。

AI駆動の開発ループを既存のUnityプロジェクト内で自律的に回し続けるために設計されています。

> **Note**: このプロジェクトの旧名称は **uLoopMCP** です。

# コンセプト
Unity CLI Loopは、「AIがUnityプロジェクトの実装をできるだけ人手を介さずに進められる」ことを目指して作られた Unity連携ツールです。
人間が手で行っていたコンパイル、Test Runner の実行、ログ確認、シーン編集、画面キャプチャによるUIレイアウト確認などの作業を、LLM ツールからまとめて操作できるようにします。

Unity CLI Loopのコアとなるコンセプトは次の3つです。

1. **AIが自律的にビルド・テスト・ログ解析・修正を回し続ける「自律開発ループ」** — `compile`, `run-tests`, `get-logs`, `clear-console`
2. **シーン構築、オブジェクト操作、メニュー実行、スクリーンショットからのUI改善など、Unity Editorの操作をAIに委任** — `execute-dynamic-code`, `execute-menu-item`, `screenshot`
3. **PlayMode中のUI自動テスト — ボタンクリック、ドラッグ、ゲーム動作の検証をAIが実行**（現在はマウスシミュレーションが中心、キーボード入力やタッチジェスチャーなど今後拡充予定） — `simulate-mouse`, `execute-dynamic-code`, `screenshot`

https://github.com/user-attachments/assets/569a2110-7351-4cf3-8281-3a83fe181817

# インストール

> [!WARNING]
> 以下のソフトウェアが必須です
>
> - **Unity 2022.3以上**
> - **Node.js 22.0以上** - CLIの実行に必要
> - [公式サイト](https://nodejs.org/en/download)やお好みのバージョンマネージャー等でインストールしてください

## Unity Package Manager経由

1. Unity Editorを開く
2. Window > Package Managerを開く
3. 「+」ボタンをクリック
4. 「Add package from git URL」を選択
5. 以下のURLを入力：
```text
https://github.com/hatayama/unity-cli-loop.git?path=/Packages/src
```

> **v1.0.0以前にgit URL でインストールされていた方へ**: v1.0.0でリポジトリ名が `uLoopMCP` から `unity-cli-loop` に変更されました。`manifest.json` の URL を更新してください：
> ```text
> 旧: https://github.com/hatayama/uLoopMCP.git?path=/Packages/src
> 新: https://github.com/hatayama/unity-cli-loop.git?path=/Packages/src
> ```
> 旧URLはGitHubのリダイレクトにより引き続き動作しますが、更新を推奨します。OpenUPMユーザーは影響ありません。

## OpenUPM経由（推奨）

## Unity Package ManagerでScoped registryを使用
1. Project Settingsウィンドウを開き、Package Managerページに移動
2. Scoped Registriesリストに以下のエントリを追加：
```text
Name: OpenUPM
URL: https://package.openupm.com
Scope(s): io.github.hatayama.uloopmcp
```
<img width="585" height="317" alt="image" src="https://github.com/user-attachments/assets/b9e0aab3-5379-405f-9b97-e7456f42bc77" />

3. Package Managerウィンドウを開き、My RegistriesセクションのOpenUPMを選択。Unity CLI Loopが表示されます。

# クイックスタート

## ステップ1: CLIのインストール

Window > uLoopを選択します。専用ウィンドウが開くので **CLI** ボタンが青くなっている事を確認します。

**Install CLI** ボタンを押します。  
<img width="277" height="327" alt="CleanShot 2026-02-26 at 20 14 25" src="https://github.com/user-attachments/assets/680b9586-6323-4bde-a2f0-1c3166f0c224" />


下記の表示になれば成功です。  
<img width="272" height="332" alt="CleanShot 2026-02-26 at 20 17 08" src="https://github.com/user-attachments/assets/1681b124-fac8-4ac9-8ea3-3e8651be9128" />



<details>
<summary>terminalからinstallする場合はこちら</summary>

```bash
npm install -g uloop-cli
```

詳細は [npm の uloop-cli パッケージ](https://www.npmjs.com/package/uloop-cli) を参照してください。
</details>

## ステップ2: Skillsのインストール

Claude CodeやCodexなど、対象を選択して **Install Skills** ボタンを押します。  
<img width="272" height="328" alt="CleanShot 2026-02-26 at 20 20 42" src="https://github.com/user-attachments/assets/79b9514c-cdbf-4eb6-89e9-650ecd3f6f85" />


<details> 
<summary>terminalからinstallする場合はこちら</summary>

```bash
# Claude Code のプロジェクトにインストール
uloop skills install --claude

# OpenAI Codex のプロジェクトにインストール
uloop skills install --codex

# または、グローバルにインストール
uloop skills install --claude --global
```
</details>

これで完了です！Skillsをインストールすると、LLMツールが以下のような指示に自動で対応できるようになります：

| あなたの指示 | LLMツールが使うSkill |
|---|---|
| 「このプロジェクトのUnityを起動して」 | `/uloop-launch` |
| 「コンパイルエラーを直して」 | `/uloop-compile` |
| 「テストを実行して失敗原因を教えて」 | `/uloop-run-tests` + `/uloop-get-logs` |
| 「シーンの階層構造を確認して」 | `/uloop-get-hierarchy` |
| 「Prefabを検索して」 | `/uloop-unity-search` |
| 「Unityを再生させて、Unityを前面に出して」 | `/uloop-control-play-mode` + `/uloop-focus-window` |
| 「Prefabのパラメータを一括修正して」 | `/uloop-execute-dynamic-code` |
| 「Game Viewのスクショを撮って、UIレイアウトを調整して」 | `/uloop-screenshot` + `/uloop-execute-dynamic-code` |


<details>
<summary>バンドルされている全16個のSkills一覧</summary>

- `/uloop-launch` - 正しいバージョンでUnityを起動
- `/uloop-compile` - コンパイルの実行
- `/uloop-get-logs` - Consoleログの取得
- `/uloop-run-tests` - テストの実行
- `/uloop-clear-console` - Consoleのクリア
- `/uloop-focus-window` - Unity Editorを前面に表示
- `/uloop-get-hierarchy` - シーン階層の取得
- `/uloop-unity-search` - Unity Search検索
- `/uloop-get-menu-items` - メニュー項目の取得
- `/uloop-execute-menu-item` - メニュー項目の実行
- `/uloop-find-game-objects` - GameObject検索
- `/uloop-screenshot` - EditorWindowのキャプチャ
- `/uloop-simulate-mouse` - PlayMode UIのマウス操作シミュレーション
- `/uloop-control-play-mode` - Play Modeの制御
- `/uloop-execute-dynamic-code` - 動的C#コード実行
- `/uloop-get-unity-search-providers` - 検索プロバイダー詳細

</details>

<details>
<summary>CLIの直接利用（上級者向け）</summary>

Skillsを使わずにCLIを直接呼び出すこともできます：

```bash
# 利用可能なツール一覧を取得
uloop list

# 正しいバージョンでUnityプロジェクトを起動
uloop launch

# ビルドターゲットを指定して起動（Android, iOS, StandaloneOSX など）
uloop launch -p Android

# 実行中のUnityを終了して再起動
uloop launch -r

# コンパイルを実行
uloop compile

# コンパイルしてDomain Reload完了まで待つ
uloop compile --wait-for-domain-reload true

# ログを取得
uloop get-logs --max-count 10

# テストを実行
uloop run-tests --filter-type all

# 動的コードを実行
uloop execute-dynamic-code --code 'using UnityEngine; Debug.Log("Hello from CLI!");'
```

</details>

<details>
<summary>シェル補完（オプション）</summary>

Bash/Zsh/PowerShell の補完機能をインストールできます：

```bash
# 補完スクリプトをシェル設定に追加（シェル自動検出）
uloop completion --install

# シェルを明示的に指定（Windows環境で自動検出が失敗する場合）
uloop completion --shell bash --install        # Git Bash / MINGW64
uloop completion --shell powershell --install  # PowerShell

# 補完スクリプトを確認
uloop completion
```

</details>

## プロジェクトパス指定 / ポート指定

`--project-path` / `--port` を省略した場合は、カレントディレクトリの Unity プロジェクトで設定されたポートが自動選択されます。

一つのLLMツールから複数のUnityインスタンスを操作したい場合、プロジェクトパスまたはポートを明示的に指定します：

```bash
# プロジェクトパスで指定（絶対パス・相対パスどちらも可）
uloop compile --project-path /Users/foo/my-unity-project
uloop compile --project-path ../other-project

# ポート番号で指定
uloop compile --port {target-port}
```

> [!NOTE]
> - `--project-path` と `--port` は同時に指定できません。
> - ポート番号は各Unityの uLoopMCP Window で確認できます。

<details>
<summary>CLIの代わりにMCPを使う場合</summary>

> [!WARNING]
> MCP接続は将来のリリースでメンテナンスが終了、または廃止される可能性があります。CLIの利用を推奨します。

MCP（Model Context Protocol）経由でも接続できます。MCPの場合、CLIやSkillsのインストールは不要です。

### MCP接続の手順

1. Window > uLoopを選択します。専用ウィンドウが開くので **MCP** ボタンを押してください。
<img width="274" height="289" alt="CleanShot 2026-02-26 at 20 37 16" src="https://github.com/user-attachments/assets/5f2fc5db-fd33-4b5d-9f0e-3e2e0d134cf6" />

2. 次に、LLM Tool SettingsセクションでターゲットIDEを選択します。黄色い「Configure {LLM Tool名}」ボタンを押してIDEに自動接続してください。
<img width="335" alt="image" src="https://github.com/user-attachments/assets/25f1f4f9-e3c8-40a5-a2f3-903f9ed5f45b" />

3. IDE接続確認
  - 例えばCursorの場合、設定ページのTools & MCPを確認し、uLoopMCPを見つけてください。トグルをクリックしてMCPを有効にします。

<img width="657" height="399" alt="image" src="https://github.com/user-attachments/assets/5137491d-0396-482f-b695-6700043b3f69" />

> [!WARNING]
> **Windsurfについて**
> プロジェクト単位の設定ができず、global設定のみとなります。

<details>
<summary>手動設定（通常は不要）</summary>

> [!NOTE]
> 通常は自動設定で十分ですが、必要に応じて、設定ファイル（`mcp.jsonなど`）を手動で編集できます：

```json
{
  "mcpServers": {
    "uLoopMCP": {
      "command": "node",
      "args": [
        "[Unity Package Path]/TypeScriptServer~/dist/server.bundle.js"
      ],
      "env": {
        "UNITY_TCP_PORT": "{port}"
      }
    }
  }
}
```

**パス例**:
- **Package Manager経由**: `"/Users/username/UnityProject/Library/PackageCache/io.github.hatayama.uloopmcp@[hash]/TypeScriptServer~/dist/server.bundle.js"`
> [!NOTE]
> Package Manager経由でインストールした場合、パッケージはハッシュ化されたディレクトリ名で`Library/PackageCache`に配置されます。「Auto Configure Cursor」ボタンを使用すると、正しいパスが自動的に設定されます。

</details>

### 複数のUnityインスタンスのサポート
> [!NOTE]
> ポート番号を変更することで複数のUnityインスタンスをサポートできます。Unity CLI Loop起動時に自動的に使われていないportが割り当てられます。

</details>

# 主要機能
## 自律開発ループ系ツール
### 1. compile - コンパイルの実行
AssetDatabase.Refresh()をした後、コンパイルして結果を返却します。内蔵のLinterでは発見できないエラー・警告を見つける事ができます。
差分コンパイルと強制全体コンパイルを選択できます。
`WaitForDomainReload=true` を指定すると、`ForceRecompile` の値に関係なく Domain Reload完了後に結果を返せます。
```text
→ compile実行、エラー・警告内容を解析
→ 該当ファイルを自動修正
→ 再度compileで確認
```

### 2. get-logs - UnityのConsoleと同じ内容のLogを取得します
LogTypeや検索対象の文字列で絞り込む事ができます。また、stacktraceの有無も選択できます。
これにより、コンテキストを小さく保ちながらlogを取得できます。
**MaxCountの動作**: 最新のログから指定数を取得します（tail的な動作）。MaxCount=10なら最新の10件のログを返します。
**高度な検索機能**:
- **正規表現サポート**: `UseRegex: true`で強力なパターンマッチングが可能
- **スタックトレース検索**: `SearchInStackTrace: true`でスタックトレース内も検索対象
```
→ get-logs (LogType: Error, SearchText: "NullReference", MaxCount: 10)
→ get-logs (LogType: All, SearchText: "(?i).*error.*", UseRegex: true, MaxCount: 20)
→ get-logs (LogType: All, SearchText: "MyClass", SearchInStackTrace: true, MaxCount: 50)
→ スタックトレースから原因箇所を特定、該当コードを修正
```

### 3. run-tests - TestRunnerの実行 (PlayMode, EditMode対応)
Unity Test Runnerを実行し、テスト結果を取得します。FilterTypeとFilterValueで条件を設定できます。
- FilterType: all（全テスト）、exact（個別テストメソッド名）、regex（クラス名や名前空間）、assembly（アセンブリ名）
- FilterValue: フィルタータイプに応じた値（クラス名、名前空間など）
テスト結果をxmlで出力する事が可能です。出力pathを返すので、それをAIに読み取ってもらう事ができます。
これもコンテキストを圧迫しないための工夫です。
```text
→ run-tests (FilterType: exact, FilterValue: "PlayerControllerTests.TestJump")
→ 失敗したテストを確認、実装を修正してテストをパス
```
> [!WARNING]
> PlayModeテスト実行の際、Domain Reloadは強制的にOFFにされます。(テスト終了後に元の設定に戻ります)
> この際、Static変数がリセットされない事に注意して下さい。

## Unity Editor 自動化・探索ツール
### 4. clear-console - ログのクリーンアップ
log検索時、ノイズのとなるlogをクリアする事ができます。
```text
→ clear-console
→ 新しいデバッグセッションを開始
```

### 5. unity-search - UnitySearchによるプロジェクト検索
[UnitySearch](https://docs.unity3d.com/ja/2022.3/Manual/search-overview.html)を使うことができます。
```text
→ unity-search (SearchQuery: "*.prefab")
→ 特定の条件に合うPrefabをリストアップ
→ 問題のあるPrefabを特定する
```

### 6. get-unity-search-providers - UnitySearch検索プロバイダーの確認
UnitySearchが提供する検索プロバイダーを取得します
```text
→ 各プロバイダーの機能を理解、最適な検索方法を選択
```

### 7. get-menu-items - メニュー項目の取得
[MenuItem("xxx")]属性で定義されたメニュー項目を取得します。文字列指定でフィルター出来ます。

### 8. execute-menu-item - メニュー項目の実行
[MenuItem("xxx")]属性で定義されたメニュー項目を実行できます。
```text
→ project固有のツールを実行
→ get-logsで結果を確認
```

### 9. find-game-objects - シーン内オブジェクト検索
オブジェクトを取得し、コンポーネントのパラメータを調べます。また、Unity Editorで選択中のGameObject（複数可）の情報も取得できます。
```text
→ find-game-objects (RequiredComponents: ["Camera"])
→ Cameraコンポーネントのパラメータを調査

→ find-game-objects (SearchMode: "Selected")
→ Unity Editorで選択中のGameObjectの詳細情報を取得（複数選択対応）
```

### 10. get-hierarchy - シーン構造の解析
現在アクティブなHierarchyの情報をネストされたJSON形式で取得します。ランタイムでも動作します。
**自動ファイル出力**: 取得したHierarchyは常に`{project_root}/.uloop/outputs/HierarchyResults/`ディレクトリにJSONとして保存されます。レスポンスにはファイルパスのみが返るため、大量データでもトークン消費を最小限に抑えられます。
**選択モード**: `UseSelection: true` を指定すると、Unity Editorで選択中のGameObjectから階層を取得できます。複数選択にも対応 - 親子両方が選択されている場合、重複を避けるため親のみがルートとして使用されます。
```text
→ GameObject間の親子関係を理解。構造的な問題を発見・修正
→ シーンの規模にかかわらず、Hierarchyデータはファイルに保存され、生のJSONの代わりにパスが返されます
→ get-hierarchy (UseSelection: true)
→ パスを手動で指定せずに、選択中のGameObjectの階層を取得
```

### 11. focus-window - Unity Editorウィンドウを前面化（macOS / Windows対応）
macOS / Windows Editor上で、Unity Editor ウィンドウを最前面に表示させます。
他アプリにフォーカスが奪われた後でも、視覚的なフィードバックをすぐ確認できます。（Linuxは未対応）

### 12. screenshot - EditorWindowのスクリーンショット
任意のEditorWindowのスクリーンショットをPNGとして保存します。ウィンドウ名（タイトルバーに表示されている文字列）を指定してキャプチャできます。
同じ種類のウィンドウが複数開いている場合（例：Inspectorを3つ開いている場合）、すべてのウィンドウを連番で保存します。
3つのマッチングモードをサポート: `exact`（デフォルト）、`prefix`、`contains` - すべて大文字小文字を区別しません。
```text
→ screenshot (WindowName: "Console")
→ Console画面の状態をPNGで保存
→ AIに視覚的なフィードバックを提供
```

### 13. control-play-mode - Play Modeの制御
Unity EditorのPlay Modeを制御します。Play（再生開始/一時停止解除）、Stop（停止）、Pause（一時停止）の3つのアクションを実行できます。
```
→ control-play-mode (Action: Play)
→ Play Modeを開始してゲームの動作を確認
→ control-play-mode (Action: Pause)
→ 一時停止して状態を確認
```

### 14. execute-dynamic-code - 動的C#コード実行
Unity Editor内で動的にC#コードを実行します。

> **⚠️ 重要な前提条件**
> このツールを使用するには、[OpenUPM NuGet](https://openupm.com/nuget/)を使用して`Microsoft.CodeAnalysis.CSharp`パッケージをインストールする必要があります。

<details>
<summary>Microsoft.CodeAnalysis.CSharpのインストール手順を見る</summary>

**インストール手順:**

OpenUPM経由（推奨）で、Unity Package Manager の Scoped Registry を使用します。

1. Project Settingsウィンドウを開き、Package Managerページに移動
2. Scoped Registriesリストに以下のエントリを追加：

```yaml
Name: OpenUPM
URL: https://package.openupm.com
Scope(s): org.nuget
```

3. Package Managerウィンドウを開き、My RegistriesセクションのOpenUPMを選択。Microsoft.CodeAnalysis.CSharpをインストールします。

</details>

**Async対応**:
- スニペット内で await が利用可能です（Task / ValueTask / UniTask など awaitable 全般）
- CancellationToken をツールに渡すと、キャンセルが末端まで伝播します

**セキュリティレベル対応**: 3段階のセキュリティ制御を実装し、実行可能なコードを段階的に制限：

  - **Level 0 - Disabled（無効化）**
    - コンパイル・実行ともに不可

  - **Level 1 - Restricted（制限付き）**【推奨設定】
    - 基本的に全てのUnity APIと.NET標準ライブラリが利用可能
    - ユーザー定義アセンブリ（Assembly-CSharp等）も利用可能
    - セキュリティ上危険な操作のみをピンポイントでブロック：
      - **ファイル削除系**: `File.Delete`, `Directory.Delete`, `FileUtil.DeleteFileOrDirectory`
      - **ファイル書き込み系**: `File.WriteAllText`, `File.WriteAllBytes`, `File.Replace`
      - **ネットワーク通信**: `HttpClient`, `WebClient`, `WebRequest`, `Socket`, `TcpClient`全般
      - **プロセス実行**: `Process.Start`, `Process.Kill`
      - **動的コード実行**: `Assembly.Load*`, `Type.InvokeMember`, `Activator.CreateComInstanceFrom`
      - **スレッド操作**: `Thread`, `Task`の直接操作
      - **レジストリ操作**: `Microsoft.Win32`名前空間全般
    - 安全な操作は許可：
      - ファイル読み取り（`File.ReadAllText`, `File.Exists`等）
      - パス操作（`Path.*`全般）
      - 情報取得（`Assembly.GetExecutingAssembly`, `Type.GetType`等）
    - 用途：通常のUnity開発、安全性を確保した自動化

  - **Level 2 - FullAccess（フルアクセス）**
    - **全てのアセンブリが利用可能（制限なし）**
    - ⚠️ **警告**: セキュリティリスクがあるため、信頼できるコードのみで使用
```
→ execute-dynamic-code (Code: "GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube); return \"Cube created\";")
→ プロトタイプの迅速な検証、バッチ処理の自動化
→ セキュリティレベルに応じてUnity APIの利用を制限
```


> [!IMPORTANT]
> **セキュリティ設定について**
>
> 一部のツールはセキュリティ上の理由でデフォルトで無効化されています。
> これらのツールを使用するには、uLoopMCPウィンドウの「Security Settings」で該当する項目を有効化してください：
>
> **基本セキュリティ設定**:
> - **Allow Tests Execution**: `run-tests`ツールを有効化
> - **Allow Menu Item Execution**: `execute-menu-item`ツールを有効化
> - **Allow Third Party Tools**: ユーザーが独自に拡張したtoolを有効化
>
> **Dynamic Code Security Level** (`execute-dynamic-code`ツール):
> - **Level 0 (Disabled)**: コード実行完全無効化（最も安全）
> - **Level 1 (Restricted)**: Unity APIのみ、危険な操作はブロック（推奨）
> - **Level 2 (FullAccess)**: 全APIが利用可能（注意して使用）
>
> 設定変更は即座に反映され、サーバー再起動は不要です。
>

### PlayMode 自動テスト系ツール
### 15. simulate-mouse - PlayMode UIのマウス操作シミュレーション
PlayMode中のUI要素に対してマウスクリック・長押し・ドラッグをシミュレーションします。EventSystemとExecuteEventsを使ってポインタイベントを直接ディスパッチするため、旧Input System・新Input Systemの両方に依存せず動作します。

6つのアクションに対応: Click、LongPress、Drag（ワンショット）、DragStart/DragMove/DragEnd（分割ドラッグ）

```text
→ screenshot (CaptureMode: rendering, AnnotateElements: true)
→ AnnotatedElementsから要素の座標（SimX/SimY）を取得
→ simulate-mouse (Action: Click, X: 400, Y: 300)
→ simulate-mouse (Action: LongPress, X: 400, Y: 300, Duration: 5.0)
→ simulate-mouse (Action: Drag, FromX: 100, FromY: 500, X: 400, Y: 300)
→ simulate-mouse (Action: DragStart, X: 100, Y: 500)
→ simulate-mouse (Action: DragMove, X: 200, Y: 400, DragSpeed: 300)
→ simulate-mouse (Action: DragEnd, X: 400, Y: 300)
```
https://github.com/user-attachments/assets/c7ee9103-c282-4f90-8b01-64bb17400f3e


## ツールリファレンス

全ツールの詳細仕様（パラメータ、レスポンス、使用例）については **[TOOL_REFERENCE_ja.md](/Packages/src/TOOL_REFERENCE_ja.md)** を参照してください。

## Unity CLI Loop 拡張ツールの開発
Unity CLI Loopはコアパッケージへの変更を必要とせず、プロジェクト固有のツールを効率的に開発できます。
型安全な設計により、信頼性の高いカスタムツールを短時間で実装可能です。
(AIに依頼すればすぐに作ってくれるはずです✨)

開発した拡張ツールはGitHubで公開し、他のプロジェクトでも再利用できます。公開例は [uLoopMCP-extensions-sample](https://github.com/hatayama/uLoopMCP-extensions-sample) を参照してください。

> [!TIP]
> **AI支援開発向け**: 詳細な実装ガイドが [.claude/rules/mcp-tools.md](/.claude/rules/mcp-tools.md)（ツール開発用）と [.claude/rules/cli.md](/.claude/rules/cli.md)（CLI/Skills開発用）に用意されています。これらのガイドは、Claude Codeが該当ディレクトリで作業する際に自動的に読み込まれます。

> [!IMPORTANT]
> **セキュリティ設定について**
>
> プロジェクト固有に開発したツールは、uLoopMCPウィンドウの「Security Settings」で **Allow Third Party Tools** を有効化する必要があります。
> また、動的コード実行を含むカスタムツールを開発する場合は、**Dynamic Code Security Level**の設定も考慮してください。

<details>
<summary>実装ガイドを見る</summary>

**ステップ1: スキーマクラスの作成**（パラメータを定義）：
```csharp
using System.ComponentModel;

public class MyCustomSchema : BaseToolSchema
{
    [Description("パラメータの説明")]
    public string MyParameter { get; set; } = "default_value";

    [Description("Enumパラメータの例")]
    public MyEnum EnumParameter { get; set; } = MyEnum.Option1;
}

public enum MyEnum
{
    Option1 = 0,
    Option2 = 1,
    Option3 = 2
}
```

**ステップ2: レスポンスクラスの作成**（返却データを定義）：
```csharp
public class MyCustomResponse : BaseToolResponse
{
    public string Result { get; set; }
    public bool Success { get; set; }

    public MyCustomResponse(string result, bool success)
    {
        Result = result;
        Success = success;
    }

    // 必須のパラメータなしコンストラクタ
    public MyCustomResponse() { }
}
```

**ステップ3: ツールクラスの作成**：
```csharp
using System.Threading;
using System.Threading.Tasks;

[McpTool(Description = "私のカスタムツールの説明")]  // ← この属性により自動登録されます
public class MyCustomTool : AbstractUnityTool<MyCustomSchema, MyCustomResponse>
{
    public override string ToolName => "my-custom-tool";

    // メインスレッドで実行されます
    protected override Task<MyCustomResponse> ExecuteAsync(MyCustomSchema parameters, CancellationToken cancellationToken)
    {
        // 型安全なパラメータアクセス
        string param = parameters.MyParameter;
        MyEnum enumValue = parameters.EnumParameter;

        // 長時間実行される処理の前にキャンセレーションをチェック
        cancellationToken.ThrowIfCancellationRequested();

        // カスタムロジックをここに実装
        string result = ProcessCustomLogic(param, enumValue);
        bool success = !string.IsNullOrEmpty(result);

        // 長時間実行される処理では定期的にキャンセレーションをチェック
        // cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new MyCustomResponse(result, success));
    }

    private string ProcessCustomLogic(string input, MyEnum enumValue)
    {
        // カスタムロジックを実装
        return $"Processed '{input}' with enum '{enumValue}'";
    }
}
```

> [!IMPORTANT]
> **重要事項**：
> - **スレッドセーフティ**: ツールはUnityのメインスレッドで実行されるため、追加の同期なしにUnity APIを安全に呼び出せます。

[カスタムツールのサンプル](/Assets/Editor/CustomToolSamples)も参考にして下さい。

</details>

### カスタムツール用 Skills

カスタムツールを作成した際、ツールフォルダ内に `Skill/` サブフォルダを作成し、`SKILL.md` ファイルを配置することで、LLMツールがSkillsシステムを通じて自動的にカスタムツールを認識・使用できるようになります。

**仕組み:**
1. カスタムツールのフォルダ内に `Skill/` サブフォルダを作成
2. `Skill/` フォルダ内に `SKILL.md` ファイルを配置
3. `uloop skills install --claude` を実行（バンドル + プロジェクトのSkillsをまとめてインストール）
4. LLMツールがカスタムSkillを自動認識

**ディレクトリ構造:**
```
Assets/Editor/CustomTools/MyTool/
├── MyTool.cs           # ツール実装
└── Skill/
    ├── SKILL.md        # スキル定義（必須）
    └── references/     # 追加ファイル（オプション）
        └── usage.md
```

**SKILL.md のフォーマット:**
```markdown
---
name: uloop-my-custom-tool
description: "ツールの説明と使用タイミング"
---

# uloop my-custom-tool

ツールの詳細ドキュメント...
```

**スキャン対象**（`Skill/SKILL.md` ファイルを検索）:
- `Assets/**/Editor/<ToolFolder>/Skill/SKILL.md`
- `Packages/*/Editor/<ToolFolder>/Skill/SKILL.md`
- `Library/PackageCache/*/Editor/<ToolFolder>/Skill/SKILL.md`

> [!TIP]
> - フロントマターに `internal: true` を追加すると、インストール対象から除外されます（内部ツールやデバッグ用ツールに便利）
> - `Skill/` フォルダ内の追加ファイル（`references/`、`scripts/`、`assets/` など）もインストール時に一緒にコピーされます

完全な例は [HelloWorld サンプル](/Assets/Editor/CustomCommandSamples/HelloWorld/Skill/SKILL.md) を参照してください。

より実践的なサンプルプロジェクトは [uLoopMCP-extensions-sample](https://github.com/hatayama/uLoopMCP-extensions-sample) を参照してください。

## その他

> [!TIP]
> **ファイル出力について**
>
> `run-tests`、`unity-search`、`get-hierarchy`の各ツールは、大量のデータによるトークン消費を避けるため、結果を`{project_root}/.uloop/outputs/`ディレクトリにファイル保存する機能があります。
> **推奨**: `.gitignore`に`.uloop/`を追加してバージョン管理から除外してください。

## ライセンス
MIT License
