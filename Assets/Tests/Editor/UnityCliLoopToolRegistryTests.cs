using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace io.github.hatayama.UnityCliLoop
{
    [TestFixture]
    public sealed class UnityCliLoopToolRegistryTests
    {
        [Test]
        public void Constructor_WhenFirstPartyToolsUseToolAttribute_RegistersThem()
        {
            // Tests that bundled tools use the same attribute-based registry path as extension tools.
            UnityCliLoopToolRegistry registry = new UnityCliLoopToolRegistry();

            Assert.That(registry.IsToolRegistered("compile"), Is.True);
            Assert.That(registry.IsToolRegistered("get-logs"), Is.True);
            Assert.That(registry.IsToolRegistered("execute-dynamic-code"), Is.True);
        }

        [Test]
        public void IsThirdPartyTool_WhenToolComesFromApplicationAssembly_ReturnsFalse()
        {
            // Tests that the renamed application assembly is still classified as first-party.
            UnityCliLoopToolRegistry registry = new UnityCliLoopToolRegistry();

            Assert.That(registry.IsThirdPartyTool("get-logs"), Is.False);
        }

        [Test]
        public void Constructor_WhenSampleToolUsesToolContractsAssembly_RegistersAsThirdParty()
        {
            // Tests that a sample extension tool uses the same registry path while remaining outside first-party assemblies.
            UnityCliLoopToolRegistry registry = new UnityCliLoopToolRegistry();

            Assert.That(registry.IsToolRegistered("hello-world"), Is.True);
            Assert.That(registry.IsThirdPartyTool("hello-world"), Is.True);
        }

        [Test]
        public void CustomCommandSamplesAsmdef_ReferencesOnlyToolContracts()
        {
            // Tests that third-party sample tools depend only on the public tool contract assembly.
            string asmdefPath = Path.Combine(
                UnityMcpPathResolver.GetProjectRoot(),
                "Assets",
                "Editor",
                "CustomCommandSamples",
                "UnityCLILoop.CustomCommandSamples.Editor.asmdef");
            JObject asmdef = JObject.Parse(File.ReadAllText(asmdefPath));
            string[] references = asmdef["references"]?.Values<string>().ToArray() ?? new string[0];

            Assert.That(references, Is.EqualTo(new[] { "UnityCLILoop.ToolContracts" }));
        }
    }
}
