using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace UnityCliLoop.CompileCheckWorker
{
    /// <summary>
    /// Builds the compilation csc would build from a Bee response file, without running generators.
    /// </summary>
    internal static class ResponseFileCompilationLoader
    {
        private static readonly IReadOnlyDictionary<string, byte[]> EmptySources = ImmutableDictionary<string, byte[]>.Empty;

        internal static CSharpCommandLineArguments ParseArguments(string projectRoot, string responseFilePath)
        {
            // The same arguments compile-check passes to csc, so options such as -nowarn,
            // -warnaserror, -ruleset, and -define come out as csc applies them.
            string[] arguments = new string[] { "/nostdlib", "/noconfig", "@" + responseFilePath };
            return CSharpCommandLineParser.Default.Parse(arguments, projectRoot, null, null);
        }

        internal static string FirstArgumentError(CSharpCommandLineArguments arguments)
        {
            Diagnostic error = arguments.Errors.FirstOrDefault(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
            return error == null ? null : error.GetMessage();
        }

        internal static SyntaxTree ParseSource(string path, byte[] bytes, CSharpCommandLineArguments arguments)
        {
            SourceText text = SourceText.From(new MemoryStream(bytes, writable: false), arguments.Encoding, arguments.ChecksumAlgorithm);
            return CSharpSyntaxTree.ParseText(text, arguments.ParseOptions, path);
        }

        /// <param name="preloadedSources">Bytes to use instead of reading the file, keyed by full path.</param>
        internal static LoadResult Load(
            string projectRoot,
            string responseFilePath,
            string generatedDirectory,
            IReadOnlyDictionary<string, byte[]> preloadedSources)
        {
            CSharpCommandLineArguments arguments = ParseArguments(projectRoot, responseFilePath);
            string argumentError = FirstArgumentError(arguments);
            if (argumentError != null)
            {
                return LoadResult.Unsupported("response file: " + argumentError);
            }

            // Editor config files would need the per-tree options csc derives from them.
            if (arguments.AnalyzerConfigPaths.Length > 0)
            {
                return LoadResult.Unsupported("analyzerconfig is not supported by the changed-files check");
            }

            string[] sourcePaths = arguments.SourceFiles.Select(source => source.Path).ToArray();
            SyntaxTree[] sourceTrees = ParseFiles(sourcePaths, arguments, preloadedSources);
            SyntaxTree[] generatedTrees = ParseFiles(ListGeneratedFiles(generatedDirectory), arguments, EmptySources);
            MetadataReference[] references = CreateReferences(projectRoot, arguments);

            AnalyzerAssemblyLoader loader = new AnalyzerAssemblyLoader();
            AssemblyLoadContext.Default.Resolving += loader.Resolve;
            List<AnalyzerReference> analyzerReferences = arguments.ResolveAnalyzerReferences(loader).ToList();
            ImmutableArray<DiagnosticAnalyzer> analyzers = analyzerReferences
                .SelectMany(reference => reference.GetAnalyzers(LanguageNames.CSharp))
                .ToImmutableArray();
            // Generators are counted, never run: the output of the last full compile is read instead.
            int generatorCount = analyzerReferences.Sum(reference => reference.GetGenerators(LanguageNames.CSharp).Length);
            ImmutableArray<AdditionalText> additionalTexts = arguments.AdditionalFiles
                .Select(file => (AdditionalText)new FileAdditionalText(file.Path, arguments.Encoding))
                .ToImmutableArray();

            CSharpCompilation compilation = CSharpCompilation.Create(
                arguments.CompilationName,
                sourceTrees.Concat(generatedTrees),
                references,
                arguments.CompilationOptions.WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default));

            Dictionary<string, SyntaxTree> sourceTreesByPath = new Dictionary<string, SyntaxTree>(StringComparer.Ordinal);
            foreach (SyntaxTree tree in sourceTrees)
            {
                sourceTreesByPath[Path.GetFullPath(tree.FilePath)] = tree;
            }

            return LoadResult.Loaded(compilation, analyzers, additionalTexts, generatorCount, sourceTreesByPath, arguments);
        }

        private static SyntaxTree[] ParseFiles(
            string[] paths,
            CSharpCommandLineArguments arguments,
            IReadOnlyDictionary<string, byte[]> preloadedSources)
        {
            // Keeps the order of the paths: the order of the trees decides the order csc binds them.
            SyntaxTree[] trees = new SyntaxTree[paths.Length];
            Parallel.For(0, paths.Length, index =>
            {
                byte[] bytes;
                if (!preloadedSources.TryGetValue(Path.GetFullPath(paths[index]), out bytes))
                {
                    bytes = File.ReadAllBytes(paths[index]);
                }

                trees[index] = ParseSource(paths[index], bytes, arguments);
            });
            return trees;
        }

        private static string[] ListGeneratedFiles(string generatedDirectory)
        {
            if (string.IsNullOrEmpty(generatedDirectory) || !Directory.Exists(generatedDirectory))
            {
                return Array.Empty<string>();
            }

            string[] files = Directory.GetFiles(generatedDirectory, "*.cs", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            return files;
        }

        private static MetadataReference[] CreateReferences(string projectRoot, CSharpCommandLineArguments arguments)
        {
            CommandLineReference[] commandLineReferences = arguments.MetadataReferences.ToArray();
            MetadataReference[] references = new MetadataReference[commandLineReferences.Length];
            Parallel.For(0, commandLineReferences.Length, index =>
            {
                CommandLineReference reference = commandLineReferences[index];
                string path = Path.GetFullPath(Path.Combine(projectRoot, reference.Reference));
                references[index] = MetadataReference.CreateFromFile(path, reference.Properties, null);
            });
            return references;
        }

        /// <summary>
        /// An additional file (for example an analyzer's settings file) read from disk on demand.
        /// </summary>
        private sealed class FileAdditionalText : AdditionalText
        {
            private readonly Encoding encoding;

            public FileAdditionalText(string path, Encoding encoding)
            {
                Path = path;
                this.encoding = encoding;
            }

            public override string Path { get; }

            public override SourceText GetText(CancellationToken cancellationToken = default)
            {
                using (FileStream stream = File.OpenRead(Path))
                {
                    return SourceText.From(stream, encoding);
                }
            }
        }
    }

    /// <summary>
    /// The compilation and analyzers of one assembly, or the reason it could not be built.
    /// </summary>
    internal sealed class LoadResult
    {
        private LoadResult()
        {
        }

        public string UnsupportedReason { get; private set; }

        public CSharpCompilation Compilation { get; private set; }

        public ImmutableArray<DiagnosticAnalyzer> Analyzers { get; private set; }

        public ImmutableArray<AdditionalText> AdditionalTexts { get; private set; }

        public int GeneratorCount { get; private set; }

        // Keyed by the full path of each source of the response file.
        public IReadOnlyDictionary<string, SyntaxTree> SourceTreesByPath { get; private set; }

        public CSharpCommandLineArguments Arguments { get; private set; }

        public static LoadResult Unsupported(string reason)
        {
            return new LoadResult { UnsupportedReason = reason };
        }

        public static LoadResult Loaded(
            CSharpCompilation compilation,
            ImmutableArray<DiagnosticAnalyzer> analyzers,
            ImmutableArray<AdditionalText> additionalTexts,
            int generatorCount,
            IReadOnlyDictionary<string, SyntaxTree> sourceTreesByPath,
            CSharpCommandLineArguments arguments)
        {
            return new LoadResult
            {
                Compilation = compilation,
                Analyzers = analyzers,
                AdditionalTexts = additionalTexts,
                GeneratorCount = generatorCount,
                SourceTreesByPath = sourceTreesByPath,
                Arguments = arguments,
            };
        }
    }
}
