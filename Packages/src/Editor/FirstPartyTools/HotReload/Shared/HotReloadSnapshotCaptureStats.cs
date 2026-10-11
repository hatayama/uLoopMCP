using System;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// The time one source snapshot capture spent in each of its phases and how much it did, so the
    /// breakdown log shows where a slow capture went.
    /// </summary>
    internal sealed class HotReloadSnapshotCaptureStats
    {
        // Times are TimeSpan ticks, summed over every assembly and source of the capture.
        internal long PackageLookupTicks;
        internal long StampTicks;
        internal long MvidTicks;
        internal long StatTicks;
        internal long ReadWriteTicks;
        internal long PdbCheckTicks;
        internal long PublishTicks;
        internal long CleanupTicks;

        internal int Assemblies;
        internal int AssembliesImmutable;
        internal int AssembliesUnchanged;
        internal int AssembliesCaptured;
        internal int FilesCopied;
        internal long BytesCopied;
        internal int FilesChecked;

        internal object ToVibeLogContext(long getAssembliesMs, long totalMs)
        {
            return new
            {
                getAssembliesMs,
                packageLookupMs = ToMilliseconds(PackageLookupTicks),
                stampMs = ToMilliseconds(StampTicks),
                mvidMs = ToMilliseconds(MvidTicks),
                statMs = ToMilliseconds(StatTicks),
                readWriteMs = ToMilliseconds(ReadWriteTicks),
                pdbCheckMs = ToMilliseconds(PdbCheckTicks),
                publishMs = ToMilliseconds(PublishTicks),
                cleanupMs = ToMilliseconds(CleanupTicks),
                totalMs,
                assemblies = Assemblies,
                assembliesImmutable = AssembliesImmutable,
                assembliesUnchanged = AssembliesUnchanged,
                assembliesCaptured = AssembliesCaptured,
                filesCopied = FilesCopied,
                bytesCopied = BytesCopied,
                filesChecked = FilesChecked
            };
        }

        private static long ToMilliseconds(long ticks)
        {
            return (long)TimeSpan.FromTicks(ticks).TotalMilliseconds;
        }
    }
}
