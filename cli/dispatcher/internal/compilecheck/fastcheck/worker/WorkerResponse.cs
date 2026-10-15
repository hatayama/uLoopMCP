using System.Collections.Generic;

namespace UnityCliLoop.CompileCheckWorker
{
    /// <summary>
    /// Fingerprints and content hashes keyed by the absolute source path of the request.
    /// </summary>
    internal sealed class FingerprintResponse
    {
        public Dictionary<string, string> Fingerprints { get; set; } = new Dictionary<string, string>();

        // The sha256 of the exact bytes the fingerprint was computed from, so the caller can tell
        // whether the file it compiled is the file that was fingerprinted.
        public Dictionary<string, string> ContentHashes { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Result of a changed-files check. Only "Checked" carries diagnostics the caller may report.
    /// </summary>
    internal sealed class CheckResponse
    {
        public const string CheckedOutcome = "Checked";
        public const string DeclarationChangedOutcome = "DeclarationChanged";
        public const string UnsupportedOutcome = "Unsupported";

        public string Outcome { get; set; }

        public string Reason { get; set; } = "";

        public List<WorkerDiagnostic> Diagnostics { get; set; } = new List<WorkerDiagnostic>();

        public List<string> UncheckedCodes { get; set; } = new List<string>();

        public bool GeneratorsNotRerun { get; set; }

        public static CheckResponse DeclarationChanged(string reason)
        {
            return new CheckResponse { Outcome = DeclarationChangedOutcome, Reason = reason };
        }

        public static CheckResponse Unsupported(string reason)
        {
            return new CheckResponse { Outcome = UnsupportedOutcome, Reason = reason };
        }
    }

    /// <summary>
    /// Result of a surface comparison. "same" only when every dependent would compile against either
    /// reference assembly with the same outcome and the same warnings; anything the comparison cannot
    /// vouch for is "different". A dependent that already fails because it names a private nested type
    /// may report that error under another ID (CS0246, CS0426 or CS0122).
    /// </summary>
    internal sealed class CompareSurfaceResponse
    {
        public const string SameOutcome = "same";
        public const string DifferentOutcome = "different";

        public string Outcome { get; set; }

        // The first line the two descriptions disagree on, or why they could not be read; empty for "same".
        public string Detail { get; set; } = "";

        public static CompareSurfaceResponse Same()
        {
            return new CompareSurfaceResponse { Outcome = SameOutcome };
        }

        public static CompareSurfaceResponse Different(string detail)
        {
            return new CompareSurfaceResponse { Outcome = DifferentOutcome, Detail = detail };
        }
    }

    /// <summary>
    /// One error or warning, located the way csc reports it (1-based line and column).
    /// </summary>
    internal sealed class WorkerDiagnostic
    {
        public const string ErrorSeverity = "error";
        public const string WarningSeverity = "warning";

        public string File { get; set; }

        public int Line { get; set; }

        public int Column { get; set; }

        public string Severity { get; set; }

        public string Code { get; set; }

        public string Message { get; set; }
    }
}
