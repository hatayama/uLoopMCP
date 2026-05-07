using System.Collections.Generic;

using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    internal sealed class DynamicReferenceSetBuilderService
    {
        public List<string> BuildReferenceSet(
            List<string> additionalReferences,
            IReadOnlyCollection<string> resolvedAssemblyReferences,
            ExternalCompilerPaths externalCompilerPaths)
        {
            return DynamicReferenceSetBuilder.BuildReferenceSet(
                additionalReferences,
                resolvedAssemblyReferences,
                externalCompilerPaths);
        }

        public string[] MergeReferencesByAssemblyName(
            string[] baseReferences,
            List<string> additionalReferences)
        {
            return DynamicReferenceSetBuilder.MergeReferencesByAssemblyName(baseReferences, additionalReferences);
        }
    }
}
