using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Codex (~/.codex/config.toml) 向けの設定サービス（TOML 文字列ベース）。
    /// </summary>
    public sealed class CodexTomlConfigService : IMcpConfigService
    {
        private static readonly Regex SectionRegex = new Regex(
            @"(?ms)^\[mcp_servers\.uLoopMCP\]\s*.*?(?=^\[|\z)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex AnyMcpServerRegex = new Regex(
            @"(?ms)^\[mcp_servers\.[^\]]+\]\s*.*?(?=^\[|\z)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex Arg0Regex = new Regex(
            @"(?ms)^\[mcp_servers\.uLoopMCP\].*?^\s*args\s*=\s*\[\s*""(?<arg0>[^""\r\n]+)""",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex PortRegex = new Regex(
            @"env\s*=\s*\{[^}]*""UNITY_TCP_PORT""\s*=\s*['""]?(?<port>\d+)['""]?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Inner-scope regexes for parsing within the matched uLoopMCP section
        private static readonly Regex Arg0InnerRegex = new Regex(
            @"(?m)^\s*args\s*=\s*\[\s*""(?<arg0>[^""\r\n]+)""",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex PortInnerRegex = new Regex(
            @"(?m)^\s*env\s*=\s*\{[^\}]*""UNITY_TCP_PORT""\s*=\s*['""]?(?<port>\d+)['""]?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex EnvLineRegex = new Regex(
            @"(?m)^\s*env\s*=\s*\{(?<body>[^\}]*)\}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex EnvPairRegex = new Regex(
            @"(?m)""(?<key>[^""]+)""\s*=\s*(?:""(?<value>[^""]+)""|(?<value>[^,}\r\n]+))",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public bool IsConfigured()
        {
            string path = UnityMcpPathResolver.GetCodexConfigPath();
            if (!File.Exists(path)) return false;
            string content = File.ReadAllText(path);
            return SectionRegex.IsMatch(content);
        }

        public bool IsUpdateNeeded(int port)
        {
            string path = UnityMcpPathResolver.GetCodexConfigPath();
            if (!File.Exists(path)) return true;
            string content = File.ReadAllText(path);

            string expectedArg0 = UnityMcpPathResolver.GetTypeScriptServerPath();
            if (string.IsNullOrEmpty(expectedArg0) || !File.Exists(expectedArg0)) return false;
            (string arg0, int? existingPort) = ReadCurrentValues(content);
            if (string.IsNullOrEmpty(arg0) || existingPort == null) return true;

            if (arg0 != expectedArg0 || existingPort.Value != port) return true;

            // When compiled with ULOOPMCP_DEBUG, require debug env flags to be present
#if ULOOPMCP_DEBUG
            Match section = SectionRegex.Match(content);
            if (section.Success)
            {
                System.Collections.Generic.Dictionary<string, string> envPairs = ParseEnvPairs(section.Value);
                bool hasMcpDebug = envPairs.TryGetValue("MCP_DEBUG", out string debugVal) && string.Equals(debugVal, "true", System.StringComparison.Ordinal);
                bool hasNodeOptions = envPairs.TryGetValue("NODE_OPTIONS", out string nodeOpt) && string.Equals(nodeOpt, "--enable-source-maps", System.StringComparison.Ordinal);
                if (!hasMcpDebug || !hasNodeOptions) return true;
            }
#endif

            return false;
        }

        public void AutoConfigure(int port)
        {
            string path = UnityMcpPathResolver.GetCodexConfigPath();
            EnsureDirectory(path);
            string content = File.Exists(path) ? File.ReadAllText(path) : string.Empty;

            string serverPath = UnityMcpPathResolver.GetTypeScriptServerPath();
            if (string.IsNullOrEmpty(serverPath) || !File.Exists(serverPath))
            {
                throw new System.InvalidOperationException("TypeScript server bundle path not found.");
            }
            string block = BuildBlock(port, serverPath);

            string result;
            if (SectionRegex.IsMatch(content))
            {
                result = SectionRegex.Replace(content, block.TrimEnd() + System.Environment.NewLine);
            }
            else
            {
                var matches = AnyMcpServerRegex.Matches(content);
                if (matches.Count > 0)
                {
                    var last = matches[matches.Count - 1];
                    int insertIndex = last.Index + last.Length;
                    result = content.Insert(insertIndex, System.Environment.NewLine + block);
                }
                else
                {
                    result = string.IsNullOrWhiteSpace(content) ? block : content.TrimEnd() + System.Environment.NewLine + System.Environment.NewLine + block;
                }
            }

            File.WriteAllText(path, result);
        }

        public int GetConfiguredPort()
        {
            string path = UnityMcpPathResolver.GetCodexConfigPath();
            if (!File.Exists(path)) throw new System.InvalidOperationException("Configuration file not found.");
            string content = File.ReadAllText(path);
            (_, int? port) = ReadCurrentValues(content);
            if (port == null) throw new System.InvalidOperationException("UNITY_TCP_PORT not found.");
            return port.Value;
        }

        public void UpdateDevelopmentSettings(int port, bool developmentMode, bool enableMcpLogs)
        {
            string path = UnityMcpPathResolver.GetCodexConfigPath();
            if (!File.Exists(path))
            {
                AutoConfigure(port);
            }

            string content = File.ReadAllText(path);

            // If section not exists, create it first and reload
            if (!SectionRegex.IsMatch(content))
            {
                AutoConfigure(port);
                content = File.ReadAllText(path);
            }

            // Replace only within the uLoopMCP section and update dev logs settings
            Match section = SectionRegex.Match(content);
            if (!section.Success)
            {
                // As a last attempt, try to autoconfigure and reload once
                AutoConfigure(port);
                content = File.ReadAllText(path);
                section = SectionRegex.Match(content);
                if (!section.Success)
                {
                    return;
                }
            }

            string updatedSection = UpdateSectionWithDevelopmentSettings(section.Value, port, developmentMode, enableMcpLogs);
            string newContent = content.Substring(0, section.Index) + updatedSection + content.Substring(section.Index + section.Length);
            File.WriteAllText(path, newContent);
        }

        private static (string arg0, int? port) ReadCurrentValues(string content)
        {
            Match section = SectionRegex.Match(content);
            if (!section.Success) return (null, null);

            string arg0 = null;
            int? port = null;

            Match a = Arg0InnerRegex.Match(section.Value);
            if (a.Success)
            {
                arg0 = a.Groups["arg0"].Value;
            }

            Match p = PortInnerRegex.Match(section.Value);
            if (p.Success)
            {
                int value;
                if (int.TryParse(p.Groups["port"].Value, out value))
                {
                    port = value;
                }
            }

            return (arg0, port);
        }

        private static string BuildBlock(int port, string serverAbsolutePath)
        {
            string escapedPath = serverAbsolutePath.Replace("\\", "\\\\").Replace("\"", "\\\"");
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[mcp_servers.uLoopMCP]");
            sb.AppendLine("command = \"node\"");
            sb.Append("args = [\"").Append(escapedPath).AppendLine("\"]");
            System.Text.StringBuilder envLine = new System.Text.StringBuilder();
            envLine.Append("env = { \"UNITY_TCP_PORT\" = \"").Append(port.ToString()).Append("\"");
#if ULOOPMCP_DEBUG
            envLine.Append(", \"MCP_DEBUG\" = \"true\", \"NODE_OPTIONS\" = \"--enable-source-maps\"");
#endif
            envLine.AppendLine(" }");
            sb.Append(envLine.ToString());
            return sb.ToString();
        }

        private static string UpdateSectionWithDevelopmentSettings(string sectionText, int port, bool developmentMode, bool enableMcpLogs)
        {
            // Ensure env line exists and update key-values
            Match envMatch = EnvLineRegex.Match(sectionText);
            string envBody = envMatch.Success ? envMatch.Groups["body"].Value : string.Empty;

            System.Collections.Generic.Dictionary<string, string> pairs = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);

            if (!string.IsNullOrEmpty(envBody))
            {
                MatchCollection mc = EnvPairRegex.Matches(envBody);
                foreach (Match m in mc)
                {
                    string key = m.Groups["key"].Value;
                    string value = m.Groups["value"].Value;
                    if (!pairs.ContainsKey(key))
                    {
                        pairs.Add(key, value);
                    }
                }
            }

            // Update mandatory port
            pairs["UNITY_TCP_PORT"] = port.ToString();

            // Clean legacy flags
            pairs.Remove("ULOOPMCP_DEBUG");
            pairs.Remove("ULOOPMCP_PRODUCTION");

            // Apply MCP debug logs
            if (enableMcpLogs)
            {
                pairs["MCP_DEBUG"] = "true";
                pairs["NODE_OPTIONS"] = "--enable-source-maps";
            }
            else
            {
                pairs.Remove("MCP_DEBUG");
                pairs.Remove("NODE_OPTIONS");
            }

            // Rebuild env line with deterministic ordering
            System.Collections.Generic.List<string> orderedKeys = new System.Collections.Generic.List<string>();
            if (pairs.ContainsKey("UNITY_TCP_PORT")) orderedKeys.Add("UNITY_TCP_PORT");
            if (pairs.ContainsKey("MCP_DEBUG")) orderedKeys.Add("MCP_DEBUG");
            if (pairs.ContainsKey("NODE_OPTIONS")) orderedKeys.Add("NODE_OPTIONS");
            foreach (System.Collections.Generic.KeyValuePair<string, string> kv in pairs)
            {
                if (orderedKeys.Contains(kv.Key)) continue;
                orderedKeys.Add(kv.Key);
            }

            System.Text.StringBuilder envBuilder = new System.Text.StringBuilder();
            for (int i = 0; i < orderedKeys.Count; i++)
            {
                string key = orderedKeys[i];
                string value = pairs[key];
                if (i > 0) envBuilder.Append(", ");
                envBuilder.Append("\"").Append(key).Append("\"").Append(" = ");
                envBuilder.Append("\"").Append(value).Append("\"");
            }
            string newEnvLine = "env = { " + envBuilder.ToString() + " }";

            string updatedSection;
            if (envMatch.Success)
            {
                updatedSection = sectionText.Substring(0, envMatch.Index) + newEnvLine + sectionText.Substring(envMatch.Index + envMatch.Length);
            }
            else
            {
                // Insert after args line if exists, otherwise append at end
                Match argsMatch = Arg0InnerRegex.Match(sectionText);
                if (argsMatch.Success)
                {
                    int insertIndex = argsMatch.Index + argsMatch.Length;
                    // Find end of line
                    int lineEnd = sectionText.IndexOf('\n', insertIndex);
                    if (lineEnd < 0) lineEnd = insertIndex;
                    string prefix = sectionText.Substring(0, lineEnd + 1);
                    string suffix = sectionText.Substring(lineEnd + 1);
                    updatedSection = prefix + newEnvLine + System.Environment.NewLine + suffix;
                }
                else
                {
                    updatedSection = sectionText.TrimEnd() + System.Environment.NewLine + newEnvLine + System.Environment.NewLine;
                }
            }

            return updatedSection;
        }

        private static System.Collections.Generic.Dictionary<string, string> ParseEnvPairs(string sectionText)
        {
            System.Collections.Generic.Dictionary<string, string> pairs = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);
            Match envMatch = EnvLineRegex.Match(sectionText);
            if (!envMatch.Success) return pairs;

            string envBody = envMatch.Groups["body"].Value;
            if (string.IsNullOrEmpty(envBody)) return pairs;

            MatchCollection mc = EnvPairRegex.Matches(envBody);
            foreach (Match m in mc)
            {
                string key = m.Groups["key"].Value;
                string value = m.Groups["value"].Value;
                if (!pairs.ContainsKey(key))
                {
                    pairs.Add(key, value);
                }
            }

            return pairs;
        }

        private static void EnsureDirectory(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }
    }
}


