# When compile-check compiles in full

Read this when a run took longer than you expected, when an assembly shows up in
`FastPathFallbacks`, or when you want to know what the next edit will cost.

The changed-files check applies to assemblies with at least 300 sources, once compile-check has
compiled that assembly in full without errors and kept a baseline, and at most 30 of its files
changed. Smaller assemblies are always compiled in full.

## What an edit costs

| Edit | What runs | Typical time |
|------|-----------|--------------|
| A body-only edit of up to 30 files in a large assembly with a baseline | Changed-files check | A few seconds |
| A body-only edit of more than 30 files | Full compile (`FastPathFallbacks`: too many changed files) | As for a full compile |
| A new, deleted or renamed `.cs` file | Full compile of the assembly | About Unity's compile of it, without the import and the domain reload |
| A declaration change (a member, a signature, a `using` directive) | Full compile, plus the dependents that can see the change | The assembly, then its dependents |
| The first run after a Unity build | Full compile of every changed assembly | As for a full compile |
| The first run after a full compile that had errors | Full compile (no baseline was recorded) | As for a full compile |
| An assembly compiled only because a reference changed | Full compile now, and again on its next body-only edit (it records no baseline) | As for a full compile |
| `--full` | In full: the assemblies fast-checked since their last full compile, and the dependents their rebuilt reference assemblies reach; the rest reused | As for a full compile of those; about a second when there are none |
| `--all` | Every assembly | The whole build |

## Dependents

When an assembly compiles in full, its public surface (its reference assembly) is compared with the
one from the last Unity build, not with the previous compile-check run; a surface that differs from
Unity's keeps compiling its dependents until Unity builds again. Only the dependents that write a
name the change touches are compiled; adding a member counts the type's name among the changed
names. A change a dependent can feel without writing any name is unnamed and compiles every
dependent: a new base type or interface, an operator, an added abstract member, a member C# binds by
pattern (`GetEnumerator`, `Deconstruct` and the like), among others. The output's `SurfaceChecks`
lists, per assembly that others reference, the verdict, the changed names or the unnamed reason, and
the dependents compiled for it with the name that selected each.

An assembly starts once every assembly it references that this run also compiles has finished. An
assembly selected for its own sources, or a dependent already known to need compiling, starts
sooner: as soon as every assembly it references that was selected for its own sources has finished.
The dependents it references that are still compiling are assumed to keep Unity's public surface;
when one of them turns out to have changed it, the assembly is compiled again after they finish, at
most twice per run. A dependent whose recorded result may still apply once those finish waits for
them instead.

## Baselines and `no baseline` reasons

A full compile that succeeds records the baseline the next changed-files check is made against. When
there is none, `FastPathFallbacks[].Reason` says why:

- `no baseline: no full compile of this assembly is on record`: no full compile has run yet, or an
  input changed while it ran (`FastPathBaselineNotes` of that run says which).
- `no baseline: the last full compile (at <time>) had errors; the first full compile without errors
  records one`: a failed full compile drops the baseline an earlier one left.
- `no baseline: the last full compile (at <time>) did not record one`: `FastPathBaselineNotes` of
  that run says why, or the assembly was compiled only because a reference changed.
- `no baseline: it was recorded by another version of compile-check`: the baseline was written by a
  different compile-check build.

`<time>` is when that full compile finished, in UTC; it can be from an earlier session or from
before a Unity build in between.

In every case the next full compile that succeeds records one, and body-only edits after it are
fast-checked.

A run with `--all` records baselines too. Under `--all`, a reference whose rebuilt API differs from
Unity's build is read from this check's output; when a later run leaves that reference out and so
reads Unity's build again, the first body edit of an assembly referencing it is compiled in full,
with `inputs other than source contents changed: references` as the reason.

## Runs that stop on declaration errors

A run whose assembly has declaration errors, such as an unknown type or an ambiguous name, reports
those errors only: the errors inside method bodies, the warnings of that stage (which `-warnaserror`
would turn into errors) and the analyzer diagnostics are not reported until the declaration errors
are fixed. Such a run is also faster than a full compile. Fix the declaration errors and run again.

## Analyzers

A full compile uses the response file as Unity wrote it, its `-analyzer:` entries and flags such as
`-warnaserror` included; compile-check adds only the output folder for generated files. The
changed-files check skips the rules listed in `FastChecks[].UncheckedCodes`: those of CS0067,
CS0169, CS0414 and CS0649 that the project does not suppress, and the rules of analyzers that look
at the whole compilation. Run once with `--full` before finishing to cover them. That run compiles
in full the assemblies fast-checked since their last full compile, and the dependents their rebuilt
reference assemblies reach, and reuses the rest; after a run that compiled in full it takes about a
second.
