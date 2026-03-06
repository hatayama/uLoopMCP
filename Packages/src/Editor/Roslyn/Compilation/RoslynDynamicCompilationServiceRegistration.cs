#if ULOOPMCP_HAS_ROSLYN
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    public static class RoslynDynamicCompilationServiceRegistration
    {
        [InitializeOnLoadMethod]
        private static void Register()
        {
            DynamicCompilationServiceRegistry.RegisterFactory(new RoslynDynamicCompilationServiceFactory());
        }
    }
}
#endif
