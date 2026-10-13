namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// Compiled caller one level above the cross-assembly relay, so the caller-note closure of the
    /// level-2 E2E host's method reaches a second dll only once the relay's dll has been read.
    /// </summary>
    public static class HotReloadCallerNoteBackfillLevel2E2ECaller
    {
        public static int Call()
        {
            return HotReloadCallerNoteBackfillLevel2E2ERelay.Call();
        }
    }
}
