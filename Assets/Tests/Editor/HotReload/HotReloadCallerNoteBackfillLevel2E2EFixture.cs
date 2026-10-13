using System.Runtime.CompilerServices;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// Compiled host whose one method the level-2 caller-note backfill E2E edits; its caller chain
    /// runs through the cross-assembly relay to the level-2 fixture assembly, so a cold run's
    /// backfill must read both dlls before the next run reads none.
    /// </summary>
    public class HotReloadCallerNoteBackfillLevel2E2EFixture
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int Read()
        {
            return 1;
        }
    }
}
