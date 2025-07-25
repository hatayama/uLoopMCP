namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// サービス実行結果を表すジェネリックデータモデル
    /// アプリケーションサービスレイヤで使用される
    /// </summary>
    /// <typeparam name="T">結果データの型</typeparam>
    public class ServiceResult<T>
    {
        /// <summary>
        /// 実行が成功したかどうか
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// 実行結果のデータ
        /// </summary>
        public T Data { get; }

        /// <summary>
        /// 実行失敗時のエラーメッセージ
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// ServiceResultを作成する
        /// </summary>
        /// <param name="success">実行結果</param>
        /// <param name="data">結果データ</param>
        /// <param name="errorMessage">エラーメッセージ（成功時はnull）</param>
        public ServiceResult(bool success, T data = default, string errorMessage = null)
        {
            Success = success;
            Data = data;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// 成功結果を作成する
        /// </summary>
        /// <param name="data">結果データ</param>
        /// <returns>成功を表すServiceResult</returns>
        public static ServiceResult<T> SuccessResult(T data) => new(true, data);

        /// <summary>
        /// 失敗結果を作成する
        /// </summary>
        /// <param name="errorMessage">エラーメッセージ</param>
        /// <returns>失敗を表すServiceResult</returns>
        public static ServiceResult<T> FailureResult(string errorMessage) => new(false, default, errorMessage);
    }
}