using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// コンパイル実行サービス
    /// 単一機能：Unity プロジェクトのコンパイルを実行する
    /// 関連クラス: CompileController, CompileUseCase, CompileTool
    /// 設計書参照: DDDリファクタリング仕様 - Application Service Layer
    /// </summary>
    public class CompilationExecutionService
    {
        /// <summary>
        /// コンパイルを非同期実行する
        /// </summary>
        /// <param name="forceRecompile">強制再コンパイルフラグ</param>
        /// <returns>コンパイル結果</returns>
        public async Task<CompileResult> ExecuteCompilationAsync(bool forceRecompile)
        {
            using CompileController compileController = new();
            return await compileController.TryCompileAsync(forceRecompile);
        }
    }
}