namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Catalog payload returned by the internal CLI tool-details bridge command.
    /// </summary>
    public class GetToolDetailsResponse : UnityCliLoopToolResponse
    {
        public ToolInfo[] Tools { get; set; }
    }
}
