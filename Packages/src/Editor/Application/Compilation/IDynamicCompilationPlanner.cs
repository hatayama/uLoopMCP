namespace io.github.hatayama.UnityCliLoop
{
    public interface IDynamicCodeSourcePreparationService
    {
        PreparedDynamicCode Prepare(
            string source,
            string namespaceName,
            string className);
    }

    public interface IDynamicCompilationPlanner
    {
        DynamicCompilationPlan CreatePlan(CompilationRequest request);
    }
}
