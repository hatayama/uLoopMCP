using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Compilation execution service
    /// Single function: Execute Unity project compilation
    /// Related classes: CompileController, CompileUseCase, CompileTool
    /// Design reference: @Packages/docs/ARCHITECTURE_Unity.md - Application Service Layer (Single Function Implementation)
    /// </summary>
    public class CompilationExecutionService
    {
        /// <summary>
        /// Execute compilation asynchronously
        /// </summary>
        /// <param name="forceRecompile">Force recompile flag</param>
        /// <returns>Compilation result</returns>
        public async Task<CompileResult> ExecuteCompilationAsync(bool forceRecompile)
        {
            using CompileController compileController = new();
            return await compileController.TryCompileAsync(forceRecompile);
        }
    }
}