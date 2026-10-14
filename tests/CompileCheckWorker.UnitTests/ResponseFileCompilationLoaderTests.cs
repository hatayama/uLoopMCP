using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace UnityCliLoop.CompileCheckWorker.UnitTests
{
    [TestFixture]
    public sealed class ResponseFileCompilationLoaderTests
    {
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
        /// Verifies that the response file options csc applies (defines, nowarn, warnaserror, additional
        /// files) come out in the loaded compilation, with one tree per source and per generated file.
        /// </summary>
        [Test]
        public void LoadAppliesResponseFileOptions()
        {
            fixture.AddSource("A.cs", "class A { }");
            fixture.AddSource("Sub/B.cs", "class B { }");
            string generatedDirectory = Path.Combine(fixture.Root, "generated");
            fixture.WriteFile("generated/Nested/G.cs", "class G { }");
            string settingsPath = fixture.WriteFile("settings.txt", "setting");
            fixture.AddArgument("-define:FOO;BAR");
            fixture.AddArgument("-nowarn:CS0219");
            fixture.AddArgument("-warnaserror+");
            fixture.AddArgument("-additionalfile:\"" + settingsPath + "\"");
            fixture.WriteResponseFile();

            LoadResult load = ResponseFileCompilationLoader.Load(
                fixture.Root, fixture.ResponseFilePath, generatedDirectory, new Dictionary<string, byte[]>());

            Assert.That(load.UnsupportedReason, Is.Null);
            CSharpCompilationOptions options = load.Compilation.Options;
            Assert.That(options.SpecificDiagnosticOptions["CS0219"], Is.EqualTo(ReportDiagnostic.Suppress));
            Assert.That(options.GeneralDiagnosticOption, Is.EqualTo(ReportDiagnostic.Error));
            CSharpParseOptions parseOptions = (CSharpParseOptions)load.Compilation.SyntaxTrees.First().Options;
            Assert.That(parseOptions.PreprocessorSymbolNames, Is.SupersetOf(new[] { "FOO", "BAR" }));
            Assert.That(load.Compilation.SyntaxTrees.Count(), Is.EqualTo(3));
            Assert.That(load.SourceTreesByPath.Count, Is.EqualTo(2));
            Assert.That(load.AdditionalTexts.Select(text => text.Path), Is.EqualTo(new[] { settingsPath }));
        }

        /// <summary>
        /// Verifies that a source given as preloaded bytes is parsed from those bytes, not read again from disk.
        /// </summary>
        [Test]
        public void LoadParsesPreloadedBytesInsteadOfRereading()
        {
            string probe = fixture.AddSource("Probe.cs", "class OnDisk { }");
            fixture.WriteResponseFile();
            Dictionary<string, byte[]> preloaded = new Dictionary<string, byte[]>
            {
                { Path.GetFullPath(probe), Encoding.UTF8.GetBytes("class Preloaded { }") },
            };

            LoadResult load = ResponseFileCompilationLoader.Load(fixture.Root, fixture.ResponseFilePath, "", preloaded);

            Assert.That(load.SourceTreesByPath[Path.GetFullPath(probe)].ToString(), Is.EqualTo("class Preloaded { }"));
        }
    }
}
