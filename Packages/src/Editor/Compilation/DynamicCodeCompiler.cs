using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Dynamic compilation service for execute-dynamic-code.
    /// Keeps the orchestration flow in one place and delegates low-level compiler concerns to helpers.
    /// </summary>
    public sealed class DynamicCodeCompiler : IDynamicCompilationService, IDisposable
    {
        private readonly DynamicCodeSecurityLevel _securityLevel;
        private readonly IDynamicCompilationPlanner _planner;
        private readonly ICompiledAssemblyBuilder _assemblyBuilder;
        private readonly ICompiledAssemblyLoader _assemblyLoader;
        private readonly CompilationCacheManager _cacheManager = new();
        private bool _disposed;

        internal int LastBuildCount { get; private set; }

        public DynamicCodeCompiler(DynamicCodeSecurityLevel securityLevel)
            : this(
                securityLevel,
                DynamicCodeServices.CompilationPlanner,
                DynamicCodeServices.AssemblyBuilder,
                DynamicCodeServices.AssemblyLoadService)
        {
        }

        internal DynamicCodeCompiler(
            DynamicCodeSecurityLevel securityLevel,
            IDynamicCompilationPlanner planner,
            ICompiledAssemblyBuilder assemblyBuilder,
            ICompiledAssemblyLoader assemblyLoader)
        {
            _securityLevel = securityLevel;
            _planner = planner ?? throw new ArgumentNullException(nameof(planner));
            _assemblyBuilder = assemblyBuilder ?? throw new ArgumentNullException(nameof(assemblyBuilder));
            _assemblyLoader = assemblyLoader ?? throw new ArgumentNullException(nameof(assemblyLoader));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _cacheManager.ClearCache();
            _disposed = true;
        }

        public async Task<CompilationResult> CompileAsync(CompilationRequest request, CancellationToken ct = default)
        {
            Debug.Assert(request != null, "request must not be null");
            Debug.Assert(!string.IsNullOrWhiteSpace(request.Code), "request.Code must not be empty");

            ct.ThrowIfCancellationRequested();
            LastBuildCount = 0;

            DynamicCompilationPlan plan = _planner.CreatePlan(request);

            CompilationResult cachedResult = TryGetCachedResult(plan);
            if (cachedResult != null)
            {
                return cachedResult;
            }

            CompilationResult sourceSecurityFailure = CreateSourceSecurityFailure(request.Code);
            if (sourceSecurityFailure != null)
            {
                return sourceSecurityFailure;
            }

            if (plan.PreparedCode.PreparedSource == null)
            {
                return CreateMixedModeFailureResult(request.Code, 0, 0);
            }

            CompiledAssemblyBuildResult buildResult = await _assemblyBuilder.BuildAsync(plan, ct);
            LastBuildCount = buildResult.BuildCount;

            if (buildResult.Diagnostics.Errors.Count > 0)
            {
                return new CompilationResult
                {
                    Success = false,
                    Errors = buildResult.Diagnostics.Errors,
                    Warnings = buildResult.Diagnostics.Warnings,
                    UpdatedCode = buildResult.UpdatedSource,
                    FailureReason = CompilationFailureReason.CompilationError,
                    AmbiguousTypeCandidates = buildResult.AmbiguousTypeCandidates,
                    AutoInjectedNamespaces = buildResult.AutoInjectedNamespaces,
                    Timings = DynamicCompilationTimingFormatter.CreateCompilationTimings(
                        buildResult.ReferenceResolutionMilliseconds,
                        buildResult.BuildMilliseconds,
                        0)
                };
            }

            CompiledAssemblyLoadResult assemblyLoadResult = _assemblyLoader.Load(
                _securityLevel,
                buildResult.AssemblyBytes);
            if (!assemblyLoadResult.Success)
            {
                return CreateAssemblySecurityFailure(
                    assemblyLoadResult.SecurityViolations,
                    buildResult.Diagnostics.Warnings,
                    buildResult.UpdatedSource,
                    buildResult.AmbiguousTypeCandidates,
                    buildResult.AutoInjectedNamespaces,
                    buildResult.ReferenceResolutionMilliseconds,
                    buildResult.BuildMilliseconds,
                    assemblyLoadResult.AssemblyLoadMilliseconds);
            }

            CompilationResult result = new CompilationResult
            {
                Success = true,
                CompiledAssembly = assemblyLoadResult.CompiledAssembly,
                Warnings = buildResult.Diagnostics.Warnings,
                UpdatedCode = buildResult.UpdatedSource,
                AmbiguousTypeCandidates = buildResult.AmbiguousTypeCandidates,
                AutoInjectedNamespaces = buildResult.AutoInjectedNamespaces,
                Timings = DynamicCompilationTimingFormatter.CreateCompilationTimings(
                    buildResult.ReferenceResolutionMilliseconds,
                    buildResult.BuildMilliseconds,
                    assemblyLoadResult.AssemblyLoadMilliseconds)
            };

            _cacheManager.CacheResultIfSuccessful(result, plan.NormalizedRequest);
            return result;
        }

        private CompilationResult TryGetCachedResult(
            DynamicCompilationPlan plan)
        {
            CompilationResult cachedResult = _cacheManager.CheckCache(plan.NormalizedRequest);
            if (cachedResult == null)
            {
                return null;
            }

            cachedResult.UpdatedCode = plan.PreparedCode.PreparedSource;
            return cachedResult;
        }

        private CompilationResult CreateSourceSecurityFailure(string originalCode)
        {
            if (_securityLevel != DynamicCodeSecurityLevel.Restricted)
            {
                return null;
            }

            SecurityValidationResult sourceSecurityResult = SourceSecurityScanner.Scan(originalCode);
            if (sourceSecurityResult.IsValid)
            {
                return null;
            }

            return new CompilationResult
            {
                Success = false,
                HasSecurityViolations = true,
                SecurityViolations = sourceSecurityResult.Violations,
                UpdatedCode = originalCode,
                FailureReason = CompilationFailureReason.SecurityViolation,
                Timings = DynamicCompilationTimingFormatter.CreateCompilationTimings(0, 0, 0)
            };
        }

        private static CompilationResult CreateMixedModeFailureResult(
            string originalCode,
            double referenceResolutionMilliseconds,
            double buildMilliseconds)
        {
            return new CompilationResult
            {
                Success = false,
                Errors = new List<CompilationError>
                {
                    new CompilationError
                    {
                        Message = "Mixed top-level statements and type declarations are not supported. Use a full class definition instead.",
                        ErrorCode = "MIXED_MODE",
                        Line = 0,
                        Column = 0
                    }
                },
                FailureReason = CompilationFailureReason.CompilationError,
                UpdatedCode = originalCode,
                Timings = DynamicCompilationTimingFormatter.CreateCompilationTimings(
                    referenceResolutionMilliseconds,
                    buildMilliseconds,
                    0)
            };
        }

        private static CompilationResult CreateAssemblySecurityFailure(
            List<SecurityViolation> securityViolations,
            List<string> warnings,
            string updatedCode,
            Dictionary<string, List<string>> ambiguousTypeCandidates,
            List<string> autoInjectedNamespaces,
            double referenceResolutionMilliseconds,
            double buildMilliseconds,
            double assemblyLoadMilliseconds)
        {
            return new CompilationResult
            {
                Success = false,
                HasSecurityViolations = true,
                SecurityViolations = securityViolations,
                Warnings = warnings,
                UpdatedCode = updatedCode,
                FailureReason = CompilationFailureReason.SecurityViolation,
                AmbiguousTypeCandidates = ambiguousTypeCandidates,
                AutoInjectedNamespaces = autoInjectedNamespaces,
                Timings = DynamicCompilationTimingFormatter.CreateCompilationTimings(
                    referenceResolutionMilliseconds,
                    buildMilliseconds,
                    assemblyLoadMilliseconds)
            };
        }

    }
}
