
namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
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
