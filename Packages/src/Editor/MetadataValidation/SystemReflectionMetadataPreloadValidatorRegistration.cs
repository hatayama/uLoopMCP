using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    public static class SystemReflectionMetadataPreloadValidatorRegistration
    {
        [InitializeOnLoadMethod]
        private static void Register()
        {
            PreloadAssemblySecurityValidatorRegistry.RegisterValidator(new SystemReflectionMetadataPreloadValidator());
        }
    }
}
