using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    // Exposes the dynamic-code tool startup sequence without exposing implementation collaborators publicly.
    internal static class ExecuteDynamicCodeEditorStartup
    {
        internal static void Initialize()
        {
            AssemblyTypeIndex.InvalidateForEditorStartup();
            DynamicReferenceSetBuilder.InvalidateReferenceCacheForEditorStartup();
            SharedRoslynCompilerWorkerHost.RegisterLifecycleForEditorStartup();
        }
    }
}
