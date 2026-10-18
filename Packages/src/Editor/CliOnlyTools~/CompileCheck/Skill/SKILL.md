---
name: uloop-compile-check
description: "Compile C# with the Unity-bundled Roslyn compiler without launching or contacting the Unity Editor; reports errors and warnings only."
---

# uloop compile-check

Compile the project's C# offline and report the diagnostics, without starting Unity or sending
anything to a running Editor. It replays the response files Unity wrote during its last build with
the C# compiler bundled in the Editor install; the output lands in `Library/uloop/compile-check/`
and is never loaded by Unity.

## When to use

- The Editor is not running and you only want to know whether the code compiles.
- You want compiler errors quickly, without waiting for an import and a domain reload.
- You are iterating on code whose result does not need to be live in the Editor yet.

## When not to use

- You want the change reflected in the Editor (play mode, tests, a tool call): run `uloop compile`.
- You just added or removed an `.asmdef`, added a reference to one, or changed scripting defines:
  run `uloop compile` once first. The response files describe the previous build only, so
  compile-check refuses the run with `COMPILE_CHECK_UNITY_BUILD_REQUIRED` instead of reporting
  diagnostics for a configuration the project no longer has.
- The project has no `Library` yet (a fresh clone, or an Editor that has never opened it): there is
  no build to replay, so the run is refused with the same code. Let Unity build once, with
  `uloop launch` or headless, without a window:

  ```bash
  <Unity executable> -batchmode -nographics -quit -projectPath /path/to/project
  ```

  After that one build, compile-check works without the Editor until an `.asmdef` or build-setting
  change makes it ask for another (see Limitations).

## Usage

```bash
uloop compile-check
uloop compile-check --all --project-path /path/to/project
```

By default it compiles the assemblies whose sources changed since the last Unity build, plus every
assembly that references one of them; `--all` compiles every assembly in the build. Assemblies that
do not depend on each other compile at the same time; `--jobs` caps how many run at once, and
`--jobs 1` compiles them one after another.

An assembly whose inputs are all exactly as the previous run read them is not compiled again: the
run reports the diagnostics recorded then, errors included. `--all` compiles everything from
scratch, which is the way out if a reused result ever looks wrong.

Once compile-check has compiled a large assembly in full without errors, an edit that keeps every
declaration is checked on its changed files only, in a few seconds, and the assembly is listed in
`FastCheckedAssemblies`. Compiled in full instead, with the reason in `FastPathFallbacks`: the first
run after a Unity build; a run that adds, removes or renames a `.cs` file; an edit that changes a
declaration (a `using` directive counts); the first run after a full compile that had errors; and an
assembly compiled only because one it references changed, which records no baseline. A change to the
public surface also compiles the dependents that can see it: those that write a changed name, or
all of them when no name carries the change. Details and times: `references/full-compiles.md`.

Full compiles run the project's analyzers with the response file's flags, `-warnaserror` included.
The changed-files check skips the assembly-wide rules in `FastChecks[].UncheckedCodes`, so run once
with `--full` before finishing; when warnings are errors, such a rule (CS0414, for example) can fail
that run only. A run that stops on declaration errors, such as an unknown type, reports neither the
errors inside method bodies nor analyzer diagnostics: fix them and run again.

## Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `--all` | flag | Compile every assembly instead of only the changed ones and their dependents |
| `--full` | flag | Compile changed assemblies in full instead of checking only their changed files |
| `--editor-version <version>` | string | Use this Unity Editor version's compiler instead of ProjectVersion.txt |
| `--jobs <N>` | number | Compile up to N assemblies at once (default: half the CPUs, at least 1; `1` compiles sequentially) |
| `--max-depth <N>` | number | Search depth when locating the project (default: 3, -1 for unlimited) |
| `--project-path <path>` | string | Target another Unity project instead of the current directory |

## Output

A JSON payload; an empty list means the case did not occur in this run:

- `Success`: whether the compile produced no errors
- `ErrorCount`, `WarningCount`: totals across every compiled and reused assembly
- `Errors`, `Warnings`: each with `Message`, `Code`, `File`, `Line`, `Column`, `Assembly`
- `CompiledAssemblies`: the assemblies this run compiled, in dependency order
- `ReusedAssemblies`: the assemblies reported from the previous run's result, without compiling
- `BlockedAssemblies`: the assemblies not compiled because an assembly they reference has errors, as
  Unity's build would; fix those errors and run again
- `FastCheckedAssemblies`: the assemblies checked on their changed files only
- `FastChecks`: per fast-checked assembly, `ChangedFiles`, `UncheckedCodes` (rules only `--full` runs),
  `GeneratorsNotRerun`, and `Reused` (the same edit's previous check, reported again)
- `FastPathFallbacks`: `Assembly` and `Reason` for each assembly that was compiled in full instead
- `FastPathBaselineNotes`: `Assembly` and `Reason` for each successful full compile that left no
  baseline for the next changed-files check
- `SkippedAssemblies`: how many assemblies were left out because nothing they can see changed
- `ResponseFileSet`: the Bee build the run replayed
- `ProjectRoot`: resolved project root
- `Message`: one-line summary
- `NextActions`: what to do next (fix and rerun, `--full` for the skipped rules, or `uloop compile`
  to make the result live); empty when nothing changed since the Unity build

The process exits 1 when `ErrorCount` is greater than zero, and when the command itself fails, such
as with `COMPILE_CHECK_UNITY_BUILD_REQUIRED`; then no payload is printed and the error, with its
`ErrorCode`, goes to stderr.

Only one compile-check runs per project at a time; a second waits up to five minutes, then fails
with `COMPILE_CHECK_PROJECT_BUSY`. Run it once more rather than retrying in a loop.

## Limitations

- It replays the last Unity build's response files. A new or deleted `.asmdef`, a changed `.asmdef`
  reference, an `.asmdef` that stopped building for the Editor, or a newly required precompiled
  reference stops the run and asks for `uloop compile`.
- Build settings are compared against a record the first run after a Unity build writes: a
  removed scripting define, a changed `csc.rsp`, `defineConstraints`, `versionDefines`,
  `packages-lock.json` or `manifest.json` stops the run, and a scripting define the last build lacks
  stops it when `ProjectSettings.asset` was saved after that build. A change made before that first
  run (other than an added define) is not seen, so run `uloop compile` after one. Under a Unity 6
  build profile that overrides Player Settings or carries its own defines, only the global
  ProjectSettings defines are compared.
- A reference removed from an `.asmdef` stops the run too; the four references Unity adds on its own
  are allowed, and a reference that matches no `.asmdef` in the project is not judged.
- A new `.cs` file outside every `.asmdef` goes to `Assembly-CSharp` or a sibling by Unity's path
  rules (`Editor` folders, `Assets/Plugins`); if the last build did not produce that assembly, the run
  stops and asks for `uloop compile`. A new script in a package folder without an `.asmdef` is not seen.
- A generator that reads a referenced assembly is not rerun for a change in that assembly; run
  `uloop compile` after one.
