/**
 * 軽量サービスロケーターパターンの実装
 *
 * 設計ドキュメント参照:
 * - .kiro/specs/typescript-server-ddd-refactoring/design.md#依存性注入の設計
 *
 * 関連クラス:
 * - ApplicationService実装クラス群（application/services/）
 * - UseCase実装クラス群（domain/use-cases/）
 * - Infrastructure層のクラス群（infrastructure/）
 */

/**
 * サービスロケーター
 *
 * 責任:
 * - 依存性の登録と解決
 * - ファクトリー関数による遅延初期化
 * - シングルトンパターンによるサービス管理（ApplicationService）
 * - 毎回新しいインスタンス生成（UseCase）
 *
 * 注意:
 * - DIコンテナではなく軽量なパターンを採用
 * - プロジェクト方針に従いシンプルな実装
 */
export class ServiceLocator {
  private static services: Map<string, () => unknown> = new Map();

  /**
   * サービスのファクトリー関数を登録
   *
   * @param token サービストークン
   * @param factory ファクトリー関数
   */
  static register<T>(token: string, factory: () => T): void {
    this.services.set(token, factory);
  }

  /**
   * サービスのインスタンスを解決
   *
   * @param token サービストークン
   * @returns サービスのインスタンス
   * @throws Error サービスが登録されていない場合
   */
  static resolve<T>(token: string): T {
    const factory = this.services.get(token);
    if (!factory) {
      throw new Error(`Service not registered: ${token}`);
    }
    return factory() as T;
  }

  /**
   * 全てのサービス登録をクリア
   *
   * 主にテスト時に使用
   */
  static clear(): void {
    this.services.clear();
  }

  /**
   * 登録されているサービス一覧を取得
   *
   * 主にデバッグ時に使用
   */
  static getRegisteredServices(): string[] {
    return Array.from(this.services.keys());
  }

  /**
   * サービスが登録されているかチェック
   *
   * @param token サービストークン
   * @returns 登録されている場合true
   */
  static isRegistered(token: string): boolean {
    return this.services.has(token);
  }
}
