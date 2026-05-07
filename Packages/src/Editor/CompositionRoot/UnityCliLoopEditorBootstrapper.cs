namespace io.github.hatayama.UnityCliLoop
{
    // Orchestrates Editor startup from an instance so only Unity's entrypoint remains static.
    internal sealed class UnityCliLoopEditorBootstrapper
    {
        private readonly UnityCliLoopApplicationRegistration _applicationRegistration;

        internal UnityCliLoopEditorBootstrapper()
        {
            _applicationRegistration = new UnityCliLoopApplicationRegistration();
        }

        internal void Initialize()
        {
            _applicationRegistration.Register();
            ApplicationEditorStartup.Initialize();
            FirstPartyToolsEditorStartup.Initialize();
            InfrastructureEditorStartup.Initialize();
            PresentationEditorStartup.Initialize();
        }
    }
}
