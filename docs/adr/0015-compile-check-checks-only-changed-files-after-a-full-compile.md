# Compile Check Checks Only the Changed Files After a Full Compile

Date: 2026-10-14

## Decision

`uloop compile-check` checks only the changed files of an assembly, instead of compiling the
whole assembly with csc, when both of these hold:

- The last time compile-check compiled that assembly in full, the compile succeeded, and the
  inputs other than the source contents (the compiler, the response file, the references, the
  analyzers and their inputs, the output name, and the list of source paths) are still the same.
- Every changed file has the same declaration fingerprint as at that full compile: only method
  bodies, accessor bodies, expression bodies, non-constant initializers, comments, or whitespace
  changed. Anything another file or assembly can see (signatures, constants, attributes,
  parameter defaults, `using` directives, preprocessor directives outside bodies, `#nullable`
  anywhere) is part of the fingerprint. So are the initializers of fields and properties of a
  partial type: the nullable analysis of a constructor reads every initializer of the type, and
  its warnings land in whichever file declares the constructor or the field.

In every other case it compiles the assembly in full, as before, and reports why. It also falls
back to a full compile when an analyzer throws while checking a changed file, because csc then
reports that failure as AD0001. `--full` turns the changed-files check off for one run.

The check runs in a C# worker built on the user's machine with the Roslyn that ships with the
Editor, so the worker uses only the public Roslyn API that Roslyn 4.3.1 already has.

## What It Guarantees

- The compiler diagnostics and the analyzer diagnostics of the changed files, with the
  suppressions of `DiagnosticSuppressor`s applied, and with the options of the response file
  (`-nowarn`, `-warnaserror`, `-ruleset`, `-define`, `-nullable`) applied.
- For the files that did not change, the diagnostics of the last full compile. An edit the
  fingerprint accepts does not change what the compiler binds or analyzes in another file, so
  their binding and flow-analysis diagnostics stay as they were. The exceptions are the
  assembly-wide diagnostics listed below.

## What It Does Not Guarantee

The output tells the caller to run `--full` before finishing whenever an assembly was checked
this way, and lists by ID the diagnostics of the first two items below:

- CS0067, CS0169, CS0414, and CS0649. The compiler reports these unused-member warnings only
  after it has seen the whole assembly.
- The rules of analyzers that use Compilation, CompilationEnd, SemanticModel, or SymbolStart
  actions. A per-file analysis does not run them, or cannot finish a symbol declared across
  files.
- Errors the compiler reports only while emitting the assembly, after binding: for example
  CS8178 (`await` in an expression that also calls a method returning by reference) and CS0656
  (a missing compiler-required member, such as the runtime binder `dynamic` needs). The check
  does not emit, because emitting the whole assembly would cost the time it saves.
- Source generators. The check reads the generator output saved by the last full compile
  instead of running them again, and does not report their diagnostics. A generator that reads
  method bodies can produce different output after a body-only edit.

## Context

compile-check compiles each changed assembly in full with csc. In a large project (about 3,000
files in one assembly, about 400 references, and about 30 analyzer DLLs) a one-line edit of a
method body took 17–22 seconds, and about 8.5 seconds of that is the analysis of every file in
the assembly, which a whole-assembly compile cannot avoid. A prototype that checked only the
changed file through the Roslyn API, reusing the generator output of the last full compile,
took 2.7 seconds for the same edit, and its per-file diagnostics matched csc for every file of
that assembly except CS0414.

Compiler diagnostics are not suppressed by `DiagnosticSuppressor`s in a per-file analysis. A
relay analyzer built on the public API (it reports the compiler diagnostics that a suppressor
can suppress again from a syntax tree action) makes the suppressors apply on Roslyn 4.3, 4.10,
and 5.6.

## Considered Options

- **Use the internal `CSharpCompilerDiagnosticAnalyzer`, as the IDE does.** Rejected: it works
  only on Roslyn 4.3.1; on 4.10 and later it returns no diagnostics at all.
- **Set `DocumentationMode.Parse` so that semantic model actions run per file.** Rejected: it
  changes what documentation-comment rules such as StyleCop's SA0001 and SA1600 family report,
  so the check would disagree with csc.
- **Keep a resident worker process with the compilation loaded.** Out of scope for now: it held
  about 1.3 GB per assembly, and the per-run worker is already fast enough.

## Revisit When

- Two bug reports trace back to something the check does not guarantee. Then reconsider a
  resident process that checks the whole assembly, or turning the changed-files check off by
  default.
