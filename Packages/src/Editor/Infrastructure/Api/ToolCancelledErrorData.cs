namespace io.github.hatayama.UnityCliLoop.Infrastructure
{
    /// <summary>
    /// Tells the CLI that waited for a tool that uloop cancel stopped it, so the CLI can report the
    /// cancel instead of a dropped connection.
    /// </summary>
    public class ToolCancelledErrorData : JsonRpcErrorData
    {
        public override string type => JsonRpcErrorTypes.ToolCancelled;

        public string runningToolName { get; }

        public ToolCancelledErrorData(string runningToolName, string message)
            : base(message)
        {
            this.runningToolName = runningToolName;
        }
    }
}
