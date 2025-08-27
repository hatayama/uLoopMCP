// Operation summary to collect success/failure and provide a compact report
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;

namespace io.github.hatayama.uLoopMCP
{
    public class OperationSummary
    {
        private readonly List<string> _successes = new List<string>();
        private readonly List<string> _failures = new List<string>();

        public void AddSuccess(string message)
        {
            _successes.Add(message);
        }

        public void AddFailure(string message)
        {
            _failures.Add(message);
        }

        public string BuildReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Operation Summary");
            sb.AppendLine($" Successes: {_successes.Count}");
            foreach (var s in _successes)
            {
                sb.AppendLine($"  - {s}");
            }
            sb.AppendLine($" Failures: {_failures.Count}");
            foreach (var f in _failures)
            {
                sb.AppendLine($"  - {f}");
            }
            return sb.ToString();
        }
    }
}
#endif


