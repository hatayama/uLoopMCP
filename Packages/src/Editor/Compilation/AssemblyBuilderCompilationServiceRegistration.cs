using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    public static class AssemblyBuilderCompilationServiceRegistration
    {
        [InitializeOnLoadMethod]
        private static void Register()
        {
            DynamicCompilationServiceRegistry.RegisterFactory(new AssemblyBuilderCompilationServiceFactory());
        }
    }
}
