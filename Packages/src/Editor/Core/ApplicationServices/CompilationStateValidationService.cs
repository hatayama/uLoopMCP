using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// コンパイル状態検証サービス
    /// 単一機能：コンパイル実行前の状態検証を行う
    /// 関連クラス: CompileTool, CompileUseCase, McpSessionManager
    /// 設計書参照: DDDリファクタリング仕様 - Application Service Layer
    /// </summary>
    public class CompilationStateValidationService
    {
        /// <summary>
        /// コンパイル実行前の状態を検証する
        /// </summary>
        /// <returns>検証結果</returns>
        public ValidationResult ValidateCompilationState()
        {
            if (EditorApplication.isCompiling)
            {
                return ValidationResult.Failure("Compilation is already in progress. Please wait for the current compilation to finish.");
            }
            
            if (McpSessionManager.instance.IsDomainReloadInProgress)
            {
                return ValidationResult.Failure("Cannot compile while domain reload is in progress. Please wait for the domain reload to complete.");
            }
            
            if (EditorApplication.isUpdating)
            {
                return ValidationResult.Failure("Cannot compile while editor is updating. Please wait for the update to complete.");
            }
            
            return ValidationResult.Success();
        }
    }
}