[English](/Packages/src/README.md)

[![Unity](https://img.shields.io/badge/Unity-2022.3+-red.svg)](https://unity3d.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.md)
[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/hatayama/uLoopMCP)  
![ClaudeCode](https://img.shields.io/badge/Claude_Code-555?logo=claude)
![Cursor](https://img.shields.io/badge/Cursor-111?logo=Cursor)
![GitHubCopilot](https://img.shields.io/badge/GitHub_Copilot-111?logo=githubcopilot)
![Windsurf](https://img.shields.io/badge/Windsurf-111?logo=Windsurf)
![WSL2](https://img.shields.io/badge/WSL2-28b?logo=WSL2)

<h1 align="center">
    <img width="500" alt="uLoopMCP" src="https://github.com/user-attachments/assets/a8b53cca-5444-445d-aa39-9024d41763e6" />  
</h1>  

様々なLLMツールからUnity Editorを操作する事ができます。

AIによる開発サイクルを高速に回すことで、継続的な改善Loopを実現します。

# コンセプト
AIによるコーディングを可能な限り長時間自走させる事をコンセンプトに作りました。
通常、Unityをコンパイルさせたり、Testを走らせたり、logをAIに伝える部分は人間がやる必要があります。その面倒をuLoopMCPが解決します。

# 特徴
1. packageをinstallして、お使いのLLMツールに接続するボタンを押すだけで、すぐに使い始める事ができます。
2. 簡単に機能拡張ができます。自分専用のmcp toolをすぐに作る事が可能です。(おそらくAIに頼めばすぐに作ってくれるはずです)
3. コンテキストをなるべく消費しないようにするオプションを実装しています。 

# ツールwindow
<img width="350" alt="image" src="https://github.com/user-attachments/assets/5863b58b-7b48-48ae-9a40-c874ddc11488" />

 - サーバーの状態を管理・モニターします
 - LLMツールの接続状況を把握できます
 - LLMツールの設定ボタンを押すことで、簡単にツールとの接続が可能です

# 主要機能
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
```
→ get-logs (LogType: Error, SearchText: "NullReference", MaxCount: 10)
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
> PlayModeテスト実行の際、Domain Reloadは強制的にOFFにされます。Static変数がリセットされない事に注意して下さい。

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
→ AIにテストプログラムを生成させる
→ execute-menu-item (MenuItemPath: "Tools/xxx") で生成させたテストプログラムの実行
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
**自動ファイル出力**: 大きなHierarchy（100KB超）は自動的に`HierarchyResults/`ディレクトリに保存され、トークン消費を最小限に抑えます。
```
→ GameObject間の親子関係を理解。構造的な問題を発見・修正
→ 大きなシーンでは、Hierarchyデータがファイルに保存され、生のJSONの代わりにパスが返されます
```
<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">機能詳細仕様</span></summary>

## 共通パラメータ・レスポンス形式

すべてのUnity MCPツールは以下の共通要素を持ちます：

### 共通パラメータ
- `TimeoutSeconds` (number): ツール実行のタイムアウト時間（秒）

### 共通レスポンスプロパティ
すべてのツールには以下のタイミング情報が自動的に含まれます：
- `StartedAt` (string): ツール実行開始時刻（ローカル時間）
- `EndedAt` (string): ツール実行終了時刻（ローカル時間）  
- `ExecutionTimeMs` (number): ツール実行時間（ミリ秒）

---

## Unity コアツール

### 1. compile
- **説明**: AssetDatabase.Refresh()を実行後、コンパイルを行います。詳細なタイミング情報付きでコンパイル結果を返します。
- **パラメータ**: 
  - `ForceRecompile` (boolean): 強制再コンパイルを実行するかどうか（デフォルト: false）
- **レスポンス**: 
  - `Success` (boolean): コンパイルが成功したかどうか
  - `ErrorCount` (number): エラーの総数
  - `WarningCount` (number): 警告の総数
  - `CompletedAt` (string): コンパイル完了時刻（ISO形式）
  - `Errors` (array): コンパイルエラーの配列（存在する場合）
    - `Message` (string): エラーメッセージ
    - `File` (string): エラーが発生したファイルパス
    - `Line` (number): エラーが発生した行番号
  - `Warnings` (array): コンパイル警告の配列（存在する場合）
    - `Message` (string): 警告メッセージ
    - `File` (string): 警告が発生したファイルパス
    - `Line` (number): 警告が発生した行番号
  - `Message` (string): 追加情報のためのオプションメッセージ

### 2. get-logs
- **説明**: フィルタリングおよび検索機能付きでUnityコンソールからログ情報を取得します
- **パラメータ**: 
  - `LogType` (enum): フィルタするログタイプ - "Error", "Warning", "Log", "All"（デフォルト: "All"）
  - `MaxCount` (number): 取得するログの最大数（デフォルト: 100）
  - `SearchText` (string): ログメッセージ内で検索するテキスト（空の場合はすべて取得）（デフォルト: ""）
  - `IncludeStackTrace` (boolean): スタックトレースを表示するかどうか（デフォルト: true）
- **レスポンス**: 
  - `TotalCount` (number): 利用可能なログの総数
  - `DisplayedCount` (number): このレスポンスで表示されるログの数
  - `LogType` (string): 使用されたログタイプフィルタ
  - `MaxCount` (number): 使用された最大数制限
  - `SearchText` (string): 使用された検索テキストフィルタ
  - `IncludeStackTrace` (boolean): スタックトレースが含まれているかどうか
  - `Logs` (array): ログエントリの配列
    - `Type` (string): ログタイプ（Error, Warning, Log）
    - `Message` (string): ログメッセージ
    - `StackTrace` (string): スタックトレース（IncludeStackTraceがtrueの場合）
    - `File` (string): ログが発生したファイル名

### 3. run-tests
- **説明**: Unity Test Runnerを実行し、包括的なレポート付きでテスト結果を取得します
- **パラメータ**: 
  - `FilterType` (enum): テストフィルタのタイプ - "all"(0), "exact"(1), "regex"(2), "assembly"(3)（デフォルト: "all"）
  - `FilterValue` (string): フィルタ値（FilterTypeがall以外の場合に指定）（デフォルト: ""）
    - `exact`: 個別テストメソッド名（完全一致）（例：io.github.hatayama.uLoopMCP.ConsoleLogRetrieverTests.GetAllLogs_WithMaskAllOff_StillReturnsAllLogs）
    - `regex`: クラス名または名前空間（正規表現パターン）（例：io.github.hatayama.uLoopMCP.ConsoleLogRetrieverTests, io.github.hatayama.uLoopMCP）
    - `assembly`: アセンブリ名（例：uLoopMCP.Tests.Editor）
  - `TestMode` (enum): テストモード - "EditMode"(0), "PlayMode"(1)（デフォルト: "EditMode"）
    - ⚠️ **PlayMode注意**: PlayModeテスト実行時は、一時的にdomain reloadが無効化されます
  - `SaveXml` (boolean): テスト結果をXMLファイルとして保存するかどうか（デフォルト: false）
    - XMLファイルは `TestResults/` フォルダに保存されます（プロジェクトルート）
    - **推奨**: `.gitignore` に `TestResults/` を追加してバージョン管理から除外してください
- **レスポンス**: 
  - `Success` (boolean): テスト実行が成功したかどうか
  - `Message` (string): テスト実行メッセージ
  - `CompletedAt` (string): テスト実行完了タイムスタンプ（ISO形式）
  - `TestCount` (number): 実行されたテストの総数
  - `PassedCount` (number): 合格したテストの数
  - `FailedCount` (number): 失敗したテストの数
  - `SkippedCount` (number): スキップされたテストの数
  - `XmlPath` (string): XML結果ファイルのパス（SaveXmlがtrueの場合）

### 4. clear-console
- **説明**: クリーンな開発ワークフローのためにUnityコンソールログをクリアします
- **パラメータ**: 
  - `AddConfirmationMessage` (boolean): クリア後に確認ログメッセージを追加するかどうか（デフォルト: true）
- **レスポンス**: 
  - `Success` (boolean): コンソールクリア操作が成功したかどうか
  - `ClearedLogCount` (number): コンソールからクリアされたログの数
  - `ClearedCounts` (object): タイプ別のクリアされたログの内訳
    - `ErrorCount` (number): クリアされたエラーログの数
    - `WarningCount` (number): クリアされた警告ログの数
    - `LogCount` (number): クリアされた情報ログの数
  - `Message` (string): クリア操作結果を説明するメッセージ
  - `ErrorMessage` (string): 操作が失敗した場合のエラーメッセージ

### 5. find-game-objects
- **説明**: 高度な検索条件（コンポーネントタイプ、タグ、レイヤーなど）で複数のGameObjectを検索します
- **パラメータ**: 
  - `NamePattern` (string): 検索するGameObject名のパターン（デフォルト: ""）
  - `SearchMode` (enum): 検索モード - "Exact", "Path", "Regex", "Contains"（デフォルト: "Exact"）
  - `RequiredComponents` (array): GameObjectが持つ必要のあるコンポーネントタイプ名の配列（デフォルト: []）
  - `Tag` (string): タグフィルター（デフォルト: ""）
  - `Layer` (number): レイヤーフィルター（デフォルト: null）
  - `IncludeInactive` (boolean): 非アクティブなGameObjectを含めるかどうか（デフォルト: false）
  - `MaxResults` (number): 返す結果の最大数（デフォルト: 20）
  - `IncludeInheritedProperties` (boolean): 継承プロパティを含めるかどうか（デフォルト: false）
- **レスポンス**: 
  - `results` (array): 見つかったGameObjectの配列
    - `name` (string): GameObject名
    - `path` (string): 完全な階層パス
    - `isActive` (boolean): GameObjectがアクティブかどうか
    - `tag` (string): GameObjectタグ
    - `layer` (number): GameObjectレイヤー
    - `components` (array): GameObject上のコンポーネントの配列
      - `TypeName` (string): コンポーネントタイプ名
      - `AssemblyQualifiedName` (string): 完全なアセンブリ修飾名
      - `Properties` (object): コンポーネントプロパティ（IncludeInheritedPropertiesがtrueの場合）
  - `totalFound` (number): 見つかったGameObjectの総数
  - `errorMessage` (string): 検索が失敗した場合のエラーメッセージ

---

## Unity 検索・発見ツール

### 6. unity-search
- **説明**: Unity Search APIを使用してUnityプロジェクトを検索し、包括的なフィルタリングとエクスポートオプションを提供します
- **パラメータ**: 
  - `SearchQuery` (string): 検索クエリ文字列（Unity Search構文をサポート）（デフォルト: ""）
    - 例: "*.cs", "t:Texture2D", "ref:MyScript", "p:MyPackage"
  - `Providers` (array): 使用する特定の検索プロバイダー（空 = すべてのアクティブプロバイダー）（デフォルト: []）
    - 一般的なプロバイダー: "asset", "scene", "menu", "settings", "packages"
  - `MaxResults` (number): 返す検索結果の最大数（デフォルト: 50）
  - `IncludeDescription` (boolean): 結果に詳細な説明を含めるかどうか（デフォルト: true）
  - `IncludeMetadata` (boolean): ファイルメタデータ（サイズ、更新日）を含めるかどうか（デフォルト: false）
  - `SearchFlags` (enum): Unity Search動作を制御する検索フラグ（デフォルト: "Default"）
  - `SaveToFile` (boolean): 検索結果を外部ファイルに保存するかどうか（デフォルト: false）
  - `OutputFormat` (enum): SaveToFileが有効な場合の出力ファイル形式 - "JSON", "CSV", "TSV"（デフォルト: "JSON"）
  - `AutoSaveThreshold` (number): 自動ファイル保存の閾値（デフォルト: 100）
  - `FileExtensions` (array): ファイル拡張子で結果をフィルタ（例: "cs", "prefab", "mat"）（デフォルト: []）
  - `AssetTypes` (array): アセットタイプで結果をフィルタ（例: "Texture2D", "GameObject", "MonoScript"）（デフォルト: []）
  - `PathFilter` (string): パスパターンで結果をフィルタ（ワイルドカードサポート）（デフォルト: ""）
- **レスポンス**: 
  - `Results` (array): 検索結果アイテムの配列（結果がファイルに保存された場合は空）
  - `TotalCount` (number): 見つかった検索結果の総数
  - `DisplayedCount` (number): このレスポンスで表示される結果の数
  - `SearchQuery` (string): 実行された検索クエリ
  - `ProvidersUsed` (array): 検索に使用された検索プロバイダー
  - `SearchDurationMs` (number): 検索時間（ミリ秒）
  - `Success` (boolean): 検索が正常に完了したかどうか
  - `ErrorMessage` (string): 検索が失敗した場合のエラーメッセージ
  - `ResultsFilePath` (string): 保存された検索結果ファイルのパス（SaveToFileが有効な場合）
  - `ResultsSavedToFile` (boolean): 結果がファイルに保存されたかどうか
  - `SavedFileFormat` (string): 保存された結果のファイル形式
  - `SaveToFileReason` (string): 結果がファイルに保存された理由

### 7. get-hierarchy
- **説明**: AIフレンドリーな処理のためにUnity階層構造をネストされたJSON形式で取得します
- **パラメータ**: 
  - `IncludeInactive` (boolean): 階層結果に非アクティブなGameObjectを含めるかどうか（デフォルト: true）
  - `MaxDepth` (number): 階層を探索する最大深度（無制限深度の場合は-1）（デフォルト: -1）
  - `RootPath` (string): 階層探索を開始するルートGameObjectパス（すべてのルートオブジェクトの場合は空/null）（デフォルト: null）
  - `IncludeComponents` (boolean): 階層内の各GameObjectのコンポーネント情報を含めるかどうか（デフォルト: true）
  - `MaxResponseSizeKB` (number): ファイルに保存する前の最大レスポンスサイズ（KB）（デフォルト: 100KB）
- **レスポンス**: 
  - **小さな階層**（≤100KB）: 直接的なネストされたJSON構造
    - `hierarchy` (array): ネスト形式のルートレベルGameObjectの配列
      - `id` (number): UnityのGetInstanceID() - セッション内で一意
      - `name` (string): GameObject名
      - `depth` (number): 階層内の深度レベル（ルートは0）
      - `isActive` (boolean): GameObjectがアクティブかどうか
      - `components` (array): このGameObjectにアタッチされたコンポーネントタイプ名の配列
      - `children` (array): 同じ構造を持つ子GameObjectの再帰的配列
    - `context` (object): 階層に関するコンテキスト情報
      - `sceneType` (string): シーンタイプ（"editor", "runtime", "prefab"）
      - `sceneName` (string): シーン名またはプレハブパス
      - `nodeCount` (number): 階層内のノードの総数
      - `maxDepth` (number): 探索中に到達した最大深度
  - **大きな階層**（>100KB）: 自動ファイルエクスポート
    - `hierarchySavedToFile` (boolean): 大きな階層では常にtrue
    - `hierarchyFilePath` (string): 保存された階層ファイルの相対パス（例: "HierarchyResults/hierarchy_2025-07-10_21-30-15.json"）
    - `saveToFileReason` (string): ファイルエクスポートの理由（"auto_threshold"）
    - `context` (object): 上記と同じコンテキスト情報
  - `Message` (string): 操作メッセージ
  - `ErrorMessage` (string): 操作が失敗した場合のエラーメッセージ

### 8. get-provider-details
- **説明**: 表示名、説明、アクティブ状態、機能を含むUnity Searchプロバイダーの詳細情報を取得します
- **パラメータ**: 
  - `ProviderId` (string): 詳細を取得する特定のプロバイダーID（空 = すべてのプロバイダー）（デフォルト: ""）
    - 例: "asset", "scene", "menu", "settings"
  - `ActiveOnly` (boolean): アクティブなプロバイダーのみを含めるかどうか（デフォルト: false）
  - `SortByPriority` (boolean): 優先度でプロバイダーをソート（数値が小さい = 優先度が高い）（デフォルト: true）
  - `IncludeDescriptions` (boolean): 各プロバイダーの詳細な説明を含める（デフォルト: true）
- **レスポンス**: 
  - `Providers` (array): プロバイダー情報の配列
  - `TotalCount` (number): 見つかったプロバイダーの総数
  - `ActiveCount` (number): アクティブなプロバイダーの数
  - `InactiveCount` (number): 非アクティブなプロバイダーの数
  - `Success` (boolean): リクエストが成功したかどうか
  - `ErrorMessage` (string): リクエストが失敗した場合のエラーメッセージ
  - `AppliedFilter` (string): 適用されたフィルタ（特定のプロバイダーIDまたは"all"）
  - `SortedByPriority` (boolean): 結果が優先度でソートされているかどうか

### 9. get-menu-items
- **説明**: プログラム実行のための詳細なメタデータ付きでUnity MenuItemsを取得します。Unity Searchのメニュープロバイダーとは異なり、自動化とデバッグに必要な実装詳細（メソッド名、アセンブリ、実行互換性）を提供します
- **パラメータ**: 
  - `FilterText` (string): MenuItemパスをフィルタするテキスト（すべてのアイテムの場合は空）（デフォルト: ""）
  - `FilterType` (enum): 適用するフィルタのタイプ - "contains", "exact", "startswith"（デフォルト: "contains"）
  - `IncludeValidation` (boolean): 結果に検証関数を含める（デフォルト: false）
  - `MaxCount` (number): 取得するメニューアイテムの最大数（デフォルト: 200）
- **レスポンス**: 
  - `MenuItems` (array): フィルタ条件に一致する発見されたMenuItemsのリスト
    - `Path` (string): MenuItemパス
    - `MethodName` (string): 実行メソッド名
    - `TypeName` (string): 実装クラス名
    - `AssemblyName` (string): アセンブリ名
    - `Priority` (number): メニューアイテムの優先度
    - `IsValidateFunction` (boolean): 検証関数かどうか
  - `TotalCount` (number): フィルタリング前に発見されたMenuItemsの総数
  - `FilteredCount` (number): フィルタリング後に返されたMenuItemsの数
  - `AppliedFilter` (string): 適用されたフィルタテキスト
  - `AppliedFilterType` (string): 適用されたフィルタタイプ

### 10. execute-menu-item
- **説明**: パスによってUnity MenuItemを実行します
- **パラメータ**: 
  - `MenuItemPath` (string): 実行するメニューアイテムパス（例: "GameObject/Create Empty"）（デフォルト: ""）
  - `UseReflectionFallback` (boolean): EditorApplication.ExecuteMenuItemが失敗した場合にリフレクションをフォールバックとして使用するかどうか（デフォルト: true）
- **レスポンス**: 
  - `MenuItemPath` (string): 実行されたメニューアイテムパス
  - `Success` (boolean): 実行が成功したかどうか
  - `ExecutionMethod` (string): 使用された実行方法（EditorApplicationまたはReflection）
  - `ErrorMessage` (string): 実行が失敗した場合のエラーメッセージ
  - `Details` (string): 実行に関する追加情報
  - `MenuItemFound` (boolean): メニューアイテムがシステムで見つかったかどうか

---

## 関連ドキュメント

- [メインREADME](README_ja.md) - プロジェクト概要とセットアップ
- [アーキテクチャドキュメント](Editor/ARCHITECTURE.md) - 技術アーキテクチャの詳細
- [TypeScriptサーバーアーキテクチャ](TypeScriptServer~/ARCHITECTURE.md) - TypeScriptサーバー実装
- [変更履歴](CHANGELOG.md) - バージョン履歴と更新
</details>

> [!NOTE]
> これらのツールを組み合わせることで、AIが人間の介入なしに複雑なタスクを完了できます。
> 特にエラー修正、テスト実行などの反復的なタスクで威力を発揮します。

## セキュリティ設定

> [!IMPORTANT]
> **デフォルトで無効化されている機能**
>
> 任意のコードを自由に実行できてしまうため、以下の機能はデフォルトで無効化されています：
> - `execute-menu-item`: メニュー項目の実行
> - `run-tests`: テストの実行
>
> これらの機能を使用するには、uLoopMCPウィンドウのSecurity Settingsで該当する設定を有効にする必要があります：
> - **Allow Test Execution**: `run-tests`ツールを有効にします
> - **Allow Menu Item Execution**: `execute-menu-item`ツールを有効にします
    > これらの機能を有効にする場合は、信頼できる環境でのみ使用してください。

## 使用方法
1. Window > uLoopMCPを選択します。専用ウィンドウが開くので、「Start Server」ボタンを押してください。  
<img width="335" alt="image" src="https://github.com/user-attachments/assets/4cfd7f26-7739-442d-bad9-b3f6d113a0d7" />

3. 次に、LLM Tool SettingsセクションでターゲットIDEを選択します。黄色い「Configure {LLM Tool名}」ボタンを押してIDEに自動接続してください。  
<img width="335" alt="image" src="https://github.com/user-attachments/assets/25f1f4f9-e3c8-40a5-a2f3-903f9ed5f45b" />

4. IDE接続確認
  - 例えばCursorの場合、設定ページのTools & Integrationsを確認し、uLoopMCPを見つけてください。トグルをクリックしてMCPを有効にします。赤い円が表示される場合は、Cursorを再起動してください。  
<img width="545" alt="image" src="https://github.com/user-attachments/assets/ed54d051-b78a-4bb4-bb2f-7ab23ebc1840" />


4. 手動設定（通常は不要）
> [!NOTE]
> 通常は自動設定で十分ですが、必要に応じて、Cursorの設定ファイル（`.cursor/mcp.json`）を手動で編集できます：

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

5. 複数のUnityインスタンスのサポート
> [!NOTE]
> ポート番号を変更することで複数のUnityインスタンスをサポートできます。uLoopMCP起動時に自動的に使われいないportが割り当てられます。

## 前提条件

> [!WARNING]
> 以下のソフトウェアが必須です：
> - **Unity 2022.3以上**
> - **Node.js 18.0以上** - MCPサーバー実行に必要
> - Node.jsを[こちら](https://nodejs.org/en/download)からインストールしてください

## インストール

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

3. Package Managerウィンドウを開き、My RegistriesセクションのOpenUPMを選択。uLoopMCPが表示されます。

## カスタムツール開発
コアパッケージを変更することなく、プロジェクト独自のツールを簡単に追加できます。

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

## Cursorでmcpの実行を自動で行う
CursorはデフォルトでMCP実行時にユーザーの許可を必要とします。
これを無効にするには、Cursor Settings > Chat > MCP Tools ProtectionをOffにします。
MCPの種類・ツール事に制御できず、全てのMCPが許可不要になってしまうため、セキュリティとのトレードオフになります。そこを留意して設定してください。

## WindowsでClaude Codeを使う際、WSL2の対応
WSL2のミラーモードを有効化します。`C:/Users/[username]/.wslconfig` に、下記を記述します。
```
[wsl2]
networkingMode=mirrored
```
その後、下記コマンドを実行して設定を反映させます。
```bash
wsl --shutdown
wsl
```

## ライセンス
MIT License
