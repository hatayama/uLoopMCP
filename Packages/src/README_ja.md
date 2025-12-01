[English](/Packages/src/README.md)

[![Unity](https://img.shields.io/badge/Unity-2022.3+-red.svg)](https://unity3d.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.md)<br>
![ClaudeCode](https://img.shields.io/badge/Claude_Code-555?logo=claude)
![Cursor](https://img.shields.io/badge/Cursor-111?logo=Cursor)
![OpenAICodex](https://img.shields.io/badge/OpenAI_Codex-111?logo=openai)
![GoogleGemini](https://img.shields.io/badge/Google_Gemini-111?logo=googlegemini)
![GitHubCopilot](https://img.shields.io/badge/GitHub_Copilot-111?logo=githubcopilot)
![Windsurf](https://img.shields.io/badge/Windsurf-111?logo=Windsurf)

<h1 align="center">
    <img width="500" alt="uLoopMCP" src="https://github.com/user-attachments/assets/a8b53cca-5444-445d-aa39-9024d41763e6" />  
</h1>  

様々なLLMツールからUnity Editorを操作する事ができます。

AIによる開発サイクルを高速に回すことで、継続的な改善Loopを実現します。

# コンセプト
uLoopMCPは、「AIがUnityプロジェクトの実装をできるだけ人手を介さずに進められる」ことを目指して作られた MCP サーバーです。
人間が手で行っていたコンパイル、Test Runner の実行、ログ確認、シーン編集などの作業を、LLM ツールからまとめて操作できるようにします。

uLoopMCPのコアとなるコンセプトは次の2つです。

1. **compile / run-tests / get-logs / clear-console などを組み合わせて、AIが自分でビルド・テスト・ログ解析を回し続けられる「自律開発ループ」を提供すること**
2. **execute-dynamic-code や execute-menu-item などを使って、Unity Editorの操作（シーン構築、メニュー実行、オブジェクト操作など）まで AIに任せられること**

# 特徴
1. AI がコンパイル → テスト実行 → ログ解析 → 再修正までを繰り返せるよう、`compile` / `run-tests` / `get-logs` / `clear-console` などのツールをひとまとめに提供します。
2. `execute-dynamic-code` を中心に、Unity Editor のメニュー実行、シーン探索、GameObject 操作などをコードから自在に自動化できます。
3. Unity Package Manager からインストールし、お使いの LLM ツール（Cursor / Claude Code / Codex / Gemini など）と数クリックで接続できます。
4. プロジェクト固有の MCP ツールを型安全に拡張しやすく、AI に実装を任せやすい設計になっています。
5. 大量のログや階層情報はファイルに書き出すことで、LLM のコンテキスト消費を抑える工夫をしています。

# ユースケース例
- Unity プロジェクトの「コンパイルが通るまで」「テストが緑になるまで」を、AI に任せて自律的に回し続ける
- 既存コードベースに対して、バグ修正やリファクタリングをAIに依頼し、`compile` / `run-tests` / `get-logs` で結果を検証させる
- 検証完了後、`execute-menu-item` または `execute-dynamic-code` でPlay Modeに入り、`focus-window` でUnity Editorを最前面に表示させる
- 大量のPrefab / GameObjectをUnity Editor上でAIに調査させ、パラメータの一括修正やシーン構造の整理を行う
- チーム専用のMCPツールを追加し、プロジェクト固有のチェックや自動修正をAIから呼び出せるようにする

# ツールwindow
<img width="308" height="401" src="https://github.com/user-attachments/assets/a6beb597-363d-4816-9286-e8bdba7e4b5c" />

- サーバーの状態を管理・モニターします
- LLMツールの接続状況を把握できます
- LLMツールの設定ボタンを押すことで、簡単にツールとの接続が可能です

## クイックスタート
1. Unity プロジェクトに uLoopMCP パッケージをインストールします。
  - Unity Package Manager の「Add package from git URL」で以下を指定します：  
    `https://github.com/hatayama/uLoopMCP.git?path=/Packages/src`
  - あるいは OpenUPM の Scoped Registry 経由でも利用できます。（詳しくは [インストール](#インストール) セクションを参照）。
2. Unity メニューから `Window > uLoopMCP` を開き、`Start Server` ボタンを押して MCP サーバーを起動します。
3. Cursor / Claude Code / GitHub Copilot など、使用している LLM ツール側で uLoopMCP を MCP として有効化します。
4. 例えば次のように指示すると、AIが自律的な開発ループを回し始めます。
  - 「このプロジェクトのコンパイルが通るように直して、`compile` でエラーが 0 になるまで繰り返して」
  - 「`run-tests` で `uLoopMCP.Tests.Editor` のテストを全部通すまで、実装とテストを更新して」
  - 「`execute-dynamic-code` でサンプルシーンに Cube を 10 個並べて、カメラ位置も自動調整して」

# 主要機能
### 自律開発ループ系ツール
#### 1. compile - コンパイルの実行
AssetDatabase.Refresh()をした後、コンパイルして結果を返却します。内蔵のLinterでは発見できないエラー・警告を見つける事ができます。  
差分コンパイルと強制全体コンパイルを選択できます。
```
→ compile実行、エラー・警告内容を解析
→ 該当ファイルを自動修正
→ 再度compileで確認
```

#### 2. get-logs - UnityのConsoleと同じ内容のLogを取得します
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

#### 3. run-tests - TestRunnerの実行 (PlayMode, EditMode対応)
Unity Test Runnerを実行し、テスト結果を取得します。FilterTypeとFilterValueで条件を設定できます。
- FilterType: all（全テスト）、fullclassname（完全クラス名）など
- FilterValue: フィルタータイプに応じた値（クラス名、名前空間など）  
テスト結果をxmlで出力する事が可能です。出力pathを返すので、それをAIに読み取ってもらう事ができます。  
これもコンテキストを圧迫しないための工夫です。
```
→ run-tests (FilterType: fullclassname, FilterValue: "PlayerControllerTests")
→ 失敗したテストを確認、実装を修正してテストをパス
```
> [!WARNING]  
> PlayModeテスト実行の際、Domain Reloadは強制的にOFFにされます。(テスト終了後に元の設定に戻ります)  
> この際、Static変数がリセットされない事に注意して下さい。

### Unity Editor 自動化・探索ツール
#### 4. clear-console - ログのクリーンアップ
log検索時、ノイズのとなるlogをクリアする事ができます。
```
→ clear-console
→ 新しいデバッグセッションを開始
```

#### 5. unity-search - UnitySearchによるプロジェクト検索
[UnitySearch](https://docs.unity3d.com/ja/2022.3/Manual/search-overview.html)を使うことができます。
```
→ unity-search (SearchQuery: "*.prefab")
→ 特定の条件に合うPrefabをリストアップ
→ 問題のあるPrefabを特定する
```

#### 6. get-provider-details - UnitySearch検索プロバイダーの確認
UnitySearchが提供する検索プロバイダーを取得します
```
→ 各プロバイダーの機能を理解、最適な検索方法を選択
```

#### 7. get-menu-items - メニュー項目の取得
[MenuItem("xxx")]属性で定義されたメニュー項目を取得します。文字列指定でフィルター出来ます。

#### 8. execute-menu-item - メニュー項目の実行
[MenuItem("xxx")]属性で定義されたメニュー項目を実行できます。
```
→ project固有のツールを実行
→ get-logsで結果を確認
```

#### 9. find-game-objects - シーン内オブジェクト検索
オブジェクトを取得し、コンポーネントのパラメータを調べます
```
→ find-game-objects (RequiredComponents: ["Camera"])
→ Cameraコンポーネントのパラメータを調査
```

#### 10. get-hierarchy - シーン構造の解析
現在アクティブなHierarchyの情報をネストされたJSON形式で取得します。ランタイムでも動作します。
**自動ファイル出力**: 取得したHierarchyは常に`{project_root}/uLoopMCPOutputs/HierarchyResults/`ディレクトリにJSONとして保存されます。MCPレスポンスにはファイルパスのみが返るため、大量データでもトークン消費を最小限に抑えられます。
```text
→ GameObject間の親子関係を理解。構造的な問題を発見・修正
→ シーンの規模にかかわらず、Hierarchyデータはファイルに保存され、生のJSONの代わりにパスが返されます
```

#### 11. focus-window - Unity Editorウィンドウを前面化（macOS / Windows対応）
macOS / Windows Editor上で、現在MCP接続中の Unity Editor ウィンドウを最前面に表示させます。  
他アプリにフォーカスが奪われた後でも、視覚的なフィードバックをすぐ確認できます。（Linuxは未対応）

#### 12. execute-dynamic-code - 動的C#コード実行
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
> **注意**: これらの機能を使ってAIによるコード生成を扱う際は、予期せぬ動作やセキュリティリスクに備えるため、sandbox環境やコンテナ上での実行を強く推奨します。

## ツールリファレンス

全ツールの詳細仕様（パラメータ、レスポンス、使用例）については **[TOOL_REFERENCE_ja.md](TOOL_REFERENCE_ja.md)** を参照してください。

## 使用方法
1. Window > uLoopMCPを選択します。専用ウィンドウが開くので、「Start Server」ボタンを押してください。  
<img width="335" alt="image" src="https://github.com/user-attachments/assets/38c67d7b-6bbf-4876-ab40-6bc700842dc4" />

3. 次に、LLM Tool SettingsセクションでターゲットIDEを選択します。黄色い「Configure {LLM Tool名}」ボタンを押してIDEに自動接続してください。  
<img width="335" alt="image" src="https://github.com/user-attachments/assets/25f1f4f9-e3c8-40a5-a2f3-903f9ed5f45b" />

4. IDE接続確認
  - 例えばCursorの場合、設定ページのTools & MCPを確認し、uLoopMCPを見つけてください。トグルをクリックしてMCPを有効にします。赤い円が表示される場合は、Cursorを再起動してください。  

<img width="657" height="399" alt="image" src="https://github.com/user-attachments/assets/5137491d-0396-482f-b695-6700043b3f69" />

> [!WARNING]  
> **Codex / Windsurfについて**  
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

5. 複数のUnityインスタンスのサポート
> [!NOTE]
> ポート番号を変更することで複数のUnityインスタンスをサポートできます。uLoopMCP起動時に自動的に使われていないportが割り当てられます。

## インストール

> [!WARNING]  
> 以下のソフトウェアが必須です
> 
> - **Unity 2022.3以上**
> - **Node.js 18.0以上** - MCPサーバー実行に必要
> - Node.jsを[こちら](https://nodejs.org/en/download)からインストールしてください

### Unity Package Manager経由

1. Unity Editorを開く
2. Window > Package Managerを開く
3. 「+」ボタンをクリック
4. 「Add package from git URL」を選択
5. 以下のURLを入力：
```
https://github.com/hatayama/uLoopMCP.git?path=/Packages/src
```

### OpenUPM経由（推奨）

### Unity Package ManagerでScoped registryを使用
1. Project Settingsウィンドウを開き、Package Managerページに移動
2. Scoped Registriesリストに以下のエントリを追加：
```
Name: OpenUPM
URL: https://package.openupm.com
Scope(s): io.github.hatayama.uloopmcp
```
<img width="585" height="317" alt="image" src="https://github.com/user-attachments/assets/b9e0aab3-5379-405f-9b97-e7456f42bc77" />

3. Package Managerウィンドウを開き、My RegistriesセクションのOpenUPMを選択。uLoopMCPが表示されます。

## プロジェクト固有のツール開発
uLoopMCPはコアパッケージへの変更を必要とせず、プロジェクト固有のMCPツールを効率的に開発できます。  
型安全な設計により、信頼性の高いカスタムツールを短時間で実装可能です。
(AIに依頼すればすぐに作ってくれるはずです✨)

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
> - **タイムアウト処理**: 全てのツールは`BaseToolSchema`から`TimeoutSeconds`パラメータを継承します。長時間実行される処理では`cancellationToken.ThrowIfCancellationRequested()`チェックを実装して、適切なタイムアウト動作を保証してください。
> - **スレッドセーフティ**: ツールはUnityのメインスレッドで実行されるため、追加の同期なしにUnity APIを安全に呼び出せます。

[カスタムツールのサンプル](/Assets/Editor/CustomToolSamples)も参考にして下さい。

</details>

## その他

> [!TIP]
> **ファイル出力について**  
> 
> `run-tests`、`unity-search`、`get-hierarchy`の各ツールは、大量のデータによるトークン消費を避けるため、結果を`{project_root}/uLoopMCPOutputs/`ディレクトリにファイル保存する機能があります。
> **推奨**: `.gitignore`に`uLoopMCPOutputs/`を追加してバージョン管理から除外してください。

> [!TIP]
> **Cursorでmcpの実行を自動で行う**  
> 
> CursorはデフォルトでMCP実行時にユーザーの許可を必要とします。
> これを無効にするには、Cursor Settings > Chat > MCP Tools ProtectionをOffにします。
> MCPの種類・ツール事に制御できず、全てのMCPが許可不要になってしまうため、セキュリティとのトレードオフになります。そこを留意して設定してください。

> [!WARNING]
> **Windows板 Claude Code**  
> 
> WindowsでClaude Codeを使う場合、1.0.51以上のバージョンを推奨します。(Git for Windows が必要です)  
> [Claude CodeのCHANGELOG](https://github.com/anthropics/claude-code/blob/main/CHANGELOG.md) を参照して下さい。

## ライセンス
MIT License
