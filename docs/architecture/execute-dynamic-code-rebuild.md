# execute-dynamic-code rebuild

This document describes the rebuilt `execute-dynamic-code` pipeline after the layering refactor.

## Layered overview

```mermaid
flowchart TD
    subgraph Entry["Entry layer"]
        Tool["ExecuteDynamicCodeTool"]
        Server["McpServerController"]
    end

    subgraph UseCase["UseCase layer"]
        ExecuteUseCase["IExecuteDynamicCodeUseCase / ExecuteDynamicCodeUseCase"]
        PrewarmUseCase["IPrewarmDynamicCodeUseCase / PrewarmDynamicCodeUseCase"]
    end

    subgraph Infrastructure["Infrastructure layer"]
        RuntimeContract["IDynamicCodeExecutionRuntime"]
        RuntimeFacade["DynamicCodeExecutionFacade"]
        Provider["RegistryDynamicCodeExecutorFactory"]
        ExecutorContract["IDynamicCodeExecutor"]
        Executor["DynamicCodeExecutor"]
        CompilerContract["IDynamicCompilationService"]
        Compiler["DynamicCodeCompiler"]
        Runner["CommandRunner"]
        EntryResolver["CompiledCommandEntryPointResolver"]
        SourcePrepSvc["DynamicCodeSourcePreparationService"]
        SourcePrep["DynamicCodeSourcePreparer"]
        RefSvc["DynamicReferenceSetBuilderService"]
        RefBuilder["DynamicReferenceSetBuilder"]
        AutoUsing["PreUsingResolver / AutoUsingResolver / AssemblyTypeIndex"]
        Diagnostics["CompilerDiagnostics"]
        LoadSvc["CompiledAssemblyLoadService"]
        Loader["CompiledAssemblyLoader"]
        Backend["DynamicCompilationBackend"]
        PathSvc["ExternalCompilerPathResolutionService"]
        PathResolver["ExternalCompilerPathResolver"]
        Roslyn["RoslynCompilerBackend"]
        Worker["SharedRoslynCompilerWorkerHost"]
        Fallback["AssemblyBuilderFallbackCompilerBackend"]
        Cache["CompilationCacheManager"]
        Timing["DynamicCompilationTimingFormatter"]
    end

    Tool --> ExecuteUseCase
    Server --> PrewarmUseCase

    ExecuteUseCase --> RuntimeContract
    PrewarmUseCase --> RuntimeContract
    RuntimeContract --> RuntimeFacade

    RuntimeFacade --> PathSvc
    RuntimeFacade --> Provider
    Provider --> ExecutorContract
    ExecutorContract --> Executor
    Executor --> CompilerContract
    CompilerContract --> Compiler
    Executor --> Runner
    Executor --> SourcePrepSvc
    Runner --> EntryResolver

    Compiler --> SourcePrepSvc
    Compiler --> RefSvc
    Compiler --> Diagnostics
    Compiler --> LoadSvc
    Compiler --> Backend
    Compiler --> Cache
    Compiler --> Timing

    SourcePrepSvc --> SourcePrep
    RefSvc --> RefBuilder
    RefBuilder --> AutoUsing
    LoadSvc --> Loader
    Backend --> PathSvc
    Backend --> Roslyn
    Backend --> Fallback
    PathSvc --> PathResolver
    Roslyn --> Worker
```

## Composition graph

```mermaid
flowchart TD
    Services["DynamicCodeServices"]
    SourcePrepSvc["DynamicCodeSourcePreparationService"]
    PathSvc["ExternalCompilerPathResolutionService"]
    RefSvc["DynamicReferenceSetBuilderService"]
    LoadSvc["CompiledAssemblyLoadService"]
    Backend["DynamicCompilationBackend"]
    EntryResolver["CompiledCommandEntryPointResolver"]
    Provider["RegistryDynamicCodeExecutorFactory"]
    RuntimeFacade["DynamicCodeExecutionFacade"]
    ExecuteUseCase["ExecuteDynamicCodeUseCase"]
    PrewarmUseCase["PrewarmDynamicCodeUseCase"]

    Services --> SourcePrepSvc
    Services --> PathSvc
    Services --> RefSvc
    Services --> LoadSvc
    Services --> Backend
    Services --> EntryResolver
    Services --> Provider
    Services --> RuntimeFacade
    Services --> ExecuteUseCase
    Services --> PrewarmUseCase

    Provider --> SourcePrepSvc
    Provider --> EntryResolver
    RuntimeFacade --> PathSvc
    RuntimeFacade --> Provider
    ExecuteUseCase --> RuntimeFacade
    PrewarmUseCase --> RuntimeFacade
```

## Reading guide

1. Start with `Entry layer`.
   - `ExecuteDynamicCodeTool` only delegates the tool workflow.
   - `McpServerController` only requests warm-up after server start and recovery.
2. Move to `UseCase layer`.
   - `ExecuteDynamicCodeUseCase` owns the user-facing workflow for execute-dynamic-code.
   - `PrewarmDynamicCodeUseCase` owns the warm-up workflow.
3. Only then read `Infrastructure layer`.
   - `DynamicCodeExecutionFacade` is the runtime gateway that hides executor reuse and provider wiring.
   - `DynamicCodeCompiler` and its collaborators perform the heavy compile/execute work.
4. Read `Composition graph` last.
   - `DynamicCodeServices` is the only place that is expected to know many concrete classes at once.
   - If a concrete-to-concrete edge only appears there, it is a wiring edge rather than a runtime dependency.

## Layer responsibilities

- `Entry layer`
  - Translate external calls into use-case invocations.
  - Avoid business workflow logic.
  - Avoid reaching into executor or compiler wiring directly.

- `UseCase layer`
  - Own temporal cohesion.
  - Decide the workflow order for the feature.
  - Keep user-facing retry rules such as the missing-`return` retry in one place.
  - Depend on runtime contracts instead of concrete infrastructure types.

- `Infrastructure layer`
  - Own the mechanics of execution, compilation, loading, caching, path discovery, and worker lifecycle.
  - Keep low-level concerns isolated behind contracts and focused service classes.

## Class responsibilities

- `ExecuteDynamicCodeTool`
  - Thin entry point for the MCP/CLI tool.
  - Delegates the full workflow to `IExecuteDynamicCodeUseCase`.

- `ExecuteDynamicCodeUseCase`
  - Resolves the current security level.
  - Converts parameters into the runtime request.
  - Performs the missing-`return` retry.
  - Shapes `ExecutionResult` into `ExecuteDynamicCodeResponse`.

- `PrewarmDynamicCodeUseCase`
  - Owns the single-flight warm-up flow.
  - Reuses the same runtime contract as the real execution path.

- `IDynamicCodeExecutionRuntime`
  - Contract between use cases and runtime infrastructure.
  - Keeps use cases from depending on factory and executor wiring directly.

- `DynamicCodeExecutionFacade`
  - Reuses executors per security level.
  - Checks whether the external Roslyn path is available for warm-up.
  - Delegates executor creation to the provider.

- `RegistryDynamicCodeExecutorFactory`
  - Builds `DynamicCodeExecutor` and `CommandRunner` from registered compiler services.
  - Lives in the composition graph and runtime infrastructure, not in the entry layer.

- `DynamicCodeExecutor`
  - Bridges compilation and execution.
  - Merges timing information.
  - Converts hoisted literals into execution parameters.

- `DynamicCodeCompiler`
  - Orchestrates cache lookup, source security, reference resolution, compilation backend selection, and assembly load.

- `DynamicCodeSourcePreparationService` / `DynamicCodeSourcePreparer`
  - Normalize snippets into wrapper code.
  - Handle top-level mode, return completion, and literal hoisting.

- `DynamicReferenceSetBuilderService` / `DynamicReferenceSetBuilder`
  - Build the minimal assembly reference set.
  - Encapsulate pre-using, auto-using, and assembly candidate logic.

- `DynamicCompilationBackend`
  - Chooses between the Roslyn path and the AssemblyBuilder fallback path.

- `RoslynCompilerBackend` / `SharedRoslynCompilerWorkerHost`
  - Provide the fast path with the shared external worker.

- `CompiledAssemblyLoadService` / `CompiledAssemblyLoader`
  - Keep metadata validation, assembly loading, and IL validation together.

- `CommandRunner` / `CompiledCommandEntryPointResolver`
  - Execute the compiled wrapper method while hiding reflection-heavy lookup.

## Design intent

- Make the architecture readable as `Entry -> UseCase -> Infrastructure`.
- Keep the runtime dependency chain narrower than the composition graph.
- Allow the composition root to know concrete classes, while runtime layers depend on contracts or use cases.
- Keep the async-only contracts honest.
- Keep performance work in infrastructure without leaking that complexity into the entry layer.
