namespace io.github.hatayama.UnityCliLoop
{
    public class ControlPlayModeResponse : UnityCliLoopToolResponse
    {
        public bool IsPlaying { get; set; }
        public bool IsPaused { get; set; }
        public string Message { get; set; }
    }
}

