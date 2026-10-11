namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// Fixture saved with a UTF-8 byte order mark, so the live-source tests can check that the live
    /// file and the snapshot decode the mark the same way. Its method body gives it a PDB document.
    /// </summary>
    internal static class HotReloadLiveSourceBomFixture
    {
        internal static int Answer()
        {
            return 42;
        }
    }
}
