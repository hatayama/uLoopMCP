using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Diagnostics.Telemetry;

namespace UnityCliLoop.CompileCheckWorker
{
    /// <summary>
    /// Lists the diagnostic IDs that a changed-files check cannot report but csc could, so the
    /// output can tell the caller what still needs a full compile.
    /// </summary>
    internal static class UncheckedRuleCollector
    {
        // The compiler reports these unused-member warnings only after it has seen the whole assembly.
        private static readonly string[] AssemblyWideCompilerCodes = new string[] { "CS0067", "CS0169", "CS0414", "CS0649" };

        internal static List<string> Collect(CompilationWithAnalyzers withAnalyzers, CompilationOptions options)
        {
            SortedSet<string> codes = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string id in AssemblyWideCompilerCodes)
            {
                ReportDiagnostic option;
                if (!options.SpecificDiagnosticOptions.TryGetValue(id, out option) || option != ReportDiagnostic.Suppress)
                {
                    codes.Add(id);
                }
            }

            foreach (DiagnosticAnalyzer analyzer in withAnalyzers.Analyzers)
            {
                if (analyzer is SuppressibleCompilerDiagnosticRelay || analyzer is DiagnosticSuppressor)
                {
                    continue;
                }

                if (!RunsOnlyOnTheWholeCompilation(withAnalyzers, analyzer))
                {
                    continue;
                }

                foreach (DiagnosticDescriptor descriptor in analyzer.SupportedDiagnostics)
                {
                    if (IsReportedAsErrorOrWarning(descriptor, options))
                    {
                        codes.Add(descriptor.Id);
                    }
                }
            }

            return codes.ToList();
        }

        private static bool RunsOnlyOnTheWholeCompilation(CompilationWithAnalyzers withAnalyzers, DiagnosticAnalyzer analyzer)
        {
            // Semantic model actions count too: a per-file analysis without /doc never runs them. So do
            // symbol start actions, for the symbol end actions they register: a partial type declared
            // across files is not finished by analyzing only the changed ones. Why symbol start rather
            // than symbol end: symbol end actions are registered inside symbol start, and the telemetry
            // does not count them, so an analyzer that uses one reports zero symbol end actions.
            AnalyzerTelemetryInfo info = withAnalyzers.GetAnalyzerTelemetryInfoAsync(analyzer, CancellationToken.None).Result;
            return info.CompilationActionsCount + info.CompilationEndActionsCount
                + info.SemanticModelActionsCount + info.SymbolStartActionsCount > 0;
        }

        private static bool IsReportedAsErrorOrWarning(DiagnosticDescriptor descriptor, CompilationOptions options)
        {
            ReportDiagnostic option;
            if (!options.SpecificDiagnosticOptions.TryGetValue(descriptor.Id, out option))
            {
                option = descriptor.IsEnabledByDefault ? ReportDiagnostic.Default : ReportDiagnostic.Suppress;
            }

            switch (option)
            {
                case ReportDiagnostic.Error:
                case ReportDiagnostic.Warn:
                    return true;
                case ReportDiagnostic.Default:
                    // A default-severity warning becomes an error under -warnaserror; both are reported.
                    return descriptor.DefaultSeverity == DiagnosticSeverity.Error
                        || descriptor.DefaultSeverity == DiagnosticSeverity.Warning;
                default:
                    return false;
            }
        }
    }
}
