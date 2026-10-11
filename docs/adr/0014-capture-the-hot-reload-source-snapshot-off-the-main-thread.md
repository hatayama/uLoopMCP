# Capture the Hot Reload Source Snapshot Off the Main Thread

Date: 2026-10-11

## Decision

After every domain reload, hot reload copies the sources of the compile into a snapshot so it can
later tell which methods were edited. That capture now runs on a pool thread:

- The domain load reads on the main thread only the values the capture needs from Unity (the
  project root, the compile start record, the package roots, the compiled assemblies). It hands
  them to the pool and returns without waiting.
- At most one capture runs per domain. A hot reload run awaits it. A pause point blocks on it,
  except while a capture runs and the file on disk matches the checksum the compiled assembly's
  PDB recorded: those are the bytes the compiler read, so the pause point reads that file
  instead of waiting.
- `get-compile-status` reports not ready while a capture runs, so `uloop compile` returns only
  after the capture, up to 30 seconds. The hold lives in the internal bridge command, because
  the CLI waits for a compile by polling it, outside the tool execution path.
- A domain reload or a compile start cancels the running capture without waiting. A capture that
  failed, found no compiled assembly, or was cancelled leaves the domain uncaptured, and the
  next reader starts a new one.

## Context

Issue #3222: on a large project the capture blocked the Editor's main thread after every
compile. After the incremental copy, users still waited a median of 975 ms after a method-body
edit, 784 ms of it listing the compilation assemblies from Unity. After adding or deleting a
file they waited 1435 ms. The request was to make the wait vanish or nearly so.

What the capture calls on Unity must stay on the main thread: `CompilationPipeline.GetAssemblies`,
`SessionState`, and the Package Manager. Everything else it does is file IO and PDB reads, which
the pool can do.

The guarantee that an edit made right after `uloop compile` returns is never captured as
compiled source used to hold because the capture finished inside the domain load, before the
command listener opened. Holding `get-compile-status` keeps that guarantee. A source written
after the compile started is still checked against the PDB, so a capture that ends after the
30-second cap stays correct; only the timing guarantee of the compile response is lost.

## Rejected Alternatives

- **Waiting for the capture in the tool execution path.** `get-compile-status` is an internal
  bridge command that never enters it, so the compile response would not wait.
- **Making the pause point wait for the capture in every case.** A pause point re-armed on the
  first update tick after a reload would then block the main thread for the whole capture,
  which is the stall this change removes.
- **Re-arming pause points after the capture instead.** The re-arm might then not finish before
  Play Mode starts.

## Revisit When

- Unity makes `CompilationPipeline.GetAssemblies` safe to call off the main thread, or
- the way the CLI waits for a compile changes, so the hold in `get-compile-status` no longer
  stops the compile response.
