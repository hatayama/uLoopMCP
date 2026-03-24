namespace io.github.hatayama.uLoopMCP
{
    public interface IPreloadAssemblySecurityValidator
    {
        SecurityValidationResult Validate(byte[] assemblyBytes);
    }
}
