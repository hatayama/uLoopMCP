using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Defines the Prewarm Dynamic Code use case boundary consumed by the owning tool.
    /// </summary>
    internal interface IPrewarmDynamicCodeUseCase
    {
        void Request();

        Task RequestAsync();
    }
}
