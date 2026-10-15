using System.Collections.Generic;

namespace UnityCliLoop.CompileCheckWorker
{
    // The JSON shapes the Go side writes for the worker. Property names stay PascalCase because the
    // Go structs marshal their field names as they are.

    /// <summary>
    /// Asks for the declaration fingerprint and the content hash of every source of one assembly.
    /// </summary>
    internal sealed class FingerprintRequest
    {
        public string ProjectRoot { get; set; }

        public string ResponseFilePath { get; set; }

        public List<string> Sources { get; set; }
    }

    /// <summary>
    /// Asks to check only the changed files of an assembly whose last full compile succeeded.
    /// </summary>
    internal sealed class CheckRequest
    {
        public string ProjectRoot { get; set; }

        public string ResponseFilePath { get; set; }

        public List<string> ChangedFiles { get; set; }

        public Dictionary<string, string> BaselineFingerprints { get; set; }

        public string GeneratedDirectory { get; set; }
    }

    /// <summary>
    /// Asks whether the reference assembly compile-check produced for an assembly describes the same
    /// surface, as a dependent compiles against it, as the one the last Unity build produced.
    /// </summary>
    internal sealed class CompareSurfaceRequest
    {
        public string ProducedPath { get; set; }

        public string UnityBuildPath { get; set; }
    }
}
