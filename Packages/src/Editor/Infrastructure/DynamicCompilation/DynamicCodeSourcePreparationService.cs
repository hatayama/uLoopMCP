namespace io.github.hatayama.UnityCliLoop
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
