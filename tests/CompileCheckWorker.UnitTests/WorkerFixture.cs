using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace UnityCliLoop.CompileCheckWorker.UnitTests
{
    /// <summary>
    /// A throwaway project directory with sources, a response file, and analyzer DLLs built from
    /// source, laid out the way compile-check hands them to the worker.
    /// </summary>
    internal sealed class WorkerFixture : IDisposable
    {
        private readonly List<string> sources = new List<string>();
        private readonly List<string> extraArguments = new List<string>();

        public WorkerFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "compile-check-worker-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string ResponseFilePath => Path.Combine(Root, "Sample.Game.rsp");

        public string AddSource(string relativePath, string content)
        {
            string path = WriteFile(relativePath, content);
            sources.Add(path);
            return path;
        }

        public string WriteFile(string relativePath, string content)
        {
            string path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return path;
        }

        public void AddArgument(string argument)
        {
            extraArguments.Add(argument);
        }

        public void AddAnalyzer(string assemblyName, string source)
        {
            string path = Path.Combine(Root, "analyzers", assemblyName + ".dll");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                new SyntaxTree[] { CSharpSyntaxTree.ParseText(source) },
                AnalyzerBuildReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            Microsoft.CodeAnalysis.Emit.EmitResult result = compilation.Emit(path);
            Assert.That(result.Success, Is.True, string.Join("\n", result.Diagnostics));
            extraArguments.Add("-analyzer:\"" + path + "\"");
        }

        public string WriteResponseFile()
        {
            List<string> lines = new List<string>
            {
                "-target:library",
                "-out:\"" + Path.Combine(Root, "Sample.Game.dll") + "\"",
            };
            lines.AddRange(CompileReferencePaths().Select(path => "-r:\"" + path + "\""));
            lines.AddRange(extraArguments);
            lines.AddRange(sources.Select(path => "\"" + path + "\""));
            File.WriteAllText(ResponseFilePath, string.Join("\n", lines), new UTF8Encoding(false));
            return ResponseFilePath;
        }

        public Dictionary<string, string> FingerprintSources()
        {
            WriteResponseFile();
            FingerprintRequest request = new FingerprintRequest
            {
                ProjectRoot = Root,
                ResponseFilePath = ResponseFilePath,
                Sources = new List<string>(sources),
            };
            return FingerprintCommand.Run(request).Fingerprints;
        }

        public CheckResponse Check(Dictionary<string, string> baseline, string generatedDirectory, params string[] changedFiles)
        {
            WriteResponseFile();
            CheckRequest request = new CheckRequest
            {
                ProjectRoot = Root,
                ResponseFilePath = ResponseFilePath,
                ChangedFiles = changedFiles.ToList(),
                BaselineFingerprints = baseline,
                GeneratedDirectory = generatedDirectory,
            };
            return ChangedFileChecker.Check(request);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
            }
        }

        private static IEnumerable<string> CompileReferencePaths()
        {
            string runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
            return new string[]
            {
                typeof(object).Assembly.Location,
                Path.Combine(runtimeDirectory, "System.Runtime.dll"),
            };
        }

        private static IEnumerable<MetadataReference> AnalyzerBuildReferences()
        {
            string trustedAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            IEnumerable<string> paths = trustedAssemblies
                .Split(Path.PathSeparator)
                .Concat(new string[]
                {
                    typeof(DiagnosticAnalyzer).Assembly.Location,
                    typeof(CSharpCompilation).Assembly.Location,
                })
                .Distinct(StringComparer.Ordinal);
            return paths.Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToList();
        }
    }
}
