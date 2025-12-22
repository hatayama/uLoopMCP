namespace io.github.hatayama.uLoopMCP
{
    public class ControlPlayModeResponse : BaseToolResponse
    {
        public bool IsPlaying { get; set; }
        public bool IsPaused { get; set; }
        public string Message { get; set; }
    }
}

