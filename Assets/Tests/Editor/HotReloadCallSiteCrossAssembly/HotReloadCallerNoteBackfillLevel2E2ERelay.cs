namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// Compiled relay between the level-2 E2E host and its caller in the level-2 fixture assembly,
    /// so the caller-note closure walks two cross-assembly levels.
    /// </summary>
    public static class HotReloadCallerNoteBackfillLevel2E2ERelay
    {
        public static int Call()
        {
            return new HotReloadCallerNoteBackfillLevel2E2EFixture().Read();
        }
    }
}
