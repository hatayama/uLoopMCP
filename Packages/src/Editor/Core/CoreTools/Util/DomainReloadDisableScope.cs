using System;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    public class DomainReloadDisableScope : IDisposable
    {
        private readonly bool _originalEnabled;
        private readonly EnterPlayModeOptions _originalOptions;
        
        public DomainReloadDisableScope()
        {
            _originalEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            _originalOptions = EditorSettings.enterPlayModeOptions;
            
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        }
        
        public void Dispose()
        {
            EditorSettings.enterPlayModeOptionsEnabled = _originalEnabled;
            EditorSettings.enterPlayModeOptions = _originalOptions;
        }
    }
}