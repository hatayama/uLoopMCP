# Unity Push Notification System Migration

## 概要

このファイルは既存のpollingシステムからPush通知システムへの移行について記録します。

## 変更内容

### 無効化されたコンポーネント

1. **Unity Discovery Polling（unity-discovery.ts）**
   - `POLLING.ENABLED = false`により無効化
   - 既存のコードは維持（フォールバック用）
   - UnityConnectionManagerで制御

2. **定期的なUnity探索**
   - 1秒間隔のpolling無効化
   - Push通知による能動的接続に置き換え

### 新システムの利点

1. **効率性向上**
   - 定期的なネットワークスキャン不要
   - イベント駆動型通信
   - システムリソース削減

2. **レスポンス向上**
   - Unityイベントに即座に応答
   - ドメインリロード復帰の高速化
   - 接続状態のリアルタイム把握

3. **堅牢性向上**
   - 詳細な切断理由通知
   - エラーハンドリング強化
   - 自動復旧メカニズム

## フォールバック機能

Push通知システムが機能しない場合：
- `POLLING.ENABLED = true`に変更
- 既存のpollingシステムが自動復帰
- 後方互換性の維持

## 設定変更

### constants.ts
```typescript
export const POLLING = {
  INTERVAL_MS: 1000,
  BUFFER_SECONDS: 15,
  ENABLED: false, // ← 追加：Push通知システムが優先
} as const;
```

### unity-connection-manager.ts
- POLLING.ENABLEDフラグによる制御追加
- Push通知システム優先ロジック実装
- フォールバック機能の維持

## 注意事項

1. **完全削除ではない**
   - 既存コードは保持
   - トラブル時の迅速な復帰が可能

2. **環境変数依存**
   - UNITY_TCP_PORT環境変数は引き続き必要
   - 既存のTCP通信（8700ポート）は維持

3. **互換性**
   - 既存のMCPクライアントとの互換性維持
   - Unityツールコマンドはそのまま動作

## 移行完了チェック

- [x] Push通知受信サーバー実装
- [x] Unity Push通知クライアント実装
- [x] エンドポイント永続化（ScriptableSingleton）
- [x] ドメインリロード対応
- [x] エラーハンドリング
- [x] 既存polling無効化
- [x] フォールバック機能保持