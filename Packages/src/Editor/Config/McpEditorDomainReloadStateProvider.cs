using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    public sealed class McpEditorDomainReloadStateProvider : IDomainReloadStateProvider
    {
        public bool IsDomainReloadInProgress()
        {
            return McpEditorSettings.GetIsDomainReloadInProgress();
        }
    }

    public static class McpEditorDomainReloadStateRegistration
    {
        [InitializeOnLoadMethod]
        private static void Register()
        {
            DomainReloadStateRegistry.RegisterProvider(new McpEditorDomainReloadStateProvider());
        }
    }
}
