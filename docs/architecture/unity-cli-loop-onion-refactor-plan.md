# UnityCLILoop C# Onion Architecture Refactor Plan

## Summary

- Work from `refactor/unity-cli-loop-onion-architecture`, created from `origin/v3-beta`.
- Treat the UnityCLILoop domain as the platform that safely hosts and executes Unity operation tools, not as the individual Unity operations themselves.
- Treat `Compile`, `GetLogs`, and `ExecuteDynamicCode` as first-party tool plugins that use the same contract, registration path, and execution path as third-party tools.
- Treat each tool implementation as an outer plugin in the Onion Architecture.
- Tool implementations depend on `UnityCLILoop.ToolContracts`, which is the inward contract defined by the platform.
- Keep the Figma diagram as the visual reference: https://www.figma.com/board/ThcM67cR9MdNRQpQgeVnvi?utm_source=codex&utm_content=edit_in_figjam&oai_id=&request_id=7ebbf16a-1ed2-4c82-9f17-9cf8201373d7

## Implemented Slice

- Added `UnityCLILoop.ToolContracts` as the public tool contract assembly.
- Moved the public extension API into `ToolContracts`:
  - `IUnityCliLoopTool`
  - `UnityCliLoopTool<TSchema, TResponse>`
  - `UnityCliLoopToolSchema`
  - `UnityCliLoopToolResponse`
  - `UnityCliLoopToolAttribute`
  - `UnityCliLoopSecuritySetting`
  - `UnityCliLoopToolParameterValidationException`
- Renamed the main editor assembly to `UnityCLILoop.Application`.
- Added onion boundary asmdefs for the intended final shape:
  - `UnityCLILoop.Domain`
  - `UnityCLILoop.Presentation`
  - `UnityCLILoop.Infrastructure`
  - `UnityCLILoop.CompositionRoot.Editor`
  - `UnityCLILoop.FirstPartyTools.Editor`
- Removed manual built-in tool registration from `UnityCliLoopToolRegistry`.
- Registered bundled tools and extension tools through the same `[UnityCliLoopTool]` attribute discovery path.
- Split `Assets/Editor/CustomCommandSamples` into `UnityCLILoop.CustomCommandSamples.Editor`, which references only `UnityCLILoop.ToolContracts`.
- Moved `get-version` out of the extension-facing tool registry and kept it as an internal bridge command for CLI readiness and diagnostics.
- Moved `get-tool-details` out of the extension-facing tool registry and kept it as an internal bridge command for CLI list/sync catalog access.
- Removed `focus-window` from the Unity-side tool registry because it is implemented as a native Go CLI command.
- Removed legacy MCP-era development tools from the runtime registry:
  - `ping`
  - `debug-sleep`
- Moved `control-play-mode` into `UnityCLILoop.FirstPartyTools.Editor` as the first bundled tool plugin that references only `UnityCLILoop.ToolContracts`.
- Moved `get-logs` into `UnityCLILoop.FirstPartyTools.Editor` as a bundled tool plugin that receives Unity Console access through a `ToolContracts` host-service contract.
- Moved `clear-console` into `UnityCLILoop.FirstPartyTools.Editor` as a bundled tool plugin that receives Unity Console mutation access through a `ToolContracts` host-service contract.
- Moved `compile` into `UnityCLILoop.FirstPartyTools.Editor` as a bundled tool plugin that receives Unity compilation access through a `ToolContracts` host-service contract.
- Moved `execute-dynamic-code` into `UnityCLILoop.FirstPartyTools.Editor` as a bundled tool plugin that receives dynamic-code execution access through a `ToolContracts` host-service contract.
- Moved `get-hierarchy` into `UnityCLILoop.FirstPartyTools.Editor` as a bundled tool plugin that receives hierarchy export access through a `ToolContracts` host-service contract.
- Moved `run-tests` into `UnityCLILoop.FirstPartyTools.Editor` as a bundled tool plugin that receives Unity Test Runner access through a `ToolContracts` host-service contract.
- Moved `find-game-objects` into `UnityCLILoop.FirstPartyTools.Editor` as a bundled tool plugin that receives GameObject search access through a `ToolContracts` host-service contract.
- Moved `screenshot` into `UnityCLILoop.FirstPartyTools.Editor` as a bundled tool plugin that receives Unity window and GameView capture access through a `ToolContracts` host-service contract.
- Moved `record-input` and `replay-input` into `UnityCLILoop.FirstPartyTools.Editor` as bundled tool plugins that receive input recording and replay access through `ToolContracts` host-service contracts.
- Moved `simulate-keyboard` and `simulate-mouse-input` into `UnityCLILoop.FirstPartyTools.Editor` as bundled tool plugins that receive input simulation access through `ToolContracts` host-service contracts.
- Moved `simulate-mouse-ui` into `UnityCLILoop.FirstPartyTools.Editor` as a bundled tool plugin that receives EventSystem mouse simulation access through a `ToolContracts` host-service contract.
- Moved `execute-dynamic-code` schema and response DTOs into `UnityCLILoop.ToolContracts` because both the bundled tool and the application-side execution pipeline shape those values.
- Moved concrete tool host-service wiring into `UnityCLILoop.CompositionRoot.Editor`; `UnityCliLoopToolRegistry` now asks an application-side provider for registered host services instead of constructing them directly.
- Moved concrete dynamic-code compilation service factory into `UnityCLILoop.Infrastructure`; `UnityCLILoop.CompositionRoot.Editor` still owns registration, while Infrastructure owns the factory that creates the compiler service.
- Moved Unity Console clear host-service implementation into `UnityCLILoop.Infrastructure`; `clear-console` receives the capability through `ToolContracts` and the composition root wires the concrete adapter.
- Moved project-root identity matching into `UnityCLILoop.Domain` as a platform safety rule; JSON-RPC request validation now delegates to the domain rule and only converts failures into tool parameter errors.
- Moved CLI version ordering into `UnityCLILoop.Domain` because dispatcher compatibility checks are pure platform rules with no Unity Editor, file-system, or protocol dependency.
- Moved compilation diagnostic message parsing into `UnityCLILoop.Domain` because it is a pure diagnostic normalization rule with no Unity Editor, file-system, or protocol dependency.
- Moved dynamic-code default namespace and class-name constants into `UnityCLILoop.Domain` because they are platform defaults shared by compilation and execution policies.
- Moved script-changes-while-playing policy values into `UnityCLILoop.Domain` because they are compile safety policy values interpreted by Application services.
- Moved dynamic-code security result values and the dangerous API catalog into `UnityCLILoop.Domain` because they are platform safety policy values shared by compilation and metadata validation.
- Moved source-level dynamic-code security scanning into `UnityCLILoop.Domain` because it is a pure platform safety policy over source text.
- Moved dynamic-code compilation service ports and their registry into `UnityCLILoop.Application` because Application owns the dynamic-code execution flow while Infrastructure supplies the concrete compiler factory.
- Moved dynamic-code compilation DTOs and compilation cache management into `UnityCLILoop.Application` because they belong to the application execution flow rather than a shared cross-layer bucket.
- Removed now-unused `uLoopMCP.Editor.Shared` references from `UnityCLILoop.Infrastructure` and `UnityCLILoop.CompositionRoot.Editor`.
- Moved preload metadata validation contracts and registry into `uLoopMCP.Editor.MetadataValidation` so the metadata validation module exposes its own facade instead of depending on `uLoopMCP.Editor.Shared`.
- Removed `uLoopMCP.Editor.Shared` as a production assembly after moving its remaining constants, logging, and domain-reload registry types into `UnityCLILoop.Application`.
- Removed stale references to the deleted shared assembly GUID from dev and editor test asmdefs.
- Added registry tests proving:
  - bundled tools are discovered through the attribute path.
  - `get-logs` is registered from `UnityCLILoop.FirstPartyTools.Editor`.
  - `compile` is registered from `UnityCLILoop.FirstPartyTools.Editor`.
  - `execute-dynamic-code` is registered from `UnityCLILoop.FirstPartyTools.Editor`.
  - `get-version` is not registered as an extension-facing tool.
  - `get-version` still executes as an internal bridge command.
  - `get-tool-details` is not registered as an extension-facing tool.
  - `get-tool-details` still returns the CLI catalog through the internal bridge path.
  - `focus-window` is not registered as a Unity-side tool.
  - legacy MCP-era development tools are not registered.
  - `hello-world` is registered as an extension tool.
  - `control-play-mode` is registered from `UnityCLILoop.FirstPartyTools.Editor`.
  - `control-play-mode` skill discovery still works after moving under `UnityCLILoop.FirstPartyTools.Editor`.
  - `get-logs` skill discovery still works after moving under `UnityCLILoop.FirstPartyTools.Editor`.
  - `clear-console` skill discovery still works after moving under `UnityCLILoop.FirstPartyTools.Editor`.
  - `compile` skill discovery still works after moving under `UnityCLILoop.FirstPartyTools.Editor`.
  - `execute-dynamic-code` skill discovery still works after moving under `UnityCLILoop.FirstPartyTools.Editor`.
  - `get-hierarchy` skill discovery still works after moving under `UnityCLILoop.FirstPartyTools.Editor`.
  - `run-tests` skill discovery still works after moving under `UnityCLILoop.FirstPartyTools.Editor`.
  - `find-game-objects` skill discovery still works after moving under `UnityCLILoop.FirstPartyTools.Editor`.
  - `screenshot` skill discovery still works after moving under `UnityCLILoop.FirstPartyTools.Editor`.
  - `record-input` and `replay-input` skill discovery still works after moving under `UnityCLILoop.FirstPartyTools.Editor`.
  - `simulate-keyboard` and `simulate-mouse-input` skill discovery still works after moving under `UnityCLILoop.FirstPartyTools.Editor`.
  - `simulate-mouse-ui` skill discovery still works after moving under `UnityCLILoop.FirstPartyTools.Editor`.
  - `get-hierarchy` is registered from `UnityCLILoop.FirstPartyTools.Editor`.
  - hierarchy host-service contracts compile under `UnityCLILoop.ToolContracts`.
  - hierarchy host-service implementation compiles under `UnityCLILoop.Application`.
  - `run-tests` is registered from `UnityCLILoop.FirstPartyTools.Editor`.
  - test execution host-service contracts compile under `UnityCLILoop.ToolContracts`.
  - test execution host-service implementation compiles under `UnityCLILoop.Application`.
  - `find-game-objects` is registered from `UnityCLILoop.FirstPartyTools.Editor`.
  - GameObject search host-service contracts compile under `UnityCLILoop.ToolContracts`.
  - GameObject search host-service implementation compiles under `UnityCLILoop.Application`.
  - `screenshot` is registered from `UnityCLILoop.FirstPartyTools.Editor`.
  - screenshot host-service contracts compile under `UnityCLILoop.ToolContracts`.
  - screenshot host-service implementation compiles under `UnityCLILoop.Application`.
  - `record-input` and `replay-input` are registered from `UnityCLILoop.FirstPartyTools.Editor`.
  - input recording host-service contracts compile under `UnityCLILoop.ToolContracts`.
  - input recording host-service implementations compile under `UnityCLILoop.Application`.
  - `simulate-keyboard` and `simulate-mouse-input` are registered from `UnityCLILoop.FirstPartyTools.Editor`.
  - input simulation host-service contracts compile under `UnityCLILoop.ToolContracts`.
  - input simulation host-service implementations compile under `UnityCLILoop.Application`.
  - `simulate-mouse-ui` is registered from `UnityCLILoop.FirstPartyTools.Editor`.
  - concrete tool host services compile under `UnityCLILoop.CompositionRoot.Editor`.
  - `UnityCliLoopToolRegistry` does not directly construct concrete host services.
  - concrete dynamic-code compiler factory compiles under `UnityCLILoop.Infrastructure`.
  - Unity Console clear host service compiles under `UnityCLILoop.Infrastructure`.
  - project-root identity safety policy compiles under `UnityCLILoop.Domain`.
  - CLI version ordering compiles under `UnityCLILoop.Domain`.
  - compilation diagnostic message parsing compiles under `UnityCLILoop.Domain`.
  - dynamic-code platform defaults compile under `UnityCLILoop.Domain`.
  - script-changes-while-playing policy values compile under `UnityCLILoop.Domain`.
  - dynamic-code security result values compile under `UnityCLILoop.Domain`.
  - dynamic-code dangerous API policy compiles under `UnityCLILoop.Domain`.
  - source-level dynamic-code security scanning compiles under `UnityCLILoop.Domain`.
  - dynamic-code compilation service ports and registry compile under `UnityCLILoop.Application`.
  - dynamic-code compilation DTOs and cache management compile under `UnityCLILoop.Application`.
  - `UnityCLILoop.Infrastructure` and `UnityCLILoop.CompositionRoot.Editor` no longer reference `uLoopMCP.Editor.Shared`.
  - preload metadata validation contracts and registry compile under `uLoopMCP.Editor.MetadataValidation`.
  - `uLoopMCP.Editor.MetadataValidation` no longer references `uLoopMCP.Editor.Shared`.
  - support constants, structured logging, and domain-reload registry types compile under `UnityCLILoop.Application`.
  - production asmdefs no longer reference `uLoopMCP.Editor.Shared`.
  - project asmdefs no longer reference the deleted shared assembly GUID.
  - the sample extension asmdef references only `UnityCLILoop.ToolContracts`.
  - the sample `hello-world` extension executes through the same typed contract path as bundled tools.
  - `UnityCLILoop.FirstPartyTools.Editor` references only `UnityCLILoop.ToolContracts`.
  - settings/setup UI code no longer reaches CLI setup internals directly and goes through `CliSetupApplicationFacade`.
  - settings/setup UI code no longer reaches skill setup internals directly and goes through `SkillSetupApplicationFacade`.
  - settings-window UI code no longer reaches tool settings, registry, or security settings internals directly and goes through `ToolSettingsApplicationFacade`.
  - settings-window UI source uses `UnityCliLoopSettingsWindow` naming instead of the legacy MCP settings-window name.
  - settings, setup, and server editor UI files now compile under `UnityCLILoop.Presentation`.
  - recordings editor UI now compiles under `UnityCLILoop.Presentation` and reaches record/replay services through `RecordingsApplicationFacade`.
  - unused legacy MCP communication-log code and its transient settings have been removed.
  - shared editor UI constants now use `UnityCliLoopUIConstants` naming instead of the legacy MCP name.
  - editor settings storage now uses `UnityCliLoopEditorSettings` naming instead of the legacy MCP name.
  - editor domain reload state provider now uses `UnityCliLoopEditorDomainReloadStateProvider` naming instead of the legacy MCP name.
  - project path resolution now uses `UnityCliLoopPathResolver` naming instead of the legacy MCP name.
  - package version, tool security, console log filter, shared constants, and server config now use `UnityCliLoop*` naming instead of legacy MCP names.
  - project IPC server lifecycle types now use `UnityCliLoop*` naming instead of legacy MCP names.
  - presentation USS, UXML, and C# style class names now use `unity-cli-loop-*` prefixes instead of the legacy `mcp-*` prefix.
  - public tool source files now live under `Packages/src/Editor/Api/Tools` instead of the legacy `Packages/src/Editor/Api/McpTools` folder.
  - pure platform values `DynamicCodeSecurityLevel`, `ToolDisabledException`, `ValidationResult`, and `ServiceResult<T>` now compile under `UnityCLILoop.Domain`.
  - dynamic-code compiler factory registration now compiles under `UnityCLILoop.CompositionRoot.Editor`.
- Added asmdef dependency tests proving:
  - `Domain` and `ToolContracts` have no project assembly references.
  - `Application` references the inward contracts and does not reference outer onion layers.
  - `Presentation` and `Infrastructure` are sibling outer layers and do not reference each other.
  - `CompositionRoot.Editor` is the assembly allowed to reference all onion assemblies.

## Key Changes

- Create `refactor/unity-cli-loop-onion-architecture` from `origin/v3-beta`.
- Restructure asmdef boundaries around:
  - `UnityCLILoop.Domain`
  - `UnityCLILoop.ToolContracts`
  - `UnityCLILoop.Application`
  - `UnityCLILoop.Presentation`
  - `UnityCLILoop.Infrastructure`
  - `UnityCLILoop.CompositionRoot.Editor`
  - `UnityCLILoop.FirstPartyTools.Editor`
- Keep `Domain` focused on platform rules.
- Keep `Application` focused on tool hosting, registration, catalog, and execution policy.
- Keep `Presentation` and `Infrastructure` as sibling outer layers that do not reference each other.
- Keep `CompositionRoot.Editor` as the only assembly that references all layers and first-party tools for DI wiring and registration.
- Keep `FirstPartyTools.Editor` as an outer plugin assembly that references only `ToolContracts`.

## Remaining Migration Steps

- Continue moving cohesive platform rules into `UnityCLILoop.Domain` when they have no Unity or file-system dependency.
- Move hosting/catalog/execution policies into `UnityCLILoop.Application`.
- Move remaining editor UI into `UnityCLILoop.Presentation` after adding the necessary Application facades.
- Move Unity Editor, IPC, file system, dynamic compilation, and protocol adapters into `UnityCLILoop.Infrastructure`.
- Continue moving bundled tool implementations into `UnityCLILoop.FirstPartyTools.Editor` once their dependencies are either internal to that plugin or exposed through stable contracts.
- Continue splitting internal bridge commands from public tool registration when more CLI-only commands are identified.
- Continue moving startup/DI wiring into `UnityCLILoop.CompositionRoot.Editor` as additional concrete service composition points are identified.
- Extend asmdef dependency tests after each physical move, so the dependency direction is enforced by Unity assemblies instead of documentation alone.

## Public Contracts

- Replace old public SDK names with UnityCLILoop names:
  - `[McpTool]` to `[UnityCliLoopTool]`
  - `McpToolAttribute` to `UnityCliLoopToolAttribute`
  - `IUnityTool` to `IUnityCliLoopTool`
  - `AbstractUnityTool<TSchema, TResponse>` to `UnityCliLoopTool<TSchema, TResponse>`
  - `BaseToolSchema` to `UnityCliLoopToolSchema`
  - `BaseToolResponse` to `UnityCliLoopToolResponse`
  - `CustomToolManager` to `UnityCliLoopToolRegistrar`
- Remove `MCP` naming from public SDK and contract types.
- Keep MCP naming only inside infrastructure protocol implementation if it is still needed there.

## Test Plan

- Add or update asmdef dependency tests to verify:
  - `Domain` does not reference project asmdefs.
  - `ToolContracts` does not reference implementation asmdefs.
  - `Application` references only `Domain` and `ToolContracts`.
  - `Presentation` and `Infrastructure` do not reference each other.
  - `FirstPartyTools.Editor` references only `ToolContracts`.
  - `CompositionRoot.Editor` is the only assembly that can reference all layers and first-party tools.
- Add contract tests for `[UnityCliLoopTool]` and `UnityCliLoopTool<TSchema, TResponse>` registration and execution.
- Add first-party parity coverage showing `GetLogs` and a Hello World style tool use the same catalog and execution path.
- Run `unicli exec Compile --json` after C# changes and confirm there are no errors or warnings.

## Assumptions

- Full source compatibility for existing third-party tools is not required.
- Tool implementations are external plugins from the perspective of the core Onion.
- Tool implementations should reference only `UnityCLILoop.ToolContracts`.
- First-party tools are grouped in `UnityCLILoop.FirstPartyTools.Editor` for now rather than split into per-tool asmdefs.
