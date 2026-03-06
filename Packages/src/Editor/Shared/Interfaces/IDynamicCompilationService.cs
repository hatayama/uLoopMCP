namespace io.github.hatayama.uLoopMCP
{
    public interface IDynamicCompilationService
    {
        CompilationResult Compile(CompilationRequest request);
    }
}
