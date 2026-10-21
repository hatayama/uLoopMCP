using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace UnityCliLoop.CompileCheckWorker
{
    /// <summary>
    /// Reports again, from an analyzer, the compiler diagnostics a DiagnosticSuppressor can
    /// suppress, so the suppressors of the assembly apply to them in a per-file analysis.
    /// </summary>
    /// <remarks>
    /// A per-file analysis does not hand compiler diagnostics to suppressors. Diagnostics an
    /// analyzer reports do get there. The relay uses a syntax tree action, not a semantic model
    /// action: without /doc a per-file analysis never completes a compilation unit, so semantic
    /// model actions never run. The IDE's internal compiler analyzer is not used because it
    /// reports nothing on Roslyn 4.10 and later.
    /// </remarks>
    internal sealed class SuppressibleCompilerDiagnosticRelay : DiagnosticAnalyzer
    {
        private readonly ImmutableHashSet<string> relayedIds;
        private readonly ImmutableArray<DiagnosticDescriptor> descriptors;

        public SuppressibleCompilerDiagnosticRelay(IEnumerable<string> relayedIds)
        {
            this.relayedIds = relayedIds.ToImmutableHashSet();
            descriptors = this.relayedIds
                .OrderBy(id => id, System.StringComparer.Ordinal)
                .Select(id => new DiagnosticDescriptor(id, id, id, "Compiler", DiagnosticSeverity.Warning, isEnabledByDefault: true))
                .ToImmutableArray();
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => descriptors;

        public override void Initialize(AnalysisContext context)
        {
            // csc reports compiler diagnostics in generated code too.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(startContext =>
            {
                Compilation compilation = startContext.Compilation;
                startContext.RegisterSyntaxTreeAction(treeContext => Relay(compilation, treeContext));
            });
        }

        private void Relay(Compilation compilation, SyntaxTreeAnalysisContext context)
        {
            SemanticModel model = compilation.GetSemanticModel(context.Tree);
            foreach (Diagnostic diagnostic in model.GetDiagnostics(null, context.CancellationToken))
            {
                if (relayedIds.Contains(diagnostic.Id))
                {
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
