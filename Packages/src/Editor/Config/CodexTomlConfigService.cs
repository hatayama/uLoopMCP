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
        private static readonly Regex SectionRegex = new Regex("(?ms)^\\[mcp_servers\\.uLoopMCP\\]\\s*.*?(?=^\\[|\\z)");
        private static readonly Regex AnyMcpServerRegex = new Regex("(?ms)^\\[mcp_servers\\.[^\\]]+\\]\\s*.*?(?=^\\[|\\z)");
        private static readonly Regex Arg0Regex = new Regex("(?ms)^\\[mcp_servers\\.uLoopMCP\\].*?^args\\s*=\\s*\\[\\s*\"(?<arg0>[^\"\\r\\n]+)\"");
        private static readonly Regex PortRegex = new Regex("env\\s*=\\s*\\{[^}]*\"UNITY_TCP_PORT\"\\s*=\\s*\"(?<port>\\d+)\"");

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
            (string arg0, int? existingPort) = ReadCurrentValues(content);
            if (string.IsNullOrEmpty(arg0) || existingPort == null) return true;

            return arg0 != expectedArg0 || existingPort.Value != port;
        }

        public void AutoConfigure(int port)
        {
            string path = UnityMcpPathResolver.GetCodexConfigPath();
            EnsureDirectory(path);
            string content = File.Exists(path) ? File.ReadAllText(path) : string.Empty;

            string serverPath = UnityMcpPathResolver.GetTypeScriptServerPath();
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
                return;
            }

            string content = File.ReadAllText(path);

            // If section not exists, create it first
            if (!SectionRegex.IsMatch(content))
            {
                AutoConfigure(port);
                return;
            }

            // Replace only the port value in env
            string newContent = PortRegex.Replace(content, m =>
            {
                return m.Value.Replace(m.Groups["port"].Value, port.ToString());
            });

            File.WriteAllText(path, newContent);
        }

        private static (string arg0, int? port) ReadCurrentValues(string content)
        {
            string arg0 = null;
            int? port = null;

            var m = Arg0Regex.Match(content);
            if (m.Success)
            {
                arg0 = m.Groups["arg0"].Value;
            }

            var p = PortRegex.Match(content);
            if (p.Success && int.TryParse(p.Groups["port"].Value, out int v))
            {
                port = v;
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
            sb.Append("env = { \"UNITY_TCP_PORT\" = \"").Append(port.ToString()).AppendLine("\" }");
            return sb.ToString();
        }

        private static void EnsureDirectory(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }
    }
}


