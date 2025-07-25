using System.Threading.Tasks;
using System.Threading;
using System.Linq;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// コンパイル処理の時間的凝集を担当
    /// 処理順序：1. コンパイル状態の検証, 2. コンパイル実行, 3. 結果の整形
    /// 関連クラス: CompileTool, CompilationStateValidationService, CompilationExecutionService
    /// 設計書参照: DDDリファクタリング仕様 - UseCase Layer
    /// </summary>
    public class CompileUseCase : AbstractUseCase<CompileSchema, CompileResponse>
    {
        /// <summary>
        /// コンパイル処理を実行する
        /// </summary>
        /// <param name="parameters">コンパイルパラメータ</param>
        /// <param name="cancellationToken">キャンセレーション制御用トークン</param>
        /// <returns>コンパイル結果</returns>
        public override async Task<CompileResponse> ExecuteAsync(CompileSchema parameters, CancellationToken cancellationToken)
        {
            // 1. コンパイル状態の検証
            var validationService = new CompilationStateValidationService();
            ValidationResult validation = validationService.ValidateCompilationState();
            
            if (!validation.IsValid)
            {
                return new CompileResponse(
                    success: false,
                    errorCount: 1,
                    warningCount: 0,
                    errors: new[] { new CompileIssue(validation.ErrorMessage, "", 0) },
                    warnings: new CompileIssue[0]
                );
            }
            
            // 2. コンパイル実行
            cancellationToken.ThrowIfCancellationRequested();
            var executionService = new CompilationExecutionService();
            CompileResult result = await executionService.ExecuteCompilationAsync(parameters.ForceRecompile);
            
            // 3. 結果の整形
            if (result.IsIndeterminate)
            {
                return new CompileResponse(
                    success: result.Success,
                    errorCount: null,
                    warningCount: null,
                    errors: null,
                    warnings: null,
                    message: "Force compilation executed. Use get-logs tool to retrieve compilation messages.",
                    isIndeterminate: true
                );
            }
            
            CompileIssue[] errors = result.error?.Select(e => new CompileIssue(e.message, e.file, e.line)).ToArray();
            CompileIssue[] warnings = result.warning?.Select(w => new CompileIssue(w.message, w.file, w.line)).ToArray();
            
            return new CompileResponse(
                success: result.Success,
                errorCount: result.error?.Length ?? 0,
                warningCount: result.warning?.Length ?? 0,
                errors: errors,
                warnings: warnings
            );
        }
    }
}