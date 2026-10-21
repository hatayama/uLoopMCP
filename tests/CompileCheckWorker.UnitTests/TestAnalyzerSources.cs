namespace UnityCliLoop.CompileCheckWorker.UnitTests
{
    /// <summary>
    /// Analyzer sources the tests build into analyzer DLLs and pass to the worker with -analyzer.
    /// </summary>
    internal static class TestAnalyzerSources
    {
        // Suppresses CS0219 for variables whose name contains "suppressed".
        public const string SuppressorAssemblyName = "CompileCheckTestSuppressor";

        public const string Suppressor = @"
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnusedProbeSuppressor : DiagnosticSuppressor
{
    private static readonly SuppressionDescriptor Rule = new SuppressionDescriptor(""TSP001"", ""CS0219"", ""test suppression"");

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => ImmutableArray.Create(Rule);

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (Diagnostic diagnostic in context.ReportedDiagnostics)
        {
            SyntaxNode node = diagnostic.Location.SourceTree.GetRoot(context.CancellationToken).FindNode(diagnostic.Location.SourceSpan);
            if (node.ToString().Contains(""suppressed""))
            {
                context.ReportSuppression(Suppression.Create(Rule, diagnostic));
            }
        }
    }
}
";

        // Reports TSA001 on every local declaration whose text contains "flagged".
        public const string NodeAnalyzerAssemblyName = "CompileCheckTestNodeAnalyzer";

        public const string NodeAnalyzer = @"
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FlaggedLocalAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(""TSA001"", ""flagged"", ""flagged local"", ""Test"", DiagnosticSeverity.Warning, true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(nodeContext =>
        {
            if (nodeContext.Node.ToString().Contains(""flagged""))
            {
                nodeContext.ReportDiagnostic(Diagnostic.Create(Rule, nodeContext.Node.GetLocation()));
            }
        }, SyntaxKind.LocalDeclarationStatement);
    }
}
";

        // Registers a CompilationEnd action, which a per-file analysis never runs.
        public const string CompilationEndAnalyzerAssemblyName = "CompileCheckTestCompilationEndAnalyzer";

        public const string CompilationEndAnalyzer = @"
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WholeAssemblyAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(""TSA002"", ""whole"", ""whole assembly"", ""Test"", DiagnosticSeverity.Warning, true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(startContext =>
        {
            startContext.RegisterCompilationEndAction(endContext => { });
        });
    }
}
";

        // Registers a SymbolEnd action, which a per-file analysis cannot finish for a type declared
        // across several files.
        public const string SymbolEndAnalyzerAssemblyName = "CompileCheckTestSymbolEndAnalyzer";

        public const string SymbolEndAnalyzer = @"
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WholeTypeAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(""TSA003"", ""whole type"", ""whole type"", ""Test"", DiagnosticSeverity.Warning, true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolStartAction(startContext =>
        {
            startContext.RegisterSymbolEndAction(endContext => { });
        }, SymbolKind.NamedType);
    }
}
";

        // Throws on every local declaration that contains "boom".
        public const string ThrowingAnalyzerAssemblyName = "CompileCheckTestThrowingAnalyzer";

        public const string ThrowingAnalyzer = @"
using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ThrowingAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(""TSA006"", ""throws"", ""throws"", ""Test"", DiagnosticSeverity.Warning, true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(nodeContext =>
        {
            if (nodeContext.Node.ToString().Contains(""boom""))
            {
                throw new InvalidOperationException(""boom"");
            }
        }, SyntaxKind.LocalDeclarationStatement);
    }
}
";

        // Registers a semantic model action and a compilation action, which a per-file analysis never runs.
        public const string WholeFileAnalyzersAssemblyName = "CompileCheckTestWholeFileAnalyzers";

        public const string WholeFileAnalyzers = @"
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SemanticModelAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(""TSA004"", ""model"", ""model"", ""Test"", DiagnosticSeverity.Warning, true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSemanticModelAction(modelContext => { });
    }
}

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CompilationAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(""TSA005"", ""compilation"", ""compilation"", ""Test"", DiagnosticSeverity.Warning, true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(compilationContext => { });
    }
}
";

        // A source generator, which the check counts but never runs.
        public const string GeneratorAssemblyName = "CompileCheckTestGenerator";

        public const string Generator = @"
using Microsoft.CodeAnalysis;

[Generator]
public sealed class EmptyGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
    }

    public void Execute(GeneratorExecutionContext context)
    {
    }
}
";
    }
}
