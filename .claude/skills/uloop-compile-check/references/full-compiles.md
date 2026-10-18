# When compile-check compiles in full

Read this when a run took longer than you expected, when an assembly shows up in
`FastPathFallbacks`, or when you want to know what the next edit will cost.

The changed-files check applies to assemblies with at least 300 sources, once compile-check has
compiled that assembly in full without errors and kept a baseline. Smaller assemblies are always
compiled in full.

## What an edit costs

| Edit | What runs | Typical time |
|------|-----------|--------------|
| A body-only edit in a large assembly with a baseline | Changed-files check | A few seconds |
| A new, deleted or renamed `.cs` file | Full compile of the assembly | About Unity's compile of it, without the import and the domain reload |
| A declaration change (a member, a signature, a `using` directive) | Full compile, plus the dependents that can see the change | The assembly, then its dependents |
| The first run after a Unity build | Full compile of every changed assembly | As for a full compile |
| The first run after a full compile that had errors | Full compile (no baseline was recorded) | As for a full compile |
| An assembly compiled only because a reference changed | Full compile now, and again on its next body-only edit (it records no baseline) | As for a full compile |
| `--full` | Changed assemblies in full | As for a full compile |
| `--all` | Every assembly | The whole build |

## Dependents

When an assembly compiles in full, its public surface (its reference assembly) is compared with the
previous one. Only the dependents that write a name the change touches are compiled; adding a member
counts the type's name among the changed names. A change a dependent can feel without writing any
name is unnamed and compiles every dependent: a new base type or interface, an operator, an added
abstract member, a member C# binds by pattern (`GetEnumerator`, `Deconstruct` and the like), among
others.

Dependents wait for the assembly you edited to finish. Only transitive dependents start earlier:
they assume that a dependent still compiling keeps its public surface, and they are compiled again
when that turns out to be wrong.

## Baselines and `no baseline` reasons

A full compile that succeeds records the baseline the next changed-files check is made against. When
there is none, `FastPathFallbacks[].Reason` says why:

- `no baseline: no full compile of this assembly is on record`: no full compile has run yet, or an
  input changed while it ran (`FastPathBaselineNotes` of that run says which).
- `no baseline: the last full compile had errors; the first full compile without errors records one`:
  a failed full compile drops the baseline an earlier one left.
- `no baseline: the last full compile did not record one`: `FastPathBaselineNotes` of that run says
  why, or the assembly was compiled only because a reference changed.
- `no baseline: it was recorded by another version of compile-check`: the baseline was written by a
  different compile-check build.

In every case the next full compile that succeeds records one, and body-only edits after it are
fast-checked.

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
at the whole compilation. Run once with `--full` before finishing to cover them.
