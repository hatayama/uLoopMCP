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
Packages/src/Cli~/             npm package source for both the global dispatcher and project-local CLI bundle
Packages/src/Cli~/src/cli.ts   project-local CLI entrypoint
Packages/src/Cli~/src/dispatcher.ts global dispatcher entrypoint
```

The existing `Packages/src/Cli~` directory is the npm publish root for `uloop-cli`.
To reuse the existing npm registry entry without adding another package, v3 keeps this publish root and changes only the exposed `bin.uloop` target to the dispatcher bundle.
The project-local CLI bundle remains in the same package so Unity can copy it into each project.

## Branch Strategy

Use this branch for the first v3 implementation PR:

```text
feature/hatayama/add-v3-dispatcher-distribution
```

A long-lived integration branch now exists:

```text
v3-beta
```

Feature PRs for this migration should target `v3-beta`.

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

The project-local CLI install path is:

```text
.uloop/bin/uloop
```

The first TypeScript v3 implementation builds two bundles from `Packages/src/Cli~`:

```text
dist/dispatcher.bundle.cjs  published npm bin for the global `uloop`
dist/cli.bundle.cjs         project-local CLI implementation copied to `.uloop/bin/uloop`
```

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

## Current CLI Install And Version Flow

The v2 flow assumes the global CLI and Unity Package are the same artifact version.

1. Unity detects the global CLI by executing `uloop --version`.
2. Settings and Setup Wizard compare the detected CLI version with `McpConstants.PackageInfo.version`.
3. The install button runs `npm install -g uloop-cli@<Unity Package version>`.
4. CLI diagnostics for package/CLI mismatch tell users to run `npm install -g uloop-cli@<server version>` or `uloop update`.
5. The CLI `update` command runs `npm install -g uloop-cli@latest`.

In v3 this comparison changes meaning.

1. The global `uloop-cli` version is the dispatcher version.
2. The Unity Package version determines the project-local CLI bundle copied to `.uloop/bin/uloop`.
3. Unity setup should install or update the global dispatcher when missing or older than the minimum supported dispatcher version.
4. Unity setup should install or refresh `.uloop/bin/uloop` when the project-local CLI is missing or older than the Unity Package version.
5. Runtime mismatch diagnostics should prefer refreshing the project-local CLI instead of telling the user to downgrade the global npm package.

## Implementation To-Do

- [x] Inventory current MCP implementation, UI, docs, package scripts, and publish workflows.
- [x] Inventory current CLI install/update/version mismatch flow.
- [x] Decide exact project-local CLI install path and generation flow.
- [x] Add tests for dispatcher project resolution and argument forwarding.
- [x] Implement the TypeScript global dispatcher.
- [x] Move or package the existing TypeScript CLI as the project-local implementation.
- [ ] Update Unity-side install/setup flow to install or refresh project-local CLI. (initial install path added; status/detection refresh still pending)
- [x] Remove MCP server functionality and related setup surfaces. (TypeScript MCP server, config generation, and Settings MCP tab removed first.)
- [ ] Update command help, README, docs, and diagnostics for CLI-only v3. (in progress)
- [ ] Update Release Please and publish workflows for `3.0.0`.
- [ ] Run TypeScript lint, build, and CLI tests.
- [ ] Run Unity compile and targeted tests where needed.
- [ ] Delete this temporary plan file before finalizing the implementation.

## Open Questions

- How much of the historical `uLoopMCP` naming should be renamed in v3 versus deferred to a later cleanup?
- What should replace `UnityMcpSettings.json` if settings are renamed during CLI-only migration?
