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
