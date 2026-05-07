using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    internal sealed class DynamicCodeSourcePreparationService : IDynamicCodeSourcePreparationService
    {
        public PreparedDynamicCode Prepare(
            string source,
            string namespaceName,
            string className)
        {
            return DynamicCodeSourcePreparer.Prepare(source, namespaceName, className);
        }
    }
}
