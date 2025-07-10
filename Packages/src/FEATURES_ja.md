[English](FEATURES.md)

# uMCP 機能仕様

このドキュメントでは、Unity MCP（Model Context Protocol）の全ツールと機能について詳細に説明します。

## 共通パラメータ・レスポンス形式

すべてのUnity MCPツールは以下の共通要素を持ちます：

### 共通パラメータ
- `TimeoutSeconds` (number): ツール実行のタイムアウト時間（秒）（デフォルト: 10秒）

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
    - `exact`: 個別テストメソッド名（完全一致）（例：io.github.hatayama.uMCP.ConsoleLogRetrieverTests.GetAllLogs_WithMaskAllOff_StillReturnsAllLogs）
    - `regex`: クラス名または名前空間（正規表現パターン）（例：io.github.hatayama.uMCP.ConsoleLogRetrieverTests, io.github.hatayama.uMCP）
    - `assembly`: アセンブリ名（例：uMCP.Tests.Editor）
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