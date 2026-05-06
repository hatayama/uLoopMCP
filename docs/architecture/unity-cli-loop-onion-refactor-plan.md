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
- Added registry tests proving:
  - bundled tools are discovered through the attribute path.
  - `get-logs` is first-party after the assembly rename.
  - `get-version` is not registered as an extension-facing tool.
  - `get-version` still executes as an internal bridge command.
  - `get-tool-details` is not registered as an extension-facing tool.
  - `get-tool-details` still returns the CLI catalog through the internal bridge path.
  - `focus-window` is not registered as a Unity-side tool.
  - legacy MCP-era development tools are not registered.
  - `hello-world` is registered as an extension tool.
  - `control-play-mode` is registered from `UnityCLILoop.FirstPartyTools.Editor`.
  - `control-play-mode` skill discovery still works after moving under `UnityCLILoop.FirstPartyTools.Editor`.
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
  - pure platform values `DynamicCodeSecurityLevel` and `ToolDisabledException` now compile under `UnityCLILoop.Domain`.
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
- Move startup/DI wiring into `UnityCLILoop.CompositionRoot.Editor`.
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
