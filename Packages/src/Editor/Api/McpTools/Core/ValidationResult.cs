namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// 検証結果を表すデータモデル
    /// アプリケーションサービスレイヤで使用される
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// 検証が成功したかどうか
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// 検証失敗時のエラーメッセージ
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// ValidationResultを作成する
        /// </summary>
        /// <param name="isValid">検証結果</param>
        /// <param name="errorMessage">エラーメッセージ（成功時はnull）</param>
        public ValidationResult(bool isValid, string errorMessage = null)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// 成功結果を作成する
        /// </summary>
        /// <returns>成功を表すValidationResult</returns>
        public static ValidationResult Success() => new(true);

        /// <summary>
        /// 失敗結果を作成する
        /// </summary>
        /// <param name="errorMessage">エラーメッセージ</param>
        /// <returns>失敗を表すValidationResult</returns>
        public static ValidationResult Failure(string errorMessage) => new(false, errorMessage);
    }
}