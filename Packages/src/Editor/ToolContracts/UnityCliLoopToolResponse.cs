namespace io.github.hatayama.UnityCliLoop
{
    public static class UnityCliLoopToolContractVersion
    {
        private static string _version = string.Empty;

        public static string Current => _version;

        public static void SetCurrent(string version)
        {
            System.Diagnostics.Debug.Assert(version != null, "version must not be null.");
            _version = version;
        }
    }

    /// <summary>
    /// Base response class for Unity CLI tool responses.
    /// </summary>
    public abstract class UnityCliLoopToolResponse
    {
        /// <summary>
        /// Unity package version for CLI compatibility checks.
        /// </summary>
        public string Ver => UnityCliLoopToolContractVersion.Current;
    }
}
