# Compile WaitForDomainReload シーケンス図

## lockファイルの生成・削除タイミング

| lockファイル | 生成タイミング | 削除タイミング |
|-------------|--------------|--------------|
| `compiling.lock` | `CompilationPipeline.compilationStarted` イベント | `CompilationPipeline.compilationFinished` イベント |
| `domainreload.lock` | `AssemblyReloadEvents.beforeAssemblyReload` イベント | McpBridgeServer のサーバー起動完了時 |
| `serverstarting.lock` | （サーバー起動開始時） | （サーバー起動完了時） |

## Case 1: ForceRecompile=true, WaitForDomainReload=true (正常動作)

```mermaid
sequenceDiagram
    participant CLI as CLI/MCP
    participant TS as TypeScript Server
    participant Unity as Unity C# (TCP Server)
    participant FS as ファイルシステム

    CLI->>TS: compile(ForceRecompile=true, WaitForDomainReload=true, RequestId=xxx)
    TS->>Unity: executeTool("compile", args)

    Note over Unity: CompileController.TryCompileAsync()
    Note over Unity: AssetDatabase.Refresh()
    Note over Unity: RequestScriptCompilation(CleanBuildCache)
    Note over Unity: WatchCompileStartAsync() 開始（fire-and-forget）

    Note over Unity: compilationStarted イベント発火
    Unity->>FS: 🔒 compiling.lock 作成（CompilationLockService）

    Note over Unity: HandleCompileFinished() コールバック
    Note over Unity: ForceRecompile=true → IsIndeterminate=true, Success=null

    Unity->>FS: 🔒 compiling.lock 削除（CompilationLockService）
    Unity->>FS: 結果ファイル書き込み（PersistResponseIfNeeded）
    Unity->>TS: TCPレスポンス: {Success: null}

    Note over TS: waitForCompileCompletion() 開始

    Note over Unity: beforeAssemblyReload イベント発火
    Unity->>FS: 🔒 domainreload.lock 作成（DomainReloadDetectionService）
    Note over TS: ポーリング: 結果ファイル=あり, lock=あり → 待機継続

    Note over Unity: Domain Reload 開始（TCPサーバー停止）
    Note over Unity: Domain Reload 完了
    Note over Unity: TCPサーバー再起動
    Unity->>FS: 🔓 domainreload.lock 削除（McpBridgeServer）

    Note over TS: ポーリング: 結果ファイル=あり, lock=なし → TCP接続チェック
    TS->>Unity: TCP接続テスト
    Unity-->>TS: 接続成功！
    Note over TS: 完了！

    TS->>CLI: レスポンス: {Success: null}
```

## Case 2: ForceRecompile=false, C#変更あり, WaitForDomainReload=true (バグ)

```mermaid
sequenceDiagram
    participant CLI as CLI/MCP
    participant TS as TypeScript Server
    participant Unity as Unity C# (TCP Server)
    participant FS as ファイルシステム

    CLI->>TS: compile(ForceRecompile=false, WaitForDomainReload=true, RequestId=xxx)
    TS->>Unity: executeTool("compile", args)

    Note over Unity: CompileController.TryCompileAsync()
    Note over Unity: AssetDatabase.Refresh()
    Note over Unity: RequestScriptCompilation()
    Note over Unity: WatchCompileStartAsync() 開始（fire-and-forget）

    Note over Unity: compilationStarted イベント発火
    Unity->>FS: 🔒 compiling.lock 作成（CompilationLockService）

    Note over Unity: コンパイル実行中...

    Note over Unity: HandleCompileFinished() コールバック
    Note over Unity: ForceRecompile=false → Success=true, エラー/警告集計

    Unity->>FS: 🔓 compiling.lock 削除（CompilationLockService）
    Unity->>FS: 結果ファイル書き込み（PersistResponseIfNeeded）
    Unity->>TS: TCPレスポンス: {Success: true, ErrorCount: 0, WarningCount: 271}

    Note over TS: waitForCompileCompletion() 開始

    rect rgb(255, 200, 200)
        Note over TS: ポーリング: 結果ファイル=あり, lock=なし → 完了判定！⚠️ バグ！
        Note over TS: Domain Reloadを待たずに即返却してしまう
        TS->>CLI: レスポンス: {Success: true}
    end

    Note over Unity: ⏰ ここでやっと beforeAssemblyReload イベント発火
    Unity->>FS: 🔒 domainreload.lock 作成（DomainReloadDetectionService）
    Note over Unity: Domain Reload 開始（TCPサーバー停止）
    Note over Unity: Domain Reload中...
    Note over Unity: TCPサーバー再起動
    Unity->>FS: 🔓 domainreload.lock 削除（McpBridgeServer）
```

## Case 3: ForceRecompile=false, C#変更なし, WaitForDomainReload=true (正常動作)

```mermaid
sequenceDiagram
    participant CLI as CLI/MCP
    participant TS as TypeScript Server
    participant Unity as Unity C# (TCP Server)
    participant FS as ファイルシステム

    CLI->>TS: compile(ForceRecompile=false, WaitForDomainReload=true, RequestId=xxx)
    TS->>Unity: executeTool("compile", args)

    Note over Unity: CompileController.TryCompileAsync()
    Note over Unity: AssetDatabase.Refresh()
    Note over Unity: RequestScriptCompilation()
    Note over Unity: WatchCompileStartAsync() 開始（fire-and-forget）
    Note over Unity: 変更なし → compilationStarted 発火せず
    Note over Unity: WatchCompileStartAsync() タイムアウト → AbortCompile()
    Note over Unity: IsIndeterminate=true, Success=false

    Unity->>FS: 結果ファイル書き込み（PersistResponseIfNeeded）
    Unity->>TS: TCPレスポンス: {Success: true, ErrorCount: 0, ...}

    Note over TS: waitForCompileCompletion() 開始
    Note over TS: ポーリング: 結果ファイル=あり, lock=なし → 完了判定！
    Note over TS: （正しい！Domain Reloadは発生しない）

    TS->>CLI: レスポンス: {Success: true}
```

## 根本原因

Case 2 で `compilationFinished` と `beforeAssemblyReload` の間にギャップがある。

1. `compilationFinished` → `compiling.lock` 削除 + 結果ファイル書き込み + TCPレスポンス送信
2. （ここでギャップ！lockファイルが一つも存在しない瞬間がある）
3. `beforeAssemblyReload` → `domainreload.lock` 作成

`waitForCompileCompletion()` がこのギャップ中にポーリングすると「結果あり + lockなし = 完了」と誤判定する。

## タイミングの違いまとめ

| ケース | `compilationFinished` 時の挙動 | `beforeAssemblyReload` のタイミング | 問題 |
|--------|-------------------------------|-----------------------------------|------|
| ForceRecompile=true | 即座にTCPレスポンス返却 | コンパイル中〜直後（TCPレスポンス前に発生しうる） | lock出現が早い → 待機OK |
| ForceRecompile=false（変更あり） | コンパイル完了後にTCPレスポンス返却 | TCPレスポンス返却**後** | lockが出現する前に完了判定 → バグ |
| ForceRecompile=false（変更なし） | 即座にTCPレスポンス返却 | 発生しない | Domain Reloadなし → 正常 |
