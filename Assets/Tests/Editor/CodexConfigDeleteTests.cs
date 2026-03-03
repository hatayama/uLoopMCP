using System;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP
{
    [TestFixture]
    public class CodexConfigDeleteTests
    {
        private static readonly Type CodexServiceType = typeof(CodexTomlConfigService);

        private static Regex GetSectionRegex()
        {
            FieldInfo field = CodexServiceType.GetField("SectionRegex", BindingFlags.NonPublic | BindingFlags.Static);
            UnityEngine.Debug.Assert(field != null, "SectionRegex field not found");
            return (Regex)field.GetValue(null);
        }

        private static string SimulateDelete(string content)
        {
            Regex sectionRegex = GetSectionRegex();
            string result = sectionRegex.Replace(content, string.Empty);
            result = Regex.Replace(result, @"(\r?\n){3,}", Environment.NewLine + Environment.NewLine);
            return result;
        }

        [Test]
        public void Should_DeleteULoopMCPSection_WhenAtEnd()
        {
            string toml = @"[mcp_servers.other]
command = ""python""
args = ['app.py']
env = { ""API_KEY"" = ""abc"" }

[mcp_servers.uLoopMCP]
command = ""node""
args = ['server.js']
env = { ""UNITY_TCP_PORT"" = ""8800"" }
";

            string result = SimulateDelete(toml);

            StringAssert.DoesNotContain("uLoopMCP", result);
            StringAssert.Contains("mcp_servers.other", result);
            StringAssert.Contains("python", result);
        }

        [Test]
        public void Should_DeleteULoopMCPSection_WhenAtBeginning()
        {
            string toml = @"[mcp_servers.uLoopMCP]
command = ""node""
args = ['server.js']
env = { ""UNITY_TCP_PORT"" = ""8800"" }

[mcp_servers.other]
command = ""python""
args = ['app.py']
env = { ""API_KEY"" = ""abc"" }
";

            string result = SimulateDelete(toml);

            StringAssert.DoesNotContain("uLoopMCP", result);
            StringAssert.Contains("mcp_servers.other", result);
            StringAssert.Contains("python", result);
        }

        [Test]
        public void Should_DeleteULoopMCPSection_WhenInMiddle()
        {
            string toml = @"[mcp_servers.first]
command = ""python""
args = ['first.py']

[mcp_servers.uLoopMCP]
command = ""node""
args = ['server.js']
env = { ""UNITY_TCP_PORT"" = ""8800"" }

[mcp_servers.last]
command = ""ruby""
args = ['last.rb']
";

            string result = SimulateDelete(toml);

            StringAssert.DoesNotContain("uLoopMCP", result);
            StringAssert.Contains("mcp_servers.first", result);
            StringAssert.Contains("mcp_servers.last", result);
        }

        [Test]
        public void Should_PreserveOtherSections_WhenOnlyULoopMCPExists()
        {
            string toml = @"[mcp_servers.uLoopMCP]
command = ""node""
args = ['server.js']
env = { ""UNITY_TCP_PORT"" = ""8800"" }
";

            string result = SimulateDelete(toml);

            StringAssert.DoesNotContain("uLoopMCP", result);
        }

        [Test]
        public void Should_ReturnUnchanged_WhenNoULoopMCPSection()
        {
            string toml = @"[mcp_servers.other]
command = ""python""
args = ['app.py']
";

            Regex sectionRegex = GetSectionRegex();
            bool hasMatch = sectionRegex.IsMatch(toml);

            Assert.IsFalse(hasMatch, "SectionRegex should not match non-uLoopMCP sections");
        }
    }
}
