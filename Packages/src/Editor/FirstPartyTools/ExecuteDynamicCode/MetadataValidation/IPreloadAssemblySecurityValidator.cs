using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    public interface IPreloadAssemblySecurityValidator
    {
        SecurityValidationResult Validate(byte[] assemblyBytes);
    }
}
