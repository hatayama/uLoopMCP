namespace io.github.hatayama.UnityCliLoop
{
    internal static class SystemReflectionMetadataPreloadValidatorRegistration
    {
        internal static void RegisterForEditorStartup()
        {
            PreloadAssemblySecurityValidatorRegistry.RegisterValidator(new SystemReflectionMetadataPreloadValidator());
        }
    }
}
