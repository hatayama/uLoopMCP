namespace io.github.hatayama.UnityCliLoop
{
    // Groups metadata-validation startup behind one facade for explicit bootstrap ordering.
    internal static class MetadataValidationEditorStartup
    {
        internal static void Initialize()
        {
            SystemReflectionMetadataPreloadValidatorRegistration.RegisterForEditorStartup();
        }
    }
}
