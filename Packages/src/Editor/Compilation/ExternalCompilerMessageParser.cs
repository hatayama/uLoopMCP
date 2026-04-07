using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor.Compilation;

namespace io.github.hatayama.uLoopMCP
{
    internal static class ExternalCompilerMessageParser
    {
        private static readonly Regex DiagnosticRegex = new(
            @"^(?<file>.+)\((?<line>\d+),(?<column>\d+)\): (?<severity>error|warning) (?<code>[A-Z]+\d+): (?<message>.+)$",
            RegexOptions.Compiled);

        public static CompilerMessage[] Parse(
            string stdout,
            string stderr,
            int exitCode)
        {
            List<CompilerMessage> messages = new List<CompilerMessage>();
            AddParsedMessages(messages, stdout);
            AddParsedMessages(messages, stderr);

            if (messages.Count > 0)
            {
                return messages.ToArray();
            }

            if (exitCode == 0)
            {
                return Array.Empty<CompilerMessage>();
            }

            string combinedOutput = CombineOutput(stdout, stderr);
            return new CompilerMessage[]
            {
                new CompilerMessage
                {
                    type = CompilerMessageType.Error,
                    message = string.IsNullOrWhiteSpace(combinedOutput)
                        ? "External C# compiler failed without diagnostics"
                        : combinedOutput
                }
            };
        }

        private static void AddParsedMessages(
            List<CompilerMessage> messages,
            string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return;
            }

            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                Match match = DiagnosticRegex.Match(line.Trim());
                if (!match.Success)
                {
                    continue;
                }

                messages.Add(new CompilerMessage
                {
                    type = match.Groups["severity"].Value == "warning"
                        ? CompilerMessageType.Warning
                        : CompilerMessageType.Error,
                    message = $"{match.Groups["code"].Value}: {match.Groups["message"].Value}",
                    file = match.Groups["file"].Value,
                    line = int.Parse(match.Groups["line"].Value),
                    column = int.Parse(match.Groups["column"].Value)
                });
            }
        }

        private static string CombineOutput(string stdout, string stderr)
        {
            if (string.IsNullOrWhiteSpace(stdout))
            {
                return stderr?.Trim();
            }

            if (string.IsNullOrWhiteSpace(stderr))
            {
                return stdout.Trim();
            }

            return $"{stdout.Trim()}\n{stderr.Trim()}";
        }
    }
}
