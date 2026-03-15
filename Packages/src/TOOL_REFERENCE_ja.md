[English](TOOL_REFERENCE.md)

# uLoopMCP ツールリファレンス

このドキュメントでは、全uLoopMCPツールの詳細仕様を提供します。

## 共通レスポンス形式

すべてのUnity MCPツールは以下の共通要素を持ちます：

### 共通レスポンスプロパティ
すべてのツールには以下のプロパティが自動的に含まれます：
- `Ver` (string): CLIとの互換性チェック用のuLoopMCPサーバーバージョン

---

## Unity コアツール

### 1. compile
- **説明**: AssetDatabase.Refresh()を実行後、コンパイルを行います。詳細なタイミング情報付きでコンパイル結果を返します。
- **パラメータ**:
  - `ForceRecompile` (boolean): 強制再コンパイルを実行するかどうか（デフォルト: false）
  - `WaitForDomainReload` (boolean): Domain Reload完了まで待機するかどうか（デフォルト: false）
- **レスポンス**:
  - `Success` (boolean | null): コンパイルが成功したかどうか。ForceRecompile=true時はDomain Reload完了まで結果が取得できないためnull
  - `ErrorCount` (number | null): エラーの総数。ForceRecompile=true時はnull
  - `WarningCount` (number | null): 警告の総数。ForceRecompile=true時はnull
  - `Errors` (array | null): コンパイルエラーの配列。ForceRecompile=true時はnull
    - `Message` (string): エラーメッセージ
    - `File` (string): エラーが発生したファイルパス
    - `Line` (number): エラーが発生した行番号
  - `Warnings` (array | null): コンパイル警告の配列。ForceRecompile=true時はnull
    - `Message` (string): 警告メッセージ
    - `File` (string): 警告が発生したファイルパス
    - `Line` (number): 警告が発生した行番号
  - `Message` (string): 追加情報のためのオプションメッセージ
  - `ProjectRoot` (string): Unityプロジェクトのルートパス。WaitForDomainReload=true時のみセットされる

### 2. get-logs
- **説明**: フィルタリングおよび高度な検索機能付きでUnityコンソールからログ情報を取得します
- **パラメータ**:
  - `LogType` (enum): フィルタするログタイプ - "Error", "Warning", "Log", "All"（デフォルト: "All"）
  - `MaxCount` (number): 取得するログの最大数（デフォルト: 100）
  - `SearchText` (string): ログメッセージ内で検索するテキスト（空の場合はすべて取得）（デフォルト: ""）
  - `UseRegex` (boolean): 検索に正規表現を使用するかどうか（デフォルト: false）
  - `SearchInStackTrace` (boolean): スタックトレース内も検索対象に含めるかどうか（デフォルト: false）
  - `IncludeStackTrace` (boolean): スタックトレースを表示するかどうか（デフォルト: false）
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

### 3. run-tests
- **説明**: Unity Test Runnerを実行し、包括的なレポート付きでテスト結果を取得します
- **パラメータ**:
  - `FilterType` (enum): テストフィルタのタイプ - "all"(0), "exact"(1), "regex"(2), "assembly"(3)（デフォルト: "all"）
  - `FilterValue` (string): フィルタ値（FilterTypeがall以外の場合に指定）（デフォルト: ""）
    - `exact`: 個別テストメソッド名（完全一致）（例：io.github.hatayama.uLoopMCP.ConsoleLogRetrieverTests.GetAllLogs_WithMaskAllOff_StillReturnsAllLogs）
    - `regex`: クラス名または名前空間（正規表現パターン）（例：io.github.hatayama.uLoopMCP.ConsoleLogRetrieverTests, io.github.hatayama.uLoopMCP）
    - `assembly`: アセンブリ名（例：uLoopMCP.Tests.Editor）
  - `TestMode` (enum): テストモード - "EditMode"(0), "PlayMode"(1)（デフォルト: "EditMode"）
    - **PlayMode注意**: PlayModeテスト実行時は、一時的にdomain reloadが無効化されます
- **レスポンス**:
  - `Success` (boolean): テスト実行が成功したかどうか
  - `Message` (string): テスト実行メッセージ
  - `CompletedAt` (string): テスト実行完了タイムスタンプ（ISO形式）
  - `TestCount` (number): 実行されたテストの総数
  - `PassedCount` (number): 合格したテストの数
  - `FailedCount` (number): 失敗したテストの数
  - `SkippedCount` (number): スキップされたテストの数
  - `XmlPath` (string): NUnit XML結果ファイルのパス（テスト失敗時に自動保存）。XMLファイルは `{project_root}/.uloop/outputs/TestResults/` フォルダに保存されます

### 4. clear-console
- **説明**: クリーンな開発ワークフローのためにUnityコンソールログをクリアします
- **パラメータ**:
  - `AddConfirmationMessage` (boolean): クリア後に確認ログメッセージを追加するかどうか（デフォルト: false）
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
  - `SearchMode` (enum): 検索モード - "Exact", "Path", "Regex", "Contains", "Selected"（デフォルト: "Exact"）
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
      - `type` (string): コンポーネントタイプ名
      - `fullTypeName` (string): 完全なアセンブリ修飾タイプ名
      - `properties` (array): コンポーネントプロパティ（IncludeInheritedPropertiesがtrueの場合）
  - `totalFound` (number): 見つかったGameObjectの総数
  - `errorMessage` (string): 検索が失敗した場合のエラーメッセージ
  - `resultsFilePath` (string): 結果がファイルにエクスポートされた場合のファイルパス
  - `message` (string): 操作メッセージ
  - `processingErrors` (array): シリアライズに失敗したオブジェクトの配列
    - `gameObjectName` (string): 失敗したGameObjectの名前
    - `gameObjectPath` (string): 失敗したGameObjectのパス
    - `error` (string): エラーの説明

---

## Unity 検索・発見ツール

### 6. unity-search
- **説明**: Unity Search APIを使用してUnityプロジェクトを検索し、包括的なフィルタリングとエクスポートオプションを提供します
- **パラメータ**:
  - `SearchQuery` (string): 検索クエリ文字列（Unity Search構文をサポート）（デフォルト: ""）
    - 例: "*.cs", "t:Texture2D", "ref:MyScript", "p:MyPackage"
    - Unity Search詳細ドキュメント: https://docs.unity3d.com/6000.1/Documentation/Manual/search-expressions.html および https://docs.unity3d.com/6000.0/Documentation/Manual/search-query-operators.html 。よく使うクエリ: "*.cs"（全C#ファイル）, "t:Texture2D"（Texture2Dアセット）, "ref:MyScript"（MyScriptを参照するアセット）, "p:MyPackage"（パッケージ内検索）, "t:MonoScript *.cs"（C#スクリプトのみ）, "Assets/Scripts/*.cs"（特定フォルダのC#ファイル）。日本語ガイド: https://light11.hatenadiary.com/entry/2022/12/12/193119
  - `Providers` (array): 使用する特定の検索プロバイダー（空 = すべてのアクティブプロバイダー）（デフォルト: []）
    - 一般的なプロバイダー: "asset", "scene", "menu", "settings", "packages"
  - `MaxResults` (number): 返す検索結果の最大数（デフォルト: 50）
  - `IncludeDescription` (boolean): 結果に詳細な説明を含めるかどうか（デフォルト: true）
  - `IncludeMetadata` (boolean): ファイルメタデータ（サイズ、更新日）を含めるかどうか（デフォルト: false）
  - `SearchFlags` (enum): Unity Search動作を制御する検索フラグ（デフォルト: "Default"(0), "Synchronous"(1), "WantsMore"(2), "Packages"(4), "Sorted"(8)）
  - `SaveToFile` (boolean): 大量の結果セットを扱う際のトークン消費を抑えるため、検索結果を外部ファイルに保存するかどうか。結果はJSON/CSVファイルとして保存されます（デフォルト: false）
  - `OutputFormat` (enum): SaveToFileが有効な場合の出力ファイル形式（デフォルト: "JSON"(0), "CSV"(1), "TSV"(2)）
  - `AutoSaveThreshold` (number): 自動ファイル保存の閾値（結果数がこの値を超えると自動的にファイルに保存）。0に設定すると自動保存を無効化（デフォルト: 100）
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
  - `AppliedFilters` (object): 適用されたフィルタ情報
    - `FileExtensions` (array): フィルタされたファイル拡張子
    - `AssetTypes` (array): フィルタされたアセットタイプ
    - `PathFilter` (string): 適用されたパスフィルタパターン
    - `FilteredOutCount` (number): フィルタで除外された結果数

### 7. get-hierarchy
- **説明**: Unity階層構造をネストされたJSON形式で取得します
- **パラメータ**:
  - `IncludeInactive` (boolean): 階層結果に非アクティブなGameObjectを含めるかどうか（デフォルト: true）
  - `MaxDepth` (number): 階層を探索する最大深度（無制限深度の場合は-1）（デフォルト: -1）
  - `RootPath` (string): 階層探索を開始するルートGameObjectパス（すべてのルートオブジェクトの場合は空/null）（デフォルト: null）
  - `IncludeComponents` (boolean): 階層内の各GameObjectのコンポーネント情報を含めるかどうか（デフォルト: true）
  - `IncludePaths` (boolean): ノードのパス情報を含めるかどうか（デフォルト: false）
  - `UseComponentsLut` (string): コンポーネント用LUTの使用 - "auto", "true", "false"（デフォルト: "auto"）
  - `UseSelection` (boolean): 現在選択中のGameObjectをルートとして使用するかどうか。trueの場合、RootPathは無視されます（デフォルト: false）
- **レスポンス**:
  - `message` (string): クライアントがJSONファイルを参照するための案内メッセージ
  - `hierarchyFilePath` (string): 階層データが保存されたファイルパス（例: "{project_root}/.uloop/outputs/HierarchyResults/hierarchy_2025-07-10_21-30-15.json"）。エクスポートされたJSONファイルには `hierarchy`（GameObjectのネスト配列）と `context`（シーン情報、ノード数、最大深度）が含まれる

### 8. get-menu-items
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
    - `CanExecuteViaEditorApplication` (boolean): EditorApplication.ExecuteMenuItemで実行可能かどうか
    - `WarningMessage` (string): MenuItemに問題がある場合の警告メッセージ（例: 属性の重複）
  - `TotalCount` (number): フィルタリング前に発見されたMenuItemsの総数
  - `FilteredCount` (number): フィルタリング後に返されたMenuItemsの数
  - `AppliedFilter` (string): 適用されたフィルタテキスト
  - `AppliedFilterType` (string): 適用されたフィルタタイプ

### 9. execute-menu-item
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
  - `WarningMessage` (string): MenuItemに問題がある場合の警告メッセージ（例: 属性の重複）

### 10. execute-dynamic-code
- **説明**: Unity Editor内で動的C#コードを実行します。セキュリティレベルに応じてAPI利用を制限し、using文の自動処理やエラーメッセージの改善機能を提供します
- **パラメータ**:
  - `Code` (string): 実行するC#コード（デフォルト: ""）
  - `Parameters` (Dictionary<string, object>): 実行時パラメータ（デフォルト: {}）
  - `CompileOnly` (boolean): コンパイルのみ実行（実行はしない）（デフォルト: false）
- **レスポンス**:
  - `Success` (boolean): 実行が成功したかどうか
  - `Result` (string): 実行結果
  - `Logs` (array): ログメッセージの配列
  - `CompilationErrors` (array): コンパイルエラーの配列（存在する場合）
    - `Message` (string): エラーメッセージ
    - `Line` (number): エラーが発生した行番号
    - `Column` (number): エラーが発生した列番号
    - `ErrorCode` (string): コンパイラーエラーコード（CS0103など）
    - `Hint` (string): エラー解決のためのオプションヒント
    - `Suggestions` (array of string): 修正候補（例: usingの追加や完全修飾名の使用）
    - `Context` (string): エラー周辺のコード行とキャレットポインタ
    - `PointerColumn` (number): キャレット描画用のポインタカラム（1-based）
  - `ErrorMessage` (string): エラーメッセージ（失敗時）
  - `SecurityLevel` (string): 現在のセキュリティレベル（"Disabled", "Restricted", "FullAccess"）
  - `UpdatedCode` (string): 更新されたコード（修正適用後）
  - `DiagnosticsSummary` (string): 診断情報のサマリー（ユニークエラー数、総数、最初のエラー概要）
  - `Diagnostics` (array): リッチクライアント向けの構造化された診断情報（CompilationErrorsと同じ構造）

### 11. focus-window
- **説明**: macOSおよびWindowsでUnity Editorウィンドウを前面に表示します
- **パラメータ**: なし
- **レスポンス**:
  - `Success` (boolean): 操作が成功したかどうか
  - `Message` (string): 操作結果メッセージ
  - `ErrorMessage` (string): 操作が失敗した場合のエラーメッセージ

### 12. screenshot
- **説明**: Unity EditorWindowのスクリーンショットを撮影してPNG画像として保存します。名前による柔軟なマッチングモードで任意のEditorWindowをキャプチャ可能です
- **パラメータ**:
  - `WindowName` (string): キャプチャするウィンドウ名（例: "Game", "Scene", "Console", "Inspector", "Project", "Hierarchy"）（デフォルト: "Game"）
  - `ResolutionScale` (number): キャプチャ画像の解像度スケール、0.1〜1.0（デフォルト: 1）
  - `MatchMode` (enum): ウィンドウ名のマッチングモード（すべて大文字小文字を区別しない） - "exact", "prefix", "contains"（デフォルト: "exact"）
    - `exact`: ウィンドウ名が完全に一致する必要があります
    - `prefix`: ウィンドウ名が入力で始まる必要があります
    - `contains`: ウィンドウ名に入力が含まれている必要があります
  - `OutputDirectory` (string): スクリーンショット保存先のディレクトリパス。空の場合はデフォルトパス（.uloop/outputs/Screenshots/）を使用。絶対パスも指定可能（デフォルト: ""）
- **レスポンス**:
  - `ScreenshotCount` (number): キャプチャされたウィンドウの数
  - `Screenshots` (array): スクリーンショット情報の配列
    - `ImagePath` (string): 保存されたPNGファイルの絶対パス
    - `FileSizeBytes` (number): 保存されたファイルのサイズ（バイト）
    - `Width` (number): キャプチャ画像の幅（ピクセル）
    - `Height` (number): キャプチャ画像の高さ（ピクセル）

### 13. control-play-mode
- **説明**: Unity Editorのプレイモードを制御します（再生/停止/一時停止）
- **パラメータ**:
  - `Action` (enum): 実行するアクション - "Play", "Stop", "Pause"（デフォルト: "Play"）
    - `Play`: プレイモードを開始（一時停止からの再開も含む）
    - `Stop`: プレイモードを終了し、エディットモードに戻る
    - `Pause`: プレイモードのまま一時停止する
- **レスポンス**:
  - `IsPlaying` (boolean): Unityが現在プレイモードかどうか
  - `IsPaused` (boolean): プレイモードが一時停止中かどうか
  - `Message` (string): 実行されたアクションの説明

### 14. simulate-mouse
- **説明**: PlayMode中のUI要素に対してマウスクリック・長押し・ドラッグをシミュレーション。EventSystemとExecuteEventsを使ってポインタイベントを直接ディスパッチするため、旧・新Input Systemの両方に依存せず動作
- **パラメータ**:
  - `Action` (enum): マウスアクション - "Click", "Drag", "DragStart", "DragMove", "DragEnd", "LongPress"（デフォルト: "Click"）
    - `Click`: (X, Y)で左クリック。PointerDown → PointerUp → PointerClick を発火
    - `LongPress`: (X, Y)でDuration秒間押し続けてからリリース。PointerClickは発火しない
    - `Drag`: (FromX, FromY)から(X, Y)へのワンショットドラッグ
    - `DragStart`: (X, Y)でドラッグを開始しホールド
    - `DragMove`: 現在位置から(X, Y)へ指定速度でアニメーション移動
    - `DragEnd`: (X, Y)へ移動後、ドラッグをリリース
  - `X` (number): ターゲットX座標（スクリーンピクセル、原点: 左上）（デフォルト: 0）
  - `Y` (number): ターゲットY座標（スクリーンピクセル、原点: 左上）（デフォルト: 0）
  - `FromX` (number): Dragアクションの開始X座標（デフォルト: 0）
  - `FromY` (number): Dragアクションの開始Y座標（デフォルト: 0）
  - `DragSpeed` (number): ドラッグ速度（ピクセル/秒）、0で即時移動（デフォルト: 2000）
  - `Duration` (number): LongPressアクションのホールド秒数（デフォルト: 0.5）
- **レスポンス**:
  - `Success` (boolean): アクションが正常に完了したかどうか
  - `Message` (string): 実行されたアクションの説明
  - `Action` (string): 実行されたアクション
  - `HitGameObjectName` (string): ヒットしたUI要素の名前（ヒットなしの場合null）
  - `PositionX` (number): 使用されたX座標
  - `PositionY` (number): 使用されたY座標
  - `EndPositionX` (number): 終了X座標（Dragアクション用）
  - `EndPositionY` (number): 終了Y座標（Dragアクション用）

### 15. get-unity-search-providers

Unity Search プロバイダーの詳細情報を取得します。

**パラメータ:**

| パラメータ | 型 | デフォルト | 説明 |
|-----------|------|---------|-------------|
| `ProviderId` | string | `""` | 特定のプロバイダーID（空 = 全プロバイダー）。例: `asset`, `scene`, `menu`, `settings` |
| `ActiveOnly` | boolean | `false` | アクティブなプロバイダーのみ返す |
| `SortByPriority` | boolean | `true` | 優先度でソート（低い値 = 高優先度） |
| `IncludeDescriptions` | boolean | `true` | 詳細な説明を含める |

**レスポンス:**

- `Providers` (array): プロバイダー詳細
  - `Id` (string): プロバイダー識別子
  - `DisplayName` (string): 表示名
  - `Description` (string): プロバイダーの説明
  - `IsActive` (boolean): プロバイダーがアクティブかどうか
  - `Priority` (integer): プロバイダーの優先度
- `TotalCount` (integer): 返されたプロバイダーの総数
- `ActiveCount` (integer): アクティブなプロバイダーの数
- `InactiveCount` (integer): 非アクティブなプロバイダーの数
- `Success` (boolean): リクエストが成功したかどうか
- `ErrorMessage` (string): リクエスト失敗時のエラーメッセージ
- `AppliedFilter` (string): 適用されたフィルター
- `SortedByPriority` (boolean): 優先度でソートされているか

---

## 関連ドキュメント

- [メインREADME](README_ja.md) - プロジェクト概要とセットアップ
- [アーキテクチャドキュメント](ARCHITECTURE_ja.md) - 技術アーキテクチャの詳細
- [変更履歴](CHANGELOG.md) - バージョン履歴と更新
