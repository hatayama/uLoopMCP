# Compile Check Starts Dependents Before the Dependents They Reference Finish

Date: 2026-10-15

## Decision

`uloop compile-check` starts an assembly that has to compile - one selected for its own sources,
or a dependent at least one of whose finished references changed its public surface - as soon as
every assembly it references that was selected for its own sources has finished. It does not wait
for the dependents it references that are still compiling. For those it reads Unity's reference
assembly, assuming they keep Unity's public surface.

- An assembly's final state is decided only once every assembly it references in the plan has
  one. When every assumption held, the compile it started early is its result. When an assumed
  reference changed its surface, it is compiled again, against this run's output for that
  reference. When an assumed reference failed or was blocked, the assembly is blocked and its
  result is dropped. The errors and the surface verdict of a dropped result decide nothing for the
  assemblies below it.
- An assembly is compiled at most twice per run: the second compile starts only once every
  reference has its final state, so it assumes nothing.
- An assembly whose recorded result may still be replayed once the dependents it references
  finish does not start early; it waits for them. A compile overwrites the record, so starting
  early would throw away a record the same inputs replay, and every run over those inputs would
  compile it twice. The record keeps, beside its input key, a digest of every input except the
  references to project assemblies and the identity of each of those references, so the check
  compares everything but the references still compiling, without reading the sources when a
  finished reference already differs.
- An assembly still waits for references selected for their own sources (they are likely to
  change their surface), and a dependent none of whose finished references changed still waits
  (it may still be skipped). `--all` selects every assembly for its own sources, so nothing starts
  early there.

## Context

On a project with 561 assemblies, adding a public member to an assembly that 67 others reference
compiled all 68 and took 35 to 43 seconds. The edited assembly took about 10.5 seconds; the 67
dependents took 57 to 71 seconds of CPU between them but 25 to 32 seconds of wall time with eight
jobs, because they reference each other through 354 references up to ten levels deep. Every
dependent waited for its references to finish, so the critical path through the dependents - 31
to 37 seconds - was the wall time. More jobs did not help: 12 and 16 jobs saved 2 to 5 seconds,
and 24 jobs took almost twice as long.

In all nine of those runs, the only reference assembly that differed from Unity's build was the
edited assembly's: the run's surface checks named one assembly each time. Every dependent waited
for the dependents it references only to read Unity's reference assembly for them anyway.

Simulating the schedule with the measured unit times puts the new critical path at 20 to 25
seconds. The floor is the edited assembly plus the largest dependent, which can only start after
it.

## Considered Options

- **Start a dependent when the dependents it references have handed their sources to csc, rather
  than when they finish.** It would remove the wasted early compile of a run that replays the
  references, but that waste happens only when a replay and a changed surface coincide, and only
  then on repeated runs. Not now.
- **Stop a running compile as soon as an assumption fails.** It needs a per-assembly cancel, and
  the compile it saves is at most one per assembly per run. Not now.
- **Write the early compile's outputs and record somewhere else and move them into place when they
  are kept.** It would protect the record without the waiting rule, but where the outputs live is
  assumed by the compile, the surface verdict and the changed-files baseline alike, so it moves
  many parts, and the early compile would still run on every repeated run.

## Limits

- An assembly selected for its own sources has no record that can still apply (its sources
  changed), so it starts early. If its changed-files check falls back to a full compile and an
  assumed reference then changes its surface, the full compile removes its changed-files baseline
  and the compile again records a new one. The loss is only for running the same state again with
  a changed dependent below an edited assembly: a changed-files check becomes two full compiles.
  It did not occur in the measured runs.

## Revisit When

- On a real project, more than one in ten of the assemblies that started early carry
  `compiled_again_after` in the VibeLog. Then narrow what is assumed, or stop starting early.
- More than one in ten of the assemblies that started early, or waited instead, carry
  `waited_for_record` - a wait for a record that then did not apply. Then revisit the record check.
- A report shows the changed-files baseline of an edited assembly lost this way. Then extend the
  record check to the baseline.
