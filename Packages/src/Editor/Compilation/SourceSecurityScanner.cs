using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Pre-compilation source-level security scanner.
    /// Blocks dangerous API patterns before code reaches the compiler,
    /// preventing actually harmful code from executing (e.g. Process.Kill crashing Unity).
    /// </summary>
    internal static class SourceSecurityScanner
    {
        private static readonly List<DangerousPattern> Patterns = new()
        {
            // Process manipulation
            new("Process", "Start", @"\bProcess\s*\.\s*Start\b"),
            new("Process", "Kill", @"\b(?:global::\s*)?(?:System\s*\.\s*Diagnostics\s*\.\s*)?Process\s*\.\s*Kill\b"),

            // File system destructive operations
            new("File", "Delete", @"\bFile\s*\.\s*Delete\b"),
            new("File", "WriteAllText", @"\bFile\s*\.\s*WriteAllText\b"),
            new("File", "WriteAllBytes", @"\bFile\s*\.\s*WriteAllBytes\b"),
            new("File", "Replace", @"\bFile\s*\.\s*Replace\b"),
            new("File", "OpenWrite", @"\bFile\s*\.\s*OpenWrite\b"),
            new("File", "AppendAllText", @"\bFile\s*\.\s*AppendAllText\b"),
            new("Directory", "Delete", @"\bDirectory\s*\.\s*Delete\b"),

            // Network
            new("HttpClient", "new", @"\bnew\s+HttpClient\b"),
            new("HttpClient", "usage", @"\bHttpClient\s*\("),
            new("WebClient", "new", @"\bnew\s+WebClient\b"),
            new("WebRequest", "Create", @"\bWebRequest\s*\.\s*Create\b"),
            new("Socket", "new", @"\bnew\s+Socket\b"),
            new("TcpClient", "new", @"\bnew\s+TcpClient\b|\bTcpClient\s*\("),
            new("UdpClient", "new", @"\bnew\s+UdpClient\b|\bUdpClient\s*\("),
            new("ClientWebSocket", "new", @"\bnew\s+.*ClientWebSocket\b|\bClientWebSocket\s*\("),
            new("Dns", "GetHostEntry", @"\bDns\s*\.\s*GetHostEntry\b"),
            new("UnityWebRequest", "new", @"\bnew\s+UnityWebRequest\b"),

            // Reflection execution
            new("Assembly", "Load", @"\bAssembly\s*\.\s*Load\b"),
            new("Assembly", "LoadFrom", @"\bAssembly\s*\.\s*LoadFrom\b"),
            new("Type", "GetType", @"\b(?:global::\s*)?(?:System\s*\.\s*)?Type\s*\.\s*GetType\s*\("),
            new("Activator", "CreateComInstanceFrom", @"\bActivator\s*\.\s*CreateComInstanceFrom\b"),

            // Environment
            new("Environment", "Exit", @"\bEnvironment\s*\.\s*Exit\b"),
            new("Environment", "FailFast", @"\bEnvironment\s*\.\s*FailFast\b"),

            // GC manipulation
            new("GCSettings", "LatencyMode", @"\bGCSettings\s*\.\s*LatencyMode\s*="),

            // Thread manipulation
            new("Thread", "Abort", @"\bThread\s*\.\s*Abort\s*\("),

            // Asset deletion
            new("AssetDatabase", "DeleteAsset", @"\bAssetDatabase\s*\.\s*DeleteAsset\b"),
            new("FileUtil", "DeleteFileOrDirectory", @"\bFileUtil\s*\.\s*DeleteFileOrDirectory\b"),

            // Unsafe code
            new("unsafe", "block", @"\bunsafe\s*\{|\bunsafe\s+\w"),
            new("DllImport", "attribute", @"\bDllImport\b"),
            new("extern", "declaration", @"\bextern\s+\w"),
        };

        public static SecurityValidationResult Scan(string sourceCode)
        {
            SecurityValidationResult result = new SecurityValidationResult
            {
                IsValid = true,
                Violations = new List<SecurityViolation>()
            };

            if (string.IsNullOrWhiteSpace(sourceCode)) return result;

            // Strip comments and string literals to avoid false positives
            string stripped = StripCommentsAndStrings(sourceCode);

            foreach (DangerousPattern pattern in Patterns)
            {
                if (Regex.IsMatch(stripped, pattern.RegexPattern, RegexOptions.Singleline))
                {
                    result.Violations.Add(new SecurityViolation
                    {
                        Type = SecurityViolationType.DangerousApiCall,
                        ApiName = $"{pattern.TypeName}.{pattern.MemberName}",
                        Message = $"Dangerous API detected: {pattern.TypeName}.{pattern.MemberName}",
                        Description = $"Source contains blocked pattern: {pattern.TypeName}.{pattern.MemberName}"
                    });
                }
            }

            result.IsValid = result.Violations.Count == 0;
            return result;
        }

        private static string StripCommentsAndStrings(string source)
        {
            // Replace string literals and comments with spaces to avoid false positives
            // while preserving line structure for position tracking
            return Regex.Replace(source, @"
                //[^\n]*                    |  # line comment
                /\*[\s\S]*?\*/              |  # block comment
                @""(?:""""|[^""])*""        |  # verbatim string
                ""(?:\\.|[^\\""\n])*""      |  # regular string
                '(?:\\.|[^\\'\n])*'            # char literal
            ", match => new string(' ', match.Length), RegexOptions.IgnorePatternWhitespace);
        }

        private sealed class DangerousPattern
        {
            public string TypeName { get; }
            public string MemberName { get; }
            public string RegexPattern { get; }

            public DangerousPattern(string typeName, string memberName, string regexPattern)
            {
                TypeName = typeName;
                MemberName = memberName;
                RegexPattern = regexPattern;
            }
        }
    }
}
