namespace io.github.hatayama.uLoopMCP
{
    public readonly struct CliInstallResult
    {
        public readonly bool Success;
        public readonly string ErrorOutput;

        public CliInstallResult(bool success, string errorOutput)
        {
            Success = success;
            ErrorOutput = errorOutput;
        }
    }
}
