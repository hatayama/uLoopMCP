namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Registration is intentionally NOT automatic via [InitializeOnLoadMethod].
    /// While Roslyn layer exists (Phase 1-4), Roslyn registers itself and takes precedence.
    /// Use DynamicCompilationServiceRegistry.SwapFactoryForTests() for testing.
    /// After Phase 5 (Roslyn removal), re-enable [InitializeOnLoadMethod] here.
    /// </summary>
    public static class AssemblyBuilderCompilationServiceRegistration
    {
        public static void Register()
        {
            DynamicCompilationServiceRegistry.RegisterFactory(new AssemblyBuilderCompilationServiceFactory());
        }
    }
}
