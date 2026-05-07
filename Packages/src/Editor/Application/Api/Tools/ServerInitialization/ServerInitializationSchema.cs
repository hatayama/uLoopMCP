namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Schema for server initialization request
    /// </summary>
    public class ServerInitializationSchema : UnityCliLoopToolSchema
    {
        public bool PreserveStartupLockUntilExplicitRelease { get; set; }
    }
}
