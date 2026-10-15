package compilecheck

import (
	"context"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"reflect"
	"strings"
	"testing"

	"github.com/hatayama/unity-cli-loop/dispatcher/internal/compilecheck/fastcheck"
	"github.com/hatayama/unity-cli-loop/dispatcher/internal/compilecheck/resultstore"
)

// Verifies that unchanged inputs are reused before the changed-files check is tried.
func TestReuseStillWinsOverFastPath(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)

	recorder := newCachedSchedulerRecorder("Alpha", "Beta")
	outcome := project.run(t, recorder, false)

	if result := resultOf(t, outcome.Units, "Alpha"); !result.Reused || result.FastPathFallbackReason != "" {
		t.Errorf("expected Alpha to be reused without a fallback reason, got %+v", result)
	}
	if project.worker.checks != 0 {
		t.Errorf("expected no check, got %d", project.worker.checks)
	}
}

// Verifies that --all compiles in full without trying the changed-files check.
func TestAllFlagSkipsFastPath(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	project.editBody(t, 0, "edited")
	project.plan.All = true

	outcome := project.run(t, newCachedSchedulerRecorder("Alpha", "Beta"), false)

	assertNotTried(t, outcome, project.worker)
}

// Verifies that --full compiles in full without trying the changed-files check, and still records
// a baseline for the compile it ran.
func TestFullFlagSkipsFastPathButRecords(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	project.editBody(t, 0, "edited")

	outcome := project.run(t, newCachedSchedulerRecorder("Alpha", "Beta"), true)

	assertNotTried(t, outcome, project.worker)
	// Why a usable baseline is enough: the store hands one back only when its input key is the one
	// the manifest of the last compile carries, and the --full compile removed the earlier pair.
	project.baseline(t)
}

// Verifies that a unit picked only as a dependent is neither checked by the changed-files check nor
// given a baseline.
func TestDependentUnitSkipsFastPath(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	project.editBody(t, 0, "edited")

	result := project.compileAlphaAsDependent(t)

	if result.FastChecked || result.FastPathFallbackReason != "" || project.worker.checks != 0 {
		t.Errorf("expected the changed-files check not to be tried, got %+v", result)
	}
	if project.hasBaseline() {
		t.Error("expected the full compile of a dependent to leave no baseline")
	}
}

// Verifies that a unit without an input key compiles in full without trying the changed-files check.
func TestUnkeyedUnitSkipsFastPath(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	project.editBody(t, 0, "edited")
	if err := os.Remove(project.plan.Units[0].Assembly.Path); err != nil {
		t.Fatalf("failed to remove the response file: %v", err)
	}

	outcome := project.run(t, newCachedSchedulerRecorder("Alpha", "Beta"), false)

	assertNotTried(t, outcome, project.worker)
}

// Verifies that a unit without a baseline compiles in full, says why, and records a baseline.
func TestMissingBaselineFallsBackAndRecords(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")

	outcome := project.run(t, recorder, false)

	assertFallback(t, outcome, recorder, "no baseline: no full compile of this assembly is on record")
	record := project.baseline(t)
	if record.WorkerKey != "worker-a" || len(record.Files) != fastPathMinimumSources {
		t.Errorf("expected a baseline of every source from worker-a, got key %s with %d files", record.WorkerKey, len(record.Files))
	}
	if file := record.Files[project.sourceName(0)]; file.Fingerprint != "class File000 " {
		t.Errorf("expected the worker's fingerprint in the baseline, got %+v", file)
	}
}

// Verifies that a baseline whose full compile outputs were changed by hand is not used.
func TestHandCorruptedStaleManifestFallsBack(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	project.editBody(t, 0, "edited")
	writeOutputFile(t, project.outputDirectory(), "Alpha"+assemblyExtension, "changed by hand")
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")

	outcome := project.run(t, recorder, false)

	assertFallback(t, outcome, recorder, "baseline compile is no longer on disk")
}

// Verifies that a manifest changed by hand to a failed compile stops the baseline from being used.
func TestHandCorruptedFailedManifestFallsBack(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	project.editBody(t, 0, "edited")
	baseline := project.baseline(t)
	if err := project.records().RecordCompile("Alpha", baseline.InputKey,
		resultstore.CompileResult[Diagnostic]{Succeeded: false, Diagnostics: baseline.Diagnostics}, unitOutputNames(project.plan.Units[0])); err != nil {
		t.Fatalf("failed to record the failed compile: %v", err)
	}
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")

	outcome := project.run(t, recorder, false)

	assertFallback(t, outcome, recorder, "baseline compile is no longer on disk")
}

// Verifies that a failed full compile leaves no baseline, so the next body edit compiles in full.
func TestFailedFullCompileLeavesNoBaseline(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	project.editDeclaration(t, 0)
	failing := newCachedSchedulerRecorder("Alpha", "Beta")
	failing.output["Alpha"] = failingCompilerOutput("Alpha")
	project.run(t, failing, false)
	if project.hasBaseline() {
		t.Fatal("expected the failed full compile to leave no baseline")
	}

	project.editBody(t, 1, "edited")
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")
	outcome := project.run(t, recorder, false)

	assertFallback(t, outcome, recorder, "no baseline: the last full compile had errors")
}

// Verifies that a change to an input other than source contents, such as a new source, compiles in full.
func TestNonSourceKeyMismatchFallsBack(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	added := "Assets/Alpha/Added.cs"
	project.writeFile(t, added, "class Added | body")
	project.plan.Units[0].Sources = append(project.plan.Units[0].Sources, added)
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")

	outcome := project.run(t, recorder, false)

	assertFallback(t, outcome, recorder, "inputs other than source contents changed: source list")
}

// Verifies that an assembly with fewer sources than the minimum is never tried, so it compiles in
// full without a fallback reason on every run.
func TestSmallAssemblyIsNotTried(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources-1)
	project.run(t, newCachedSchedulerRecorder("Alpha", "Beta"), false)
	project.editBody(t, 0, "edited")

	outcome := project.run(t, newCachedSchedulerRecorder("Alpha", "Beta"), false)

	assertNotTried(t, outcome, project.worker)
	if project.worker.ensures != 0 || project.hasBaseline() {
		t.Errorf("expected no worker and no baseline, got %d builds", project.worker.ensures)
	}
}

// Verifies that an input other than source contents that changed while csc compiled leaves no
// baseline, and says why, so the next body edit compiles in full.
func TestNonSourceInputChangedDuringCompileLeavesNoBaseline(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")
	recorder.during = func(name string) {
		if name == "Alpha" {
			touchProjectFile(t, project.root, filepath.Join("Editor", "csc.dll"))
		}
	}

	outcome := project.run(t, recorder, false)

	if note := resultOf(t, outcome.Units, "Alpha").FastPathNote; note != "baseline not recorded: inputs other than source contents changed during the compile: compiler" {
		t.Errorf("unexpected note %q", note)
	}
	if project.hasBaseline() {
		t.Fatal("expected no baseline when an input changed during the compile")
	}

	project.editBody(t, 0, "edited")
	second := newCachedSchedulerRecorder("Alpha", "Beta")
	assertFallback(t, project.run(t, second, false), second, "no baseline: the last full compile did not record one")
}

// Verifies that a baseline whose saved generator output was deleted or changed is not used, since
// checking without that output reports errors wherever code uses what the generators produce.
func TestMissingOrChangedGeneratorOutputFallsBack(t *testing.T) {
	for name, damage := range map[string]func(t *testing.T, generatedPath string){
		"deleted": func(t *testing.T, generatedPath string) {
			if err := os.RemoveAll(generatedPath); err != nil {
				t.Fatalf("failed to remove the generator output: %v", err)
			}
		},
		"changed": func(t *testing.T, generatedPath string) {
			writeOutputFile(t, filepath.Join(generatedPath, "Gen"), "G.g.cs", "class G { int x; }")
		},
	} {
		t.Run(name, func(t *testing.T) {
			project := newFastPathProject(t, fastPathMinimumSources)
			generatedPath := filepath.Join(project.root, project.records().GeneratedDirectory("Alpha"))
			recorder := newCachedSchedulerRecorder("Alpha", "Beta")
			recorder.during = func(unit string) {
				if unit == "Alpha" {
					project.writeFile(t, filepath.Join(project.records().GeneratedDirectory("Alpha"), "Gen", "G.g.cs"), "class G { }")
				}
			}
			project.run(t, recorder, false)
			if count := project.generatedFileCount(t, project.baseline(t)); count != 1 {
				t.Fatalf("expected the generated file in the baseline, got %d files", count)
			}
			damage(t, generatedPath)
			project.editBody(t, 0, "edited")
			second := newCachedSchedulerRecorder("Alpha", "Beta")

			outcome := project.run(t, second, false)

			assertFallback(t, outcome, second, "generator output of the baseline is gone")
		})
	}
}

// Verifies that more changed files than the maximum compile in full.
func TestTooManyChangedFilesFallsBack(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	for index := 0; index <= fastPathMaximumChangedFiles; index++ {
		project.editBody(t, index, "edited")
	}
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")

	outcome := project.run(t, recorder, false)

	assertFallback(t, outcome, recorder, "too many changed files (31, limit 30)")
}

// Verifies that a declaration change compiles in full and records a baseline with the new fingerprint.
func TestDeclarationChangeFallsBackAndRerecords(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	project.editDeclaration(t, 0)
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")

	outcome := project.run(t, recorder, false)

	assertFallback(t, outcome, recorder, fastcheck.DeclarationChangedOutcome+": "+project.sourceName(0))
	if file := project.baseline(t).Files[project.sourceName(0)]; file.Fingerprint != "class File000 : Base " {
		t.Errorf("expected the new declaration in the baseline, got %+v", file)
	}
}

// Verifies that a worker that cannot be built makes the unit compile in full.
func TestWorkerBuildFailureFallsBack(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	project.editBody(t, 0, "edited")
	project.worker.ensureErr = errors.New("csc failed")
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")

	outcome := project.run(t, recorder, false)

	assertFallback(t, outcome, recorder, "worker unavailable: csc failed")
}

// Verifies that a worker that fails while checking makes the unit compile in full.
func TestWorkerRunFailureFallsBack(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	project.editBody(t, 0, "edited")
	project.worker.checkErr = errors.New("timed out")
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")

	outcome := project.run(t, recorder, false)

	assertFallback(t, outcome, recorder, "worker failed: timed out")
}

// Verifies that an Unsupported outcome makes the unit compile in full with the worker's reason.
func TestUnsupportedOutcomeFallsBack(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	project.editBody(t, 0, "edited")
	project.worker.outcome = fastcheck.UnsupportedOutcome
	project.worker.reason = "analyzerconfig"
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")

	outcome := project.run(t, recorder, false)

	assertFallback(t, outcome, recorder, "Unsupported: analyzerconfig")
}

// Verifies that a baseline fingerprinted by another worker build is not used.
func TestWorkerKeyMismatchFallsBack(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	project.editBody(t, 0, "edited")
	project.worker.key = "worker-b"
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")

	outcome := project.run(t, recorder, false)

	assertFallback(t, outcome, recorder, "baseline was fingerprinted by another worker build")
	if record := project.baseline(t); record.WorkerKey != "worker-b" {
		t.Errorf("expected the baseline to be recorded again by worker-b, got %s", record.WorkerKey)
	}
}

// Verifies the clean changed-files check: Alpha is not compiled, its outputs, manifest, and baseline
// stay as they were, the diagnostics of unchanged files are carried over, and a dependent whose
// reference kept its surface is still skipped.
func TestFastPathCleanResultKeepsBaselineFiles(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	markSelectedAsDependent(t, project.plan, "Beta")
	first := newCachedSchedulerRecorder("Alpha", "Beta")
	first.surfaces["Alpha"] = "unity Alpha"
	first.output["Alpha"] = fakeCompilerOutput{stdout: "Assets/Alpha/File001.cs(1,1): warning CS0168: unused"}
	project.run(t, first, false)
	before := project.outputSnapshot(t)
	project.editBody(t, 0, "edited")
	project.worker.uncheckedCodes = []string{"CS0169"}

	recorder := newCachedSchedulerRecorder("Alpha", "Beta")
	outcome := project.run(t, recorder, false)

	result := resultOf(t, outcome.Units, "Alpha")
	if !result.FastChecked || !result.Succeeded || recorder.runs["Alpha"] != 0 {
		t.Fatalf("expected a clean changed-files check without csc, got %+v after %d runs", result, recorder.runs["Alpha"])
	}
	assertStrings(t, "changed files", result.FastCheckChangedFiles, []string{project.sourceName(0)})
	assertStrings(t, "unchecked codes", result.UncheckedCodes, []string{"CS0169"})
	if len(result.Diagnostics) != 1 || result.Diagnostics[0].File != "Assets/Alpha/File001.cs" {
		t.Errorf("expected the warning of the unchanged file to be carried over, got %+v", result.Diagnostics)
	}
	if after := project.outputSnapshot(t); !reflect.DeepEqual(after, before) {
		t.Errorf("expected the outputs, manifest, and baseline to stay, got %v then %v", before, after)
	}
	if recorder.runs["Beta"] != 0 || outcome.Skipped != 1 {
		t.Errorf("expected Beta to be skipped, got %d runs and %d skipped", recorder.runs["Beta"], outcome.Skipped)
	}
	if project.worker.lastCheck.GeneratedDirectory != filepath.Join(project.root, project.records().GeneratedDirectory("Alpha")) {
		t.Errorf("expected the saved generator output to be passed, got %q", project.worker.lastCheck.GeneratedDirectory)
	}
}

// Verifies that an error found by the changed-files check fails the unit and blocks its dependents.
func TestFastPathErrorBlocksDependents(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	markSelectedAsDependent(t, project.plan, "Beta")
	project.recordBaseline(t)
	project.editBody(t, 0, "int x = \"s\";")
	project.worker.diagnostics = []fastcheck.WorkerDiagnostic{{
		File: project.absoluteSource(0), Line: 1, Column: 20, Severity: severityError, Code: "CS0029", Message: "cannot convert",
	}}

	outcome := project.run(t, newCachedSchedulerRecorder("Alpha", "Beta"), false)

	result := resultOf(t, outcome.Units, "Alpha")
	if !result.FastChecked || result.Succeeded {
		t.Fatalf("expected a failed changed-files check, got %+v", result)
	}
	assertStrings(t, "blocked", outcome.Blocked, []string{"Beta"})
}

// Verifies that the last full compile's diagnostics of a changed file are replaced by what the check
// found, and that a diagnostic both report for an unchanged file appears once.
func TestChangedFileBaselineDiagnosticsAreReplaced(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	first := newCachedSchedulerRecorder("Alpha", "Beta")
	first.output["Alpha"] = fakeCompilerOutput{stdout: strings.Join([]string{
		"Assets/Alpha/File000.cs(1,1): warning CS0168: old warning",
		"Assets/Alpha/File001.cs(2,2): warning CS0168: kept warning",
	}, "\n")}
	project.run(t, first, false)
	project.editBody(t, 0, "edited")
	project.worker.diagnostics = []fastcheck.WorkerDiagnostic{
		{File: project.absoluteSource(0), Line: 3, Column: 3, Severity: severityWarning, Code: "CS0219", Message: "new warning"},
		{File: project.absoluteSource(1), Line: 2, Column: 2, Severity: severityWarning, Code: "CS0168", Message: "kept warning"},
	}

	outcome := project.run(t, newCachedSchedulerRecorder("Alpha", "Beta"), false)

	want := []Diagnostic{
		{Severity: severityWarning, Code: "CS0168", Message: "kept warning", File: "Assets/Alpha/File001.cs", Line: 2, Column: 2},
		{Severity: severityWarning, Code: "CS0219", Message: "new warning", File: "Assets/Alpha/File000.cs", Line: 3, Column: 3},
	}
	if got := resultOf(t, outcome.Units, "Alpha").Diagnostics; !reflect.DeepEqual(got, want) {
		t.Errorf("diagnostics = %+v, want %+v", got, want)
	}
}

// Verifies that putting an edited file back the way it was is answered by reuse.
func TestRevertedContentIsReused(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	project.editBody(t, 0, "edited")
	project.run(t, newCachedSchedulerRecorder("Alpha", "Beta"), false)
	project.editBody(t, 0, "body")

	outcome := project.run(t, newCachedSchedulerRecorder("Alpha", "Beta"), false)

	if result := resultOf(t, outcome.Units, "Alpha"); !result.Reused {
		t.Errorf("expected the reverted assembly to be reused, got %+v", result)
	}
}

// Verifies that a baseline agreeing with every source but disagreeing with the current key is
// dropped, and the unit compiles in full.
func TestZeroChangedFilesDropsBaselineAndFallsBack(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	baseline := project.baseline(t)
	records := project.records()
	if err := records.RecordCompile("Alpha", "tampered",
		resultstore.CompileResult[Diagnostic]{Succeeded: true, Diagnostics: baseline.Diagnostics}, unitOutputNames(project.plan.Units[0])); err != nil {
		t.Fatalf("failed to record the tampered compile: %v", err)
	}
	if err := records.RecordBaseline("Alpha", resultstore.BaselineInputs{
		InputKey: "tampered", NonSourceKey: baseline.NonSourceKey, WorkerKey: baseline.WorkerKey, Files: baseline.Files,
	}); err != nil {
		t.Fatalf("failed to record the tampered baseline: %v", err)
	}
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")

	outcome := project.run(t, recorder, false)

	assertFallback(t, outcome, recorder, "baseline record does not match its compile result")
	if project.baseline(t).InputKey == "tampered" {
		t.Error("expected the disagreeing baseline to be replaced")
	}
}

// Verifies that cancelling the run during the changed-files check stops the run and leaves the
// outputs, manifest, and baseline as they were.
func TestCancelledFastPathLeavesBaselineUntouched(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	before := project.outputSnapshot(t)
	project.editBody(t, 0, "edited")
	ctx, cancel := context.WithCancel(context.Background())
	project.worker.onCheck = cancel
	project.worker.checkErr = context.Canceled

	_, err := project.runWithContext(ctx, newCachedSchedulerRecorder("Alpha", "Beta"), false)

	if !errors.Is(err, context.Canceled) {
		t.Fatalf("expected the run to stop with a cancellation, got %v", err)
	}
	if after := project.outputSnapshot(t); !reflect.DeepEqual(after, before) {
		t.Errorf("expected the outputs, manifest, and baseline to stay, got %v then %v", before, after)
	}
}

// Verifies that a reason the worker gives with absolute paths reaches the output with the paths
// spelled relative to the project root, so no local path leaks into it.
func TestFallbackReasonHasNoAbsolutePath(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	project.editBody(t, 0, "edited")
	project.worker.outcome = fastcheck.UnsupportedOutcome
	project.worker.reason = "changed file is not a source: " + project.absoluteSource(0) + " near " +
		filepath.Join(project.root, "Library", "Other.cs")
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")

	outcome := project.run(t, recorder, false)

	reason := resultOf(t, outcome.Units, "Alpha").FastPathFallbackReason
	if strings.Contains(reason, project.root) {
		t.Errorf("expected no absolute path in the reason, got %q", reason)
	}
	assertFallback(t, outcome, recorder, "Unsupported: changed file is not a source: "+project.sourceName(0)+" near Library")
}

// Verifies that an assembly whose baseline was removed because a reference failed and blocked it
// falls back without claiming it was never compiled in full, since nothing on disk tells the two apart.
func TestBaselineRemovedByBlockedRunIsNotCalledNeverCompiled(t *testing.T) {
	project := &fastPathProject{
		cachedSchedulerProject: newCachedSchedulerProject(t, map[string][]string{"Beta": {"Alpha"}}, "Alpha", "Beta"),
		worker:                 newFakeFastCheckWorker(),
	}
	sources := make([]string, 0, fastPathMinimumSources)
	for index := 0; index < fastPathMinimumSources; index++ {
		source := fmt.Sprintf("Assets/Beta/File%03d.cs", index)
		project.writeFile(t, source, fmt.Sprintf("class File%03d %s body", index, fakeDeclarationSeparator))
		sources = append(sources, source)
	}
	project.plan.Units[1].Sources = sources
	project.run(t, newCachedSchedulerRecorder("Alpha", "Beta"), false)
	if !project.hasBaselineOf("Beta") {
		t.Fatal("expected the first run to record Beta's baseline")
	}

	project.editSource(t, "Alpha", "class Alpha { broken }")
	failing := newCachedSchedulerRecorder("Alpha", "Beta")
	failing.output["Alpha"] = failingCompilerOutput("Alpha")
	assertStrings(t, "blocked", project.run(t, failing, false).Blocked, []string{"Beta"})

	project.editSource(t, "Alpha", "class Alpha { }")
	project.writeFile(t, sources[0], fmt.Sprintf("class File000 %s edited", fakeDeclarationSeparator))
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")
	outcome := project.run(t, recorder, false)

	if reason := resultOf(t, outcome.Units, "Beta").FastPathFallbackReason; reason != "no baseline: no full compile of this assembly is on record" {
		t.Errorf("Beta fallback reason = %q", reason)
	}
}

// Verifies that a unit whose non-source key could not be read before the run falls back with a
// reason of its own rather than listing every kind of input as changed.
func TestUnreadableNonSourceKeyFallsBack(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	project.editBody(t, 0, "edited")
	makeOutputDirectory(t, project.root, project.plan)
	unit := project.plan.Units[0]
	keyResult, err := computeInputKey(project.root, project.paths, project.plan, unit)
	if err != nil {
		t.Fatalf("expected a key, got error: %v", err)
	}
	compiler := Compiler{Paths: project.paths, ProjectRoot: project.root}

	attempt := project.runner(false).Try(context.Background(), compiler, project.plan, unit, keyResult, nonSourceIdentity{})

	if attempt.Used || attempt.Reason != "inputs other than source contents could not be read" {
		t.Errorf("expected the unreadable-inputs fallback, got %+v", attempt)
	}
}
