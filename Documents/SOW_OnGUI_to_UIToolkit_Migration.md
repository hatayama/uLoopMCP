# SOW: OnGUI to UI Toolkit Migration for McpEditorWindow

## 概要
Unity EditorのMcpEditorWindowをOnGUIベースからUI Toolkitへ移行する。UI ToolkitのUXML/USSを使用し、MVPアーキテクチャを維持しながらモダンなUIシステムへ移行する。

## 現状分析

### 現在の実装
- **UIシステム**: OnGUI (IMGUI)
- **アーキテクチャ**: MVPパターン
  - Model: `McpEditorModel`
  - View: `McpEditorWindowView` (OnGUIベース)
  - Presenter: `McpEditorWindow`
- **主要UIコンポーネント**:
  - Server Status表示
  - Server Controls (Start/Stop, Port設定, Auto Start)
  - Connected Tools Section (Foldout)
  - Editor Config Section (LLM Tool Settings)
  - Security Settings Section

### 現在の問題点
- OnGUIは毎フレーム再描画されるため非効率
- スタイリングの柔軟性が低い
- レイアウト管理が煩雑
- 再利用可能なコンポーネントの作成が困難

## 移行計画

### Phase 1: 基盤準備
1. UI Toolkit用のファイル構造を作成
2. UXML/USSファイルの初期設定
3. 新しいViewクラスの作成

### Phase 2: 段階的移行（マーチン・ファウラーのリファクタリング手法）
1. **Parallel Change Pattern** を使用
   - 新旧のUIシステムを並行して動作させる
   - フラグで切り替え可能にする
2. **Extract Method** で各UI部分を独立した描画メソッドに分離
3. **Replace Method Body** で段階的にUI Toolkitへ置き換え

### Phase 3: 完全移行
1. OnGUIコードの削除
2. パフォーマンステストとバグ修正
3. 古いコードのクリーンアップ

## 技術詳細

### バックグラウンド更新の考慮
UI ToolkitはOnGUIと異なり、Unityがバックグラウンドでも表示更新が可能。以下の対策を実装：

1. **定期的な更新タイマー**
   - `IVisualElementScheduler`を使用して定期更新
   - サーバー状態の監視と自動更新

2. **イベント駆動更新**
   - サーバー状態変更イベントの購読
   - クライアント接続/切断イベントの監視

3. **フォーカス状態の管理**
   - `EditorApplication.focusChanged`でフォーカス状態を監視
   - バックグラウンド時は更新頻度を調整（パフォーマンス最適化）

### ファイル構造
```
Packages/src/Editor/UI/
├── McpEditorWindow.cs (変更)
├── McpEditorWindowView.cs (削除予定)
├── UIToolkit/
│   ├── McpEditorWindow.uxml
│   ├── McpEditorWindow.uss
│   ├── Views/
│   │   ├── McpEditorWindowUITView.cs (新規)
│   │   ├── ServerStatusView.cs
│   │   ├── ServerControlsView.cs
│   │   ├── ConnectedToolsView.cs
│   │   ├── EditorConfigView.cs
│   │   └── SecuritySettingsView.cs
│   └── Components/
│       ├── McpButton.cs
│       ├── McpFoldout.cs
│       └── McpClientItem.cs
```

### UXML構造
```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
    <ui:Style src="McpEditorWindow.uss" />
    <ui:VisualElement name="mcp-editor-window" class="mcp-editor-window">
        <ui:ScrollView name="main-scroll-view" class="mcp-editor-window__scroll-view">
            <!-- Server Status -->
            <ui:VisualElement name="server-status" class="mcp-section mcp-server-status">
                <!-- Content injected by C# -->
            </ui:VisualElement>
            
            <!-- Server Controls -->
            <ui:VisualElement name="server-controls" class="mcp-section mcp-server-controls">
                <!-- Content injected by C# -->
            </ui:VisualElement>
            
            <!-- Connected Tools -->
            <ui:Foldout name="connected-tools" class="mcp-section mcp-connected-tools">
                <!-- Content injected by C# -->
            </ui:Foldout>
            
            <!-- Editor Config -->
            <ui:Foldout name="editor-config" class="mcp-section mcp-editor-config">
                <!-- Content injected by C# -->
            </ui:Foldout>
            
            <!-- Security Settings -->
            <ui:Foldout name="security-settings" class="mcp-section mcp-security-settings">
                <!-- Content injected by C# -->
            </ui:Foldout>
        </ui:ScrollView>
    </ui:VisualElement>
</ui:UXML>
```

### USS (BEM記法)
```css
/* Unity標準のダークテーマ色を維持 */
:root {
    --mcp-background-dark: #383838;
    --mcp-section-background: #393939;
    --mcp-button-green: #5A9758;
    --mcp-button-green-hover: #6BA769;
    --mcp-text-green: #4CAF50;
    --mcp-text-red: #F44336;
    --mcp-text-secondary: #888888;
    --mcp-warning-background: #5A5037;
    --mcp-info-background: #3B4A5A;
}

/* Block */
.mcp-editor-window {
    flex-grow: 1;
    background-color: var(--mcp-background-dark);
    padding: 4px;
}

/* Element */
.mcp-editor-window__scroll-view {
    flex-grow: 1;
}

/* Section base - Unity標準のhelpBoxスタイルを再現 */
.mcp-section {
    margin: 4px 4px 8px 4px;
    padding: 8px;
    background-color: var(--mcp-section-background);
    border-radius: 3px;
    border-width: 1px;
    border-color: #232323;
}

/* Server Status Block */
.mcp-server-status {
    flex-direction: row;
    align-items: center;
}

.mcp-server-status__label {
    font-weight: bold;
    min-width: 50px;
}

.mcp-server-status__value {
    margin-left: 8px;
}

.mcp-server-status__value--running {
    color: #4CAF50;
}

.mcp-server-status__value--stopped {
    color: #F44336;
}

/* Server Controls Block */
.mcp-server-controls__port-row {
    flex-direction: row;
    align-items: center;
    margin-bottom: 8px;
}

.mcp-server-controls__port-label {
    min-width: 30px;
    -unity-text-align: middle-left;
}

.mcp-server-controls__port-field {
    width: 50px;
    -unity-text-align: middle-right;
    margin-left: 4px;
}

.mcp-server-controls__toggle-button {
    height: 25px;
    font-size: 12px;
    margin: 8px 0;
    -unity-font-style: bold;
}

.mcp-server-controls__toggle-button--start {
    background-color: var(--mcp-button-green);
}

.mcp-server-controls__toggle-button--start:hover {
    background-color: var(--mcp-button-green-hover);
}

.mcp-server-controls__toggle-button--stop {
    background-color: #994444;
}

.mcp-server-controls__toggle-button--stop:hover {
    background-color: #AA5555;
}

/* Auto Start Checkbox */
.mcp-server-controls__auto-start-row {
    flex-direction: row;
    align-items: center;
}

.mcp-server-controls__auto-start-toggle {
    margin-right: 4px;
}

.mcp-server-controls__auto-start-label {
    flex-grow: 1;
    -unity-text-align: middle-left;
}

.mcp-server-controls__auto-start-label:hover {
    color: #CCCCCC;
}

/* Connected Tools Block */
.mcp-connected-tools__client-item {
    margin: 2px 0;
    padding: 6px 8px;
    background-color: #414141;
    border-radius: 3px;
    flex-direction: row;
    align-items: center;
}

.mcp-connected-tools__client-icon {
    margin-right: 4px;
}

.mcp-connected-tools__client-name {
    -unity-font-style: bold;
    color: #DDDDDD;
}

.mcp-connected-tools__client-port {
    color: var(--mcp-text-secondary);
    margin-left: 4px;
}

/* Foldout customization */
.unity-foldout__toggle {
    margin-left: 0;
    padding-left: 15px;
}

.unity-foldout__text {
    -unity-font-style: bold;
}

/* HelpBox styles */
.mcp-helpbox {
    padding: 8px;
    margin: 4px 0;
    border-radius: 3px;
    border-width: 1px;
}

.mcp-helpbox--info {
    background-color: var(--mcp-info-background);
    border-color: #2A3948;
}

.mcp-helpbox--warning {
    background-color: var(--mcp-warning-background);
    border-color: #493F27;
}

.mcp-helpbox--error {
    background-color: #5A3737;
    border-color: #492727;
}

/* Modifier examples */
.mcp-button--primary {
    background-color: var(--unity-colors-button-background-pressed);
}

.mcp-button--disabled {
    opacity: 0.5;
}
```

## 実装差分

### Phase 1: 新しいViewクラスの作成

```diff
+ // Packages/src/Editor/UI/UIToolkit/Views/McpEditorWindowUITView.cs
+ using UnityEngine;
+ using UnityEngine.UIElements;
+ using UnityEditor.UIElements;
+ 
+ namespace io.github.hatayama.uLoopMCP
+ {
+     /// <summary>
+     /// UI Toolkit implementation of McpEditorWindow View
+     /// </summary>
+     public class McpEditorWindowUITView
+     {
+         private VisualElement _root;
+         private ScrollView _scrollView;
+         
+         // Section containers
+         private VisualElement _serverStatusContainer;
+         private VisualElement _serverControlsContainer;
+         private Foldout _connectedToolsFoldout;
+         private Foldout _editorConfigFoldout;
+         private Foldout _securitySettingsFoldout;
+         
+         // View components
+         private ServerStatusView _serverStatusView;
+         private ServerControlsView _serverControlsView;
+         private ConnectedToolsView _connectedToolsView;
+         private EditorConfigView _editorConfigView;
+         private SecuritySettingsView _securitySettingsView;
+         
+         public VisualElement Root => _root;
+         
+         public void Initialize()
+         {
+             // Load UXML (USSはUXML内で自動的に読み込まれる)
+             VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
+                 "Packages/src/Editor/UI/UIToolkit/McpEditorWindow.uxml");
+             _root = visualTree.CloneTree();
+             
+             // Query containers
+             _scrollView = _root.Q<ScrollView>("main-scroll-view");
+             _serverStatusContainer = _root.Q<VisualElement>("server-status");
+             _serverControlsContainer = _root.Q<VisualElement>("server-controls");
+             _connectedToolsFoldout = _root.Q<Foldout>("connected-tools");
+             _editorConfigFoldout = _root.Q<Foldout>("editor-config");
+             _securitySettingsFoldout = _root.Q<Foldout>("security-settings");
+             
+             // Initialize sub-views
+             InitializeSubViews();
+         }
+         
+         private void InitializeSubViews()
+         {
+             _serverStatusView = new ServerStatusView(_serverStatusContainer);
+             _serverControlsView = new ServerControlsView(_serverControlsContainer);
+             _connectedToolsView = new ConnectedToolsView(_connectedToolsFoldout);
+             _editorConfigView = new EditorConfigView(_editorConfigFoldout);
+             _securitySettingsView = new SecuritySettingsView(_securitySettingsFoldout);
+         }
+         
+         public void UpdateServerStatus(ServerStatusData data)
+         {
+             _serverStatusView.Update(data);
+         }
+         
+         public void UpdateServerControls(ServerControlsData data, Action toggleCallback, 
+             Action<bool> autoStartCallback, Action<int> portCallback)
+         {
+             _serverControlsView.Update(data, toggleCallback, autoStartCallback, portCallback);
+         }
+         
+         // 他のUpdate メソッド...
+     }
+ }
```

### Phase 2: McpEditorWindowの段階的移行

```diff
  public class McpEditorWindow : EditorWindow
  {
+     // UI Toolkit移行フラグ
+     private const bool USE_UI_TOOLKIT = false; // 段階的に true に変更
+     
+     // UI Toolkit View
+     private McpEditorWindowUITView _uitView;
+     
      // Configuration services factory
      private McpConfigServiceFactory _configServiceFactory;
      
      // View layer
      private McpEditorWindowView _view;
      
      // ... existing code ...
      
      private void InitializeView()
      {
-         _view = new McpEditorWindowView();
+         if (USE_UI_TOOLKIT)
+         {
+             _uitView = new McpEditorWindowUITView();
+             _uitView.Initialize();
+             rootVisualElement.Add(_uitView.Root);
+         }
+         else
+         {
+             _view = new McpEditorWindowView();
+         }
      }
      
      private void OnEnable()
      {
          InitializeAll();
+         if (USE_UI_TOOLKIT)
+         {
+             StartBackgroundUpdates();
+         }
      }
      
+     private void OnDisable()
+     {
+         if (USE_UI_TOOLKIT)
+         {
+             StopBackgroundUpdates();
+         }
+         CleanupEventHandler();
+         SaveSessionState();
+     }
+     
+     private void StartBackgroundUpdates()
+     {
+         // UI Toolkitの定期更新を開始
+         rootVisualElement.schedule.Execute(UpdateUIToolkit)
+             .Every(500); // 500ms毎に更新（バックグラウンドでも動作）
+             
+         // フォーカス変更イベントを購読
+         EditorApplication.focusChanged += OnEditorFocusChanged;
+     }
+     
+     private void StopBackgroundUpdates()
+     {
+         // スケジュールされた更新を停止
+         rootVisualElement.schedule.Execute(UpdateUIToolkit).Pause();
+         
+         // イベント購読を解除
+         EditorApplication.focusChanged -= OnEditorFocusChanged;
+     }
+     
+     private void OnEditorFocusChanged(bool hasFocus)
+     {
+         if (USE_UI_TOOLKIT)
+         {
+             // フォーカス状態に応じて更新頻度を調整
+             var scheduler = rootVisualElement.schedule.Execute(UpdateUIToolkit);
+             if (hasFocus)
+             {
+                 scheduler.Every(200); // フォーカス時は高頻度更新
+             }
+             else
+             {
+                 scheduler.Every(1000); // バックグラウンド時は低頻度更新
+             }
+         }
+     }
+     
      private void OnGUI()
      {
+         if (USE_UI_TOOLKIT)
+         {
+             // UI Toolkitモードではマニュアル更新は不要
+             return;
+         }
+         
          // 既存のOnGUIコード...
      
+     private void UpdateUIToolkit()
+     {
+         // Synchronize server port and UI settings
+         SyncPortSettings();
+         
+         // Update server status
+         ServerStatusData statusData = CreateServerStatusData();
+         _uitView.UpdateServerStatus(statusData);
+         
+         // Update server controls
+         ServerControlsData controlsData = CreateServerControlsData();
+         _uitView.UpdateServerControls(controlsData, ToggleServer, 
+             UpdateAutoStartServer, UpdateCustomPort);
+         
+         // Update other sections...
+     }
  }
```

### Phase 3: サブビューの実装例

```diff
+ // Packages/src/Editor/UI/UIToolkit/Views/ServerStatusView.cs
+ using UnityEngine.UIElements;
+ 
+ namespace io.github.hatayama.uLoopMCP
+ {
+     public class ServerStatusView
+     {
+         private readonly VisualElement _container;
+         private Label _statusLabel;
+         private Label _statusValue;
+         
+         public ServerStatusView(VisualElement container)
+         {
+             _container = container;
+             BuildUI();
+         }
+         
+         private void BuildUI()
+         {
+             _container.Clear();
+             _container.AddToClassList("mcp-server-status");
+             
+             _statusLabel = new Label("Status:");
+             _statusLabel.AddToClassList("mcp-server-status__label");
+             _container.Add(_statusLabel);
+             
+             _statusValue = new Label();
+             _statusValue.AddToClassList("mcp-server-status__value");
+             _container.Add(_statusValue);
+         }
+         
+         public void Update(ServerStatusData data)
+         {
+             _statusValue.text = data.Status;
+             
+             // Remove existing status classes
+             _statusValue.RemoveFromClassList("mcp-server-status__value--running");
+             _statusValue.RemoveFromClassList("mcp-server-status__value--stopped");
+             
+             // Add appropriate status class
+             string statusClass = data.IsRunning ? 
+                 "mcp-server-status__value--running" : 
+                 "mcp-server-status__value--stopped";
+             _statusValue.AddToClassList(statusClass);
+         }
+     }
+ }
```

## 移行手順

### Step 1: 準備フェーズ（1日）
1. UI Toolkitファイル構造の作成
2. 基本的なUXML/USSファイルの作成
3. McpEditorWindowUITViewクラスの実装

### Step 2: 並行実装フェーズ（3-4日）
1. USE_UI_TOOLKITフラグの追加
2. 各セクションのViewクラス実装
3. 手動更新メソッドの実装（データバインディングは使用しない）
4. イベントハンドリングの実装

### Step 3: テストフェーズ（2日）
1. 機能テスト（両UIシステムの動作確認）
2. パフォーマンステスト
3. UI/UXの調整

### Step 4: 切り替えフェーズ（1日）
1. USE_UI_TOOLKITフラグをtrueに変更
2. 問題の修正
3. OnGUIコードの削除

## リスクと対策

### リスク
1. **手動更新の管理**: データの変更時に明示的なUpdate呼び出しが必要
2. **カスタムスタイリング**: 既存の見た目を完全に再現するのが困難な可能性
3. **イベントハンドリング**: OnGUIとUI Toolkitでイベント処理が異なる

### 対策
1. 段階的移行により、各部分を個別にテスト可能
2. BEM記法により、スタイルの管理を体系化
3. 十分なテスト期間の確保

## 成功指標
1. 全機能が正常に動作すること
2. UIの応答性が向上すること
3. コードの保守性が向上すること
4. 再利用可能なコンポーネントが作成されること
5. **現在のUnityダークテーマの見た目を完全に維持すること**
   - 背景色、セクション色、ボタン色の一致
   - フォントサイズ、余白、角丸の再現
   - ホバー効果やフォーカス状態の維持

## 参考資料
- [Unity UI Toolkit Documentation](https://docs.unity3d.com/Manual/UIE-HowTo-CreateEditorWindow.html)
- [Martin Fowler - Parallel Change](https://martinfowler.com/bliki/ParallelChange.html)
- [BEM Methodology](http://getbem.com/introduction/)