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

Issue #3222: on a large project the capture ran synchronously on the main thread after every
script compile and took about 1.9–2.3 s, writing 781 files (3.3 MB) for each compile, even when
hot reload was not used. Copying only the changed sources and skipping the capture when both
readers are disabled shortened it, but the main thread still waited on it, most of all on
listing the compilation assemblies from Unity.

What the capture calls on Unity must stay on the main thread: `CompilationPipeline.GetAssemblies`,
`SessionState`, and the Package Manager. Everything else it does is file IO and PDB reads, which
the pool can do.

The guarantee that an edit made right after `uloop compile` returns is never captured as
compiled source used to hold because the capture finished inside the domain load, before the
command listener opened. Holding `get-compile-status` keeps that guarantee. A source written
after the compile started is still checked against the PDB, so a capture that ends after the
30-second cap stays correct; only the timing guarantee of the compile response is lost.

## Considered Options

- **1. Keep `GetAssemblies` on the main thread and stop at the incremental copy.** Rejected: the
  main thread still stalls.
- **2. Build only the capture's assembly list from the dlls and PDBs.** Not enough alone: the
  cost of `GetAssemblies` only moves to the warm-up or the first run. Adopted together with 4.
- **3. Move the capture, the warm-up's reference graph, the default file selection, and the
  patch target resolution all to lists built from dlls and PDBs.** Too large. Only the part that
  keeps the warm-up from calling `GetAssemblies` at startup (rebuilding the list from a saved
  one and the PDBs) was adopted.
- **4. Run the capture on a pool thread.** Not possible alone: `GetAssemblies` is main-thread
  only, and while the capture moves the previous snapshot directory nothing is published, so
  every reader has to wait on the capture. Adopted together with 2, with the gate below.
- **Waiting for the capture in the tool execution path.** Rejected: `get-compile-status` is an
  internal bridge command that never enters it, so the compile response would not wait. The
  hold is in its `Ready` instead.
- **Making the pause point wait for the capture in every case.** Rejected: a pause point
  re-armed on the first update tick after a reload would block the main thread for the whole
  capture, which is the stall this change removes.
- **Re-arming pause points after the capture instead.** Rejected: the re-arm might not finish
  before Play Mode starts.

## Revisit When

- Unity makes `CompilationPipeline.GetAssemblies` safe to call off the main thread, or
- the way the CLI waits for a compile changes, so the hold in `get-compile-status` no longer
  stops the compile response.
