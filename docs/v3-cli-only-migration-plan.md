# v3 CLI-Only Migration Plan

This is a temporary working plan for the v3 migration.

Delete this file before the implementation is finished. While the work is in progress, use this file as the source of truth before changing code.

## Goal

Move Unity CLI Loop to a CLI-only v3 distribution model.

The v3 release changes the product contract in two major ways:

1. The globally installed `uloop-cli` package becomes a thin `uloop` dispatcher.
2. MCP functionality is removed completely. The product becomes CLI-only.

The command name remains `uloop`, and the existing npm package name remains `uloop-cli`.

## Version Strategy

All public v3 artifacts start from `3.0.0`.

```text
Unity Package:        3.0.0
project-local CLI:    3.0.0
global uloop-cli:     3.0.0
```

`uloop-cli@3.0.0` is not a new command name. It is the existing npm registry entry repurposed so that the global `uloop` command dispatches into the target Unity project.

## Repository Strategy

Keep the monorepo.

The Unity Package, project-local CLI, and global dispatcher share contracts such as project discovery, `.uloop/bin/uloop` layout, `--project-path`, version reporting, and diagnostic output. Keeping them in one repository reduces cross-repo coordination risk.

Expected working layout:

```text
Packages/src/                  Unity Package
Packages/src/Cli~/             project-local CLI source
Packages/src/GlobalDispatcher~  global dispatcher source
```

The exact dispatcher path can change during implementation if the existing repo layout suggests a simpler shape.

## Branch Strategy

Use this branch for v3 development:

```text
feature/hatayama/add-v3-dispatcher-distribution
```

Longer term, consider a dedicated `v3-beta` integration branch if the work spans multiple PRs.

`main` remains the 2.x stable line. Bug fixes should land on `main` first, then be merged or cherry-picked into the v3 branch.

```text
bugfix -> main -> 2.x release
              \
               -> merge/cherry-pick into v3 work
```

## Migration Order

Do not combine the distribution model change with the Go rewrite.

1. First, migrate to the v3 dispatcher plus project-local CLI structure while still using TypeScript.
2. Then add Go as an opt-in project-local implementation.
3. Promote stable Go commands to default gradually.
4. Keep the TypeScript implementation as the reference until Go output, exit codes, errors, and JSON-RPC requests match.

## CLI Dispatch Contract

The global dispatcher should stay thin.

Responsibilities:

1. Resolve the target Unity project from `--project-path`, current directory discovery, or a future explicit project selection mechanism.
2. Find the project-local CLI at `.uloop/bin/uloop`.
3. Forward arguments with minimal interpretation.
4. Return the project-local CLI exit code.

Non-responsibilities:

1. Do not implement Unity operations in the global dispatcher.
2. Do not duplicate command-specific option knowledge in the global dispatcher.
3. Do not make the dispatcher a long-running proxy.

## MCP Removal Scope

Remove MCP as a feature, not just from marketing copy.

Areas to inspect:

1. TypeScript MCP server implementation under `Packages/src/TypeScriptServer~`.
2. Unity Editor UI and Setup Wizard references to MCP server setup.
3. C# editor/server classes that expose MCP protocol behavior.
4. npm scripts, package manifests, lockfiles, and publish workflows for MCP server artifacts.
5. Skill generation, metadata, docs, README, screenshots, and user-facing command references.
6. Runtime settings and settings file names that currently encode MCP concepts.
7. Version mismatch diagnostics and update guidance that assume global CLI equals Unity Package.

Be careful with historical names such as `uLoopMCP` namespace. Removing MCP functionality does not automatically require renaming every namespace in the same PR if that would make the migration too risky.

## Release Please And Publish Notes

Current Release Please config treats `Packages/src` as the release target and updates CLI-related files as extra files.

For v3, keep the first transition simple:

1. Release v3 as a single major release.
2. Keep using the existing `uloop-cli` npm registry.
3. Avoid introducing a separate `uloop-dispatcher` npm package unless later releases need fully independent dispatcher versioning.

If dispatcher-only releases become necessary later, convert Release Please to a package-aware setup with a dispatcher component tag such as `uloop-dispatcher-v3.1.0`.

## Implementation To-Do

- [x] Inventory current MCP implementation, UI, docs, package scripts, and publish workflows.
- [ ] Inventory current CLI install/update/version mismatch flow.
- [ ] Decide exact project-local CLI install path and generation flow.
- [ ] Add tests for dispatcher project resolution and argument forwarding.
- [ ] Implement the TypeScript global dispatcher.
- [ ] Move or package the existing TypeScript CLI as the project-local implementation.
- [ ] Update Unity-side install/setup flow to install or refresh project-local CLI.
- [x] Remove MCP server functionality and related setup surfaces. (TypeScript MCP server, config generation, and Settings MCP tab removed first.)
- [ ] Update command help, README, docs, and diagnostics for CLI-only v3. (in progress)
- [ ] Update Release Please and publish workflows for `3.0.0`.
- [ ] Run TypeScript lint, build, and CLI tests.
- [ ] Run Unity compile and targeted tests where needed.
- [ ] Delete this temporary plan file before finalizing the implementation.

## Open Questions

- Should `v3-beta` be created as a long-lived integration branch now, or only after this first implementation branch becomes too large?
- Should `.uloop/bin/uloop` be generated by Unity on package import, by Setup Wizard, or lazily by the global dispatcher?
- How much of the historical `uLoopMCP` naming should be renamed in v3 versus deferred to a later cleanup?
- What should replace `UnityMcpSettings.json` if settings are renamed during CLI-only migration?
