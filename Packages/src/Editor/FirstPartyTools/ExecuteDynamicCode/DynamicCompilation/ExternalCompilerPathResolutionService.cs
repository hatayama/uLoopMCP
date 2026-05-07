
namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    internal sealed class ExternalCompilerPathResolutionService
    {
        public ExternalCompilerPaths Resolve()
        {
            return ExternalCompilerPathResolver.Resolve();
        }
    }
}
