using System.Threading.Tasks;
using System.Threading;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// UseCase基底クラス - 時間的凝集を担当
    /// 毎回新しいインスタンスが生成され、Execute完了後に破棄される
    /// 関連クラス: AbstractUnityTool, BaseToolSchema, BaseToolResponse
    /// </summary>
    /// <typeparam name="TSchema">Schema type for tool parameters</typeparam>
    /// <typeparam name="TResponse">Response type for tool results</typeparam>
    public abstract class AbstractUseCase<TSchema, TResponse>
        where TSchema : BaseToolSchema
        where TResponse : BaseToolResponse
    {
        /// <summary>
        /// UseCase実行メソッド - 唯一のpublicメソッド
        /// 時間的凝集（複数の操作を順序立てて実行）を担当する
        /// </summary>
        /// <param name="parameters">型安全なパラメータ</param>
        /// <param name="cancellationToken">キャンセレーション制御用トークン</param>
        /// <returns>型安全な実行結果</returns>
        public abstract Task<TResponse> ExecuteAsync(TSchema parameters, CancellationToken cancellationToken);
    }
}