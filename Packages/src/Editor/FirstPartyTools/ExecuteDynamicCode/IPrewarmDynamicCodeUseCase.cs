using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    internal interface IPrewarmDynamicCodeUseCase
    {
        void Request();

        Task RequestAsync();
    }
}
