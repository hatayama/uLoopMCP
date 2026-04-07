using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor.Compilation;
using Debug = UnityEngine.Debug;

namespace io.github.hatayama.uLoopMCP
{
    internal sealed class CompiledAssemblyBuilder : ICompiledAssemblyBuilder
    {
        private static int _compileCounter;

        private readonly ExternalCompilerPathResolutionService _externalCompilerPathResolver;
        private readonly DynamicReferenceSetBuilderService _referenceSetBuilder;
        private readonly DynamicCompilationBackend _compilationBackend;

        public CompiledAssemblyBuilder(
            ExternalCompilerPathResolutionService externalCompilerPathResolver,
            DynamicReferenceSetBuilderService referenceSetBuilder,
            DynamicCompilationBackend compilationBackend)
        {
            _externalCompilerPathResolver = externalCompilerPathResolver;
            _referenceSetBuilder = referenceSetBuilder;
            _compilationBackend = compilationBackend;
        }

        public bool SupportsAutoPrewarm()
        {
            return _externalCompilerPathResolver.Resolve() != null;
        }

        public async Task<CompiledAssemblyBuildResult> BuildAsync(
            DynamicCompilationPlan plan,
            CancellationToken ct = default)
        {
            Debug.Assert(plan != null, "plan must not be null");

            ct.ThrowIfCancellationRequested();

            ExternalCompilerPaths externalCompilerPaths = _externalCompilerPathResolver.Resolve();
            string tempDirectoryPath = Path.Combine("Temp", "uLoopMCPCompilation");
            string uniqueName = $"{plan.ClassName}_{Interlocked.Increment(ref _compileCounter)}";
            string sourcePath = Path.Combine(tempDirectoryPath, $"{uniqueName}.cs");
            string dllPath = Path.Combine(tempDirectoryPath, $"{uniqueName}.dll");
            bool canDeleteTempFiles = true;
            double referenceResolutionMilliseconds = 0;
            double buildMilliseconds = 0;
            int buildCount = 0;

            Directory.CreateDirectory(tempDirectoryPath);

            try
            {
                string wrappedCode = plan.PreparedCode.PreparedSource;
                bool isScriptMode = plan.PreparedCode.IsScriptMode;
                string originalWrappedCode = wrappedCode;
                bool preUsingAdded = false;
                PreUsingResult preUsingResult = null;

                Stopwatch initialReferenceResolutionStopwatch = Stopwatch.StartNew();
                List<string> initialReferences = BuildInitialReferences(
                    plan,
                    externalCompilerPaths,
                    ref wrappedCode,
                    ref preUsingAdded,
                    ref preUsingResult);
                initialReferenceResolutionStopwatch.Stop();
                referenceResolutionMilliseconds += initialReferenceResolutionStopwatch.Elapsed.TotalMilliseconds;

                async Task<CompilerMessage[]> BuildFunc(
                    string resolvedSourcePath,
                    string resolvedDllPath,
                    List<string> resolvedReferences,
                    CancellationToken cancellationToken)
                {
                    Stopwatch buildStopwatch = Stopwatch.StartNew();
                    CompilerMessage[] compilerMessages = await _compilationBackend.CompileAsync(
                        resolvedSourcePath,
                        resolvedDllPath,
                        resolvedReferences,
                        externalCompilerPaths,
                        cancellationToken,
                        () => canDeleteTempFiles = false,
                        () => canDeleteTempFiles = true,
                        () => buildCount++);
                    buildStopwatch.Stop();
                    buildMilliseconds += buildStopwatch.Elapsed.TotalMilliseconds;
                    return compilerMessages;
                }

                AutoUsingResolver resolver = new AutoUsingResolver();
                AutoUsingResult autoResult = await resolver.ResolveAsync(
                    sourcePath,
                    dllPath,
                    wrappedCode,
                    initialReferences,
                    BuildFunc,
                    ct);
                referenceResolutionMilliseconds += autoResult.ReferenceResolutionMilliseconds;

                wrappedCode = autoResult.UpdatedSource;
                CompilerDiagnostics diagnostics = CompilerDiagnostics.FromMessages(autoResult.Messages);

                bool preUsingRolledBack = false;
                if (diagnostics.Errors.Count > 0 && preUsingAdded && diagnostics.HasAmbiguityErrors)
                {
                    Stopwatch rollbackReferenceResolutionStopwatch = Stopwatch.StartNew();
                    List<string> rollbackReferences = _referenceSetBuilder.BuildReferenceSet(
                        plan.OriginalRequest.AdditionalReferences,
                        null,
                        externalCompilerPaths);
                    rollbackReferenceResolutionStopwatch.Stop();
                    referenceResolutionMilliseconds += rollbackReferenceResolutionStopwatch.Elapsed.TotalMilliseconds;

                    AutoUsingResult rollbackResult = await resolver.ResolveAsync(
                        sourcePath,
                        dllPath,
                        originalWrappedCode,
                        rollbackReferences,
                        BuildFunc,
                        ct);
                    referenceResolutionMilliseconds += rollbackResult.ReferenceResolutionMilliseconds;

                    CompilerDiagnostics rollbackDiagnostics = CompilerDiagnostics.FromMessages(rollbackResult.Messages);
                    if (rollbackDiagnostics.Errors.Count < diagnostics.Errors.Count)
                    {
                        wrappedCode = rollbackResult.UpdatedSource;
                        diagnostics = rollbackDiagnostics;
                        autoResult = rollbackResult;
                        preUsingRolledBack = true;
                    }
                }

                List<string> autoInjectedNamespaces = MergeAutoInjectedNamespaces(
                    preUsingRolledBack,
                    preUsingResult,
                    autoResult);

                byte[] assemblyBytes = null;
                if (diagnostics.Errors.Count == 0)
                {
                    assemblyBytes = File.ReadAllBytes(dllPath);
                }

                return new CompiledAssemblyBuildResult(
                    wrappedCode,
                    diagnostics,
                    autoResult.AmbiguousTypeCandidates,
                    autoInjectedNamespaces,
                    assemblyBytes,
                    referenceResolutionMilliseconds,
                    buildMilliseconds,
                    buildCount);
            }
            finally
            {
                if (canDeleteTempFiles)
                {
                    File.Delete(sourcePath);
                    File.Delete(dllPath);
                    File.Delete(Path.ChangeExtension(dllPath, ".pdb"));
                }
            }
        }

        private List<string> BuildInitialReferences(
            DynamicCompilationPlan plan,
            ExternalCompilerPaths externalCompilerPaths,
            ref string wrappedCode,
            ref bool preUsingAdded,
            ref PreUsingResult preUsingResult)
        {
            if (!plan.PreparedCode.IsScriptMode)
            {
                return _referenceSetBuilder.BuildReferenceSet(
                    plan.OriginalRequest.AdditionalReferences,
                    null,
                    externalCompilerPaths);
            }

            preUsingResult = PreUsingResolver.Resolve(wrappedCode, AssemblyTypeIndex.Instance);
            preUsingAdded = !ReferenceEquals(preUsingResult.UpdatedSource, wrappedCode);
            wrappedCode = preUsingResult.UpdatedSource;
            return _referenceSetBuilder.BuildReferenceSet(
                plan.OriginalRequest.AdditionalReferences,
                preUsingResult.AddedAssemblyReferences,
                externalCompilerPaths);
        }

        private static List<string> MergeAutoInjectedNamespaces(
            bool preUsingRolledBack,
            PreUsingResult preUsingResult,
            AutoUsingResult autoResult)
        {
            List<string> mergedNamespaces = new List<string>();

            if (!preUsingRolledBack && preUsingResult != null && preUsingResult.AddedNamespaces.Count > 0)
            {
                foreach (string namespaceName in preUsingResult.AddedNamespaces)
                {
                    mergedNamespaces.Add(namespaceName);
                }
            }

            foreach (string namespaceName in autoResult.AddedNamespaces)
            {
                if (!mergedNamespaces.Contains(namespaceName))
                {
                    mergedNamespaces.Add(namespaceName);
                }
            }

            return mergedNamespaces;
        }
    }
}
