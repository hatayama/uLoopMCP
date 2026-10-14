using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using NUnit.Framework;

namespace UnityCliLoop.CompileCheckWorker.UnitTests
{
    [TestFixture]
    public sealed class ChangedFileCheckerTests
    {
        private const string ProbeTemplate =
            "namespace Sample.Game\n" +
            "{\n" +
            "    public static class Probe\n" +
            "    {\n" +
            "        public static void Run()\n" +
            "        {\n" +
            "            {0}\n" +
            "        }\n" +
            "    }\n" +
            "}\n";

        private WorkerFixture fixture;

        [SetUp]
        public void SetUp()
        {
            fixture = new WorkerFixture();
        }

        [TearDown]
        public void TearDown()
        {
            fixture.Dispose();
        }

        /// <summary>
        /// Verifies that a compiler error in a changed file is reported with the file, line, and column csc reports.
        /// </summary>
        [Test]
        public void ReportsCompilerErrorInChangedFile()
        {
            string probe = fixture.AddSource("Probe.cs", Probe("int value = \"s\";"));

            CheckResponse response = CheckWithCurrentBaseline(probe);

            Assert.That(response.Outcome, Is.EqualTo(CheckResponse.CheckedOutcome));
            WorkerDiagnostic error = response.Diagnostics.Single(diagnostic => diagnostic.Code == "CS0029");
            Assert.That(error.Severity, Is.EqualTo(WorkerDiagnostic.ErrorSeverity));
            Assert.That(error.File, Is.EqualTo(Path.GetFullPath(probe)));
            Assert.That(error.Line, Is.EqualTo(7));
            Assert.That(error.Column, Is.EqualTo(25));
        }

        /// <summary>
        /// Verifies that a DiagnosticSuppressor of the assembly suppresses a compiler warning in a changed file.
        /// </summary>
        [Test]
        public void SuppressorSuppressesRelayedCompilerWarning()
        {
            fixture.AddAnalyzer(TestAnalyzerSources.SuppressorAssemblyName, TestAnalyzerSources.Suppressor);
            string probe = fixture.AddSource("Probe.cs", Probe("int suppressedValue = 1;"));

            CheckResponse response = CheckWithCurrentBaseline(probe);

            Assert.That(response.Outcome, Is.EqualTo(CheckResponse.CheckedOutcome));
            Assert.That(Codes(response), Does.Not.Contain("CS0219"));
        }

        /// <summary>
        /// Verifies that a compiler warning the suppressor declines is still reported once, as a warning.
        /// </summary>
        [Test]
        public void UnsuppressedRelayedWarningIsReported()
        {
            fixture.AddAnalyzer(TestAnalyzerSources.SuppressorAssemblyName, TestAnalyzerSources.Suppressor);
            string probe = fixture.AddSource("Probe.cs", Probe("int keptValue = 1;"));

            CheckResponse response = CheckWithCurrentBaseline(probe);

            WorkerDiagnostic warning = response.Diagnostics.Single(diagnostic => diagnostic.Code == "CS0219");
            Assert.That(warning.Severity, Is.EqualTo(WorkerDiagnostic.WarningSeverity));
        }

        /// <summary>
        /// Verifies that -warnaserror+ in the response file reports a compiler warning as an error.
        /// </summary>
        [Test]
        public void WarnAsErrorPromotesWarning()
        {
            fixture.AddArgument("-warnaserror+");
            string probe = fixture.AddSource("Probe.cs", Probe("int keptValue = 1;"));

            CheckResponse response = CheckWithCurrentBaseline(probe);

            WorkerDiagnostic promoted = response.Diagnostics.Single(diagnostic => diagnostic.Code == "CS0219");
            Assert.That(promoted.Severity, Is.EqualTo(WorkerDiagnostic.ErrorSeverity));
        }

        /// <summary>
        /// Verifies that -nowarn in the response file drops the warning.
        /// </summary>
        [Test]
        public void NowarnDropsWarning()
        {
            fixture.AddArgument("-nowarn:CS0219");
            string probe = fixture.AddSource("Probe.cs", Probe("int keptValue = 1;"));

            CheckResponse response = CheckWithCurrentBaseline(probe);

            Assert.That(response.Outcome, Is.EqualTo(CheckResponse.CheckedOutcome));
            Assert.That(Codes(response), Does.Not.Contain("CS0219"));
        }

        /// <summary>
        /// Verifies that a syntax node analyzer of the assembly reports its diagnostic in a changed file.
        /// </summary>
        [Test]
        public void AnalyzerDiagnosticInChangedFileIsReported()
        {
            fixture.AddAnalyzer(TestAnalyzerSources.NodeAnalyzerAssemblyName, TestAnalyzerSources.NodeAnalyzer);
            string probe = fixture.AddSource("Probe.cs", Probe("int flagged = 1; System.GC.KeepAlive(flagged);"));

            CheckResponse response = CheckWithCurrentBaseline(probe);

            WorkerDiagnostic diagnostic = response.Diagnostics.Single(item => item.Code == "TSA001");
            Assert.That(diagnostic.File, Is.EqualTo(Path.GetFullPath(probe)));
        }

        /// <summary>
        /// Verifies that errors in files that did not change are not reported.
        /// </summary>
        [Test]
        public void DiagnosticsInUnchangedFilesAreNotReported()
        {
            string probe = fixture.AddSource("Probe.cs", Probe("System.GC.KeepAlive(1);"));
            fixture.AddSource("Other.cs", "class Other { void M() { int value = \"s\"; } }");

            CheckResponse response = CheckWithCurrentBaseline(probe);

            Assert.That(response.Outcome, Is.EqualTo(CheckResponse.CheckedOutcome));
            Assert.That(response.Diagnostics, Is.Empty);
        }

        /// <summary>
        /// Verifies that a changed file whose declarations differ from the baseline asks for a full compile.
        /// </summary>
        [Test]
        public void DeclarationChangeReturnsDeclarationChanged()
        {
            string probe = fixture.AddSource("Probe.cs", Probe("System.GC.KeepAlive(1);"));
            Dictionary<string, string> baseline = fixture.FingerprintSources();
            File.WriteAllText(probe, Probe("System.GC.KeepAlive(1); } public static void Added() {"));

            CheckResponse response = fixture.Check(baseline, "", probe);

            Assert.That(response.Outcome, Is.EqualTo(CheckResponse.DeclarationChangedOutcome));
            Assert.That(response.Diagnostics, Is.Empty);
        }

        /// <summary>
        /// Verifies that a changed file with no baseline fingerprint asks for a full compile.
        /// </summary>
        [Test]
        public void MissingBaselineFingerprintReturnsDeclarationChanged()
        {
            string probe = fixture.AddSource("Probe.cs", Probe("System.GC.KeepAlive(1);"));

            CheckResponse response = fixture.Check(new Dictionary<string, string>(), "", probe);

            Assert.That(response.Outcome, Is.EqualTo(CheckResponse.DeclarationChangedOutcome));
            Assert.That(response.Reason, Does.Contain(probe));
        }

        /// <summary>
        /// Verifies that a response file with an analyzer config is reported as unsupported.
        /// </summary>
        [Test]
        public void AnalyzerConfigReturnsUnsupported()
        {
            string configPath = fixture.WriteFile(".editorconfig", "root = true\n");
            fixture.AddArgument("-analyzerconfig:\"" + configPath + "\"");
            string probe = fixture.AddSource("Probe.cs", Probe("System.GC.KeepAlive(1);"));

            CheckResponse response = CheckWithCurrentBaseline(probe);

            Assert.That(response.Outcome, Is.EqualTo(CheckResponse.UnsupportedOutcome));
            Assert.That(response.Reason, Does.Contain("analyzerconfig"));
        }

        /// <summary>
        /// Verifies that the rules a per-file check cannot run are listed: CompilationEnd and SymbolEnd
        /// analyzer rules and the unused-member compiler warnings, minus those turned off with -nowarn.
        /// </summary>
        [Test]
        public void UncheckedCodesListCompilationEndAndUnusedFieldRules()
        {
            fixture.AddAnalyzer(TestAnalyzerSources.CompilationEndAnalyzerAssemblyName, TestAnalyzerSources.CompilationEndAnalyzer);
            fixture.AddAnalyzer(TestAnalyzerSources.SymbolEndAnalyzerAssemblyName, TestAnalyzerSources.SymbolEndAnalyzer);
            string probe = fixture.AddSource("Probe.cs", Probe("System.GC.KeepAlive(1);"));

            CheckResponse response = CheckWithCurrentBaseline(probe);

            Assert.That(response.UncheckedCodes, Is.EqualTo(new[] { "CS0067", "CS0169", "CS0414", "CS0649", "TSA002", "TSA003" }));

            fixture.AddArgument("-nowarn:CS0414");
            CheckResponse withNowarn = CheckWithCurrentBaseline(probe);

            Assert.That(withNowarn.UncheckedCodes, Is.EqualTo(new[] { "CS0067", "CS0169", "CS0649", "TSA002", "TSA003" }));
        }

        /// <summary>
        /// Verifies that types from the saved generator output are visible to a changed file.
        /// </summary>
        [Test]
        public void GeneratedDirectoryTreesAreVisibleToChangedFile()
        {
            string generatedDirectory = Path.Combine(fixture.Root, "Sample.Game.generated");
            fixture.WriteFile("Sample.Game.generated/Generator/Made.g.cs", "namespace Generated { public static class Made { public static int Value = 1; } }");
            string probe = fixture.AddSource("Probe.cs", Probe("int value = Generated.Made.Value; System.GC.KeepAlive(value);"));
            Dictionary<string, string> baseline = fixture.FingerprintSources();

            CheckResponse response = fixture.Check(baseline, generatedDirectory, probe);

            Assert.That(response.Outcome, Is.EqualTo(CheckResponse.CheckedOutcome));
            Assert.That(response.Diagnostics, Is.Empty);
        }

        /// <summary>
        /// Verifies that the content hash a fingerprint request returns is the sha256 of the exact bytes on
        /// disk, and that the fingerprint is the one computed from those bytes.
        /// </summary>
        [Test]
        public void FingerprintReturnsContentHashOfParsedBytes()
        {
            string probe = fixture.AddSource("Probe.cs", "");
            byte[] bytes = new UTF8Encoding(true).GetPreamble()
                .Concat(Encoding.UTF8.GetBytes("class Probe\r\n{\r\n    void M() { }\r\n}\r\n"))
                .ToArray();
            File.WriteAllBytes(probe, bytes);
            fixture.WriteResponseFile();

            FingerprintResponse response = FingerprintCommand.Run(new FingerprintRequest
            {
                ProjectRoot = fixture.Root,
                ResponseFilePath = fixture.ResponseFilePath,
                Sources = new List<string> { probe },
            });

            string expectedHash;
            using (SHA256 sha256 = SHA256.Create())
            {
                expectedHash = string.Concat(sha256.ComputeHash(bytes).Select(b => b.ToString("x2")));
            }

            Assert.That(response.ContentHashes[probe], Is.EqualTo(expectedHash));
            SyntaxTree tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText("class Probe\n{\n    void M() { }\n}\n");
            Assert.That(response.Fingerprints[probe], Is.EqualTo(DeclarationFingerprint.Compute(tree)));
        }

        private CheckResponse CheckWithCurrentBaseline(string changedFile)
        {
            Dictionary<string, string> baseline = fixture.FingerprintSources();
            return fixture.Check(baseline, "", changedFile);
        }

        private static List<string> Codes(CheckResponse response)
        {
            return response.Diagnostics.Select(diagnostic => diagnostic.Code).ToList();
        }

        private static string Probe(string statement)
        {
            return ProbeTemplate.Replace("{0}", statement);
        }
    }
}
