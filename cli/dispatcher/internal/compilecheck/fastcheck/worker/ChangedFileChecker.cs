using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace UnityCliLoop.CompileCheckWorker
{
    /// <summary>
    /// Checks only the changed files of an assembly against the rest of it, reporting what csc
    /// would report for those files, or why the assembly needs a full compile instead.
    /// </summary>
    internal static class ChangedFileChecker
    {
        private const string AnalyzerExceptionDiagnosticId = "AD0001";

        internal static CheckResponse Check(CheckRequest request)
        {
            // The declaration check runs first: a changed declaration means a full compile anyway,
            // and finding that out costs one parse instead of loading every reference and source.
            Dictionary<string, byte[]> changedBytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            CheckResponse declarationChange = FindDeclarationChange(request, changedBytes);
            if (declarationChange != null)
            {
                return declarationChange;
            }

            // The changed files are compiled from the bytes their fingerprints were computed from, so
            // an edit made after the fingerprint check cannot slip into the result unchecked.
            LoadResult load = ResponseFileCompilationLoader.Load(
                request.ProjectRoot, request.ResponseFilePath, request.GeneratedDirectory, changedBytes);
            if (load.UnsupportedReason != null)
            {
                return CheckResponse.Unsupported(load.UnsupportedReason);
            }

            List<SyntaxTree> changedTrees = new List<SyntaxTree>();
            foreach (string changed in request.ChangedFiles)
            {
                SyntaxTree tree;
                if (!load.SourceTreesByPath.TryGetValue(Path.GetFullPath(changed), out tree))
                {
                    return CheckResponse.Unsupported("changed file is not a source of the response file: " + changed);
                }

                changedTrees.Add(tree);
            }

            ImmutableHashSet<string> relayIds = CollectRelayIds(load.Analyzers);
            ImmutableArray<DiagnosticAnalyzer> allAnalyzers = load.Analyzers.Add(new SuppressibleCompilerDiagnosticRelay(relayIds));
            CompilationWithAnalyzersOptions options = new CompilationWithAnalyzersOptions(
                new AnalyzerOptions(load.AdditionalTexts),
                null,
                true,
                false,
                false);
            CompilationWithAnalyzers withAnalyzers = load.Compilation.WithAnalyzers(allAnalyzers, options);

            List<Diagnostic> collected = new List<Diagnostic>();
            foreach (SyntaxTree tree in changedTrees)
            {
                collected.AddRange(CheckTree(withAnalyzers, tree, relayIds));
            }

            CultureInfo culture = load.Arguments.PreferredUILang ?? CultureInfo.CurrentUICulture;
            // csc reports an analyzer that threw as AD0001 without a source location, which the
            // per-file result would drop; a full compile reports it as csc does.
            Diagnostic analyzerFailure = collected.FirstOrDefault(diagnostic => diagnostic.Id == AnalyzerExceptionDiagnosticId);
            if (analyzerFailure != null)
            {
                return CheckResponse.Unsupported(FirstLine(analyzerFailure.GetMessage(culture)));
            }

            return new CheckResponse
            {
                Outcome = CheckResponse.CheckedOutcome,
                Diagnostics = ToWorkerDiagnostics(collected, culture),
                UncheckedCodes = UncheckedRuleCollector.Collect(withAnalyzers, load.Compilation.Options),
                GeneratorsNotRerun = load.GeneratorCount > 0,
            };
        }

        private static CheckResponse FindDeclarationChange(CheckRequest request, Dictionary<string, byte[]> changedBytes)
        {
            CSharpCommandLineArguments arguments = ResponseFileCompilationLoader.ParseArguments(request.ProjectRoot, request.ResponseFilePath);
            string argumentError = ResponseFileCompilationLoader.FirstArgumentError(arguments);
            if (argumentError != null)
            {
                return CheckResponse.Unsupported("response file: " + argumentError);
            }

            List<string> changedDeclarations = new List<string>();
            foreach (string changed in request.ChangedFiles)
            {
                string baseline;
                if (request.BaselineFingerprints == null || !request.BaselineFingerprints.TryGetValue(changed, out baseline))
                {
                    return CheckResponse.DeclarationChanged("no baseline fingerprint for " + changed);
                }

                byte[] bytes = File.ReadAllBytes(changed);
                changedBytes[Path.GetFullPath(changed)] = bytes;
                SyntaxTree tree = ResponseFileCompilationLoader.ParseSource(changed, bytes, arguments);
                // Why the loop goes on past the first changed declaration: the reason names every edit
                // that costs the full compile, and naming one hid the rest until the next run.
                if (!string.Equals(DeclarationFingerprint.Compute(tree), baseline, StringComparison.Ordinal))
                {
                    changedDeclarations.Add(changed);
                }
            }

            if (changedDeclarations.Count > 0)
            {
                return CheckResponse.DeclarationChanged(string.Join(", ", changedDeclarations));
            }

            return null;
        }

        private static ImmutableHashSet<string> CollectRelayIds(ImmutableArray<DiagnosticAnalyzer> analyzers)
        {
            return analyzers
                .OfType<DiagnosticSuppressor>()
                .SelectMany(suppressor => suppressor.SupportedSuppressions)
                .Select(suppression => suppression.SuppressedDiagnosticId)
                .Where(id => id.StartsWith("CS", StringComparison.Ordinal))
                .ToImmutableHashSet(StringComparer.Ordinal);
        }

        private static IEnumerable<Diagnostic> CheckTree(CompilationWithAnalyzers withAnalyzers, SyntaxTree tree, ImmutableHashSet<string> relayIds)
        {
            SemanticModel model = withAnalyzers.Compilation.GetSemanticModel(tree);
            // The relay reports these again with the suppressors applied, so keeping the originals
            // would report them twice.
            IEnumerable<Diagnostic> compilerDiagnostics = model.GetDiagnostics(null, CancellationToken.None)
                .Where(diagnostic => !relayIds.Contains(diagnostic.Id));
            AnalysisResult treeResult = withAnalyzers.GetAnalysisResultAsync(tree, CancellationToken.None).Result;
            AnalysisResult modelResult = withAnalyzers.GetAnalysisResultAsync(model, null, CancellationToken.None).Result;
            return compilerDiagnostics
                .Concat(treeResult.GetAllDiagnostics())
                .Concat(modelResult.GetAllDiagnostics())
                .ToList();
        }

        private static string FirstLine(string text)
        {
            int lineEnd = text.IndexOfAny(new char[] { '\r', '\n' });
            return lineEnd < 0 ? text : text.Substring(0, lineEnd);
        }

        private static List<WorkerDiagnostic> ToWorkerDiagnostics(IEnumerable<Diagnostic> diagnostics, CultureInfo culture)
        {
            // Matches what csc prints: errors and warnings in source, each location and code once.
            return diagnostics
                .Where(diagnostic => !diagnostic.IsSuppressed && diagnostic.Location.IsInSource)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error || diagnostic.Severity == DiagnosticSeverity.Warning)
                .Select(diagnostic => ToWorkerDiagnostic(diagnostic, culture))
                .DistinctBy(diagnostic => (diagnostic.File, diagnostic.Line, diagnostic.Column, diagnostic.Code))
                .ToList();
        }

        private static WorkerDiagnostic ToWorkerDiagnostic(Diagnostic diagnostic, CultureInfo culture)
        {
            FileLinePositionSpan span = diagnostic.Location.GetMappedLineSpan();
            return new WorkerDiagnostic
            {
                File = Path.GetFullPath(span.Path),
                Line = span.StartLinePosition.Line + 1,
                Column = span.StartLinePosition.Character + 1,
                Severity = diagnostic.Severity == DiagnosticSeverity.Error ? WorkerDiagnostic.ErrorSeverity : WorkerDiagnostic.WarningSeverity,
                Code = diagnostic.Id,
                Message = diagnostic.GetMessage(culture),
            };
        }
    }
}
