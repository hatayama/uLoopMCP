package compilecheck

import (
	"context"
	"os"
	"path/filepath"
	"reflect"
	"slices"
	"strings"
	"testing"

	"github.com/hatayama/unity-cli-loop/dispatcher/internal/compilecheck/fastcheck"
)

// newEditedFastPathProject records a baseline and edits the body of Alpha's first source, with the
// worker reporting an error in it, an unchecked code, and generators it did not rerun - every field
// a check reports holds something other than its zero value.
func newEditedFastPathProject(t *testing.T) *fastPathProject {
	t.Helper()
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	project.editBody(t, 0, "int x = \"s\";")
	project.worker.diagnostics = []fastcheck.WorkerDiagnostic{{
		File: project.absoluteSource(0), Line: 1, Column: 20, Severity: severityError, Code: "CS0029", Message: "cannot convert",
	}}
	project.worker.uncheckedCodes = []string{"CS0169"}
	project.worker.generatorsNotRerun = true

	return project
}

// runFastChecked runs the plan and fails the test unless Alpha was fast-checked without csc.
func (project *fastPathProject) runFastChecked(t *testing.T) UnitResult {
	t.Helper()
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")
	result := resultOf(t, project.run(t, recorder, false).Units, "Alpha")
	if !result.FastChecked || result.Reused || recorder.runs["Alpha"] != 0 {
		t.Fatalf("expected Alpha to be fast-checked without csc, got %+v after %d runs", result, recorder.runs["Alpha"])
	}

	return result
}

// Verifies a second run over the same edit reports the first run's check without calling the worker,
// and reports exactly what that check reported.
func TestFastCheckReplaysTheLastCheckOfTheSameEdit(t *testing.T) {
	project := newEditedFastPathProject(t)
	first := project.runFastChecked(t)
	if first.FastCheckReused || project.worker.checks != 1 {
		t.Fatalf("expected the first run to check, got %+v after %d checks", first, project.worker.checks)
	}

	second := project.runFastChecked(t)

	if !second.FastCheckReused || project.worker.checks != 1 {
		t.Fatalf("expected the second run to replay the check, got %+v after %d checks", second, project.worker.checks)
	}
	if first.Succeeded || second.Succeeded {
		t.Errorf("expected both runs to report the error, got %t then %t", first.Succeeded, second.Succeeded)
	}
	if !hasDiagnosticCode(second.Diagnostics, "CS0029") || !reflect.DeepEqual(second.Diagnostics, first.Diagnostics) {
		t.Errorf("diagnostics = %+v, want the first run's %+v", second.Diagnostics, first.Diagnostics)
	}
	assertStrings(t, "changed files", second.FastCheckChangedFiles, first.FastCheckChangedFiles)
	assertStrings(t, "unchecked codes", second.UncheckedCodes, []string{"CS0169"})
	if !second.GeneratorsNotRerun {
		t.Error("expected the replay to keep the generators the check did not rerun")
	}
}

// Verifies a different edit is checked again, and that check then replaces the recorded one.
func TestFastCheckChecksAgainAfterAnotherEdit(t *testing.T) {
	project := newEditedFastPathProject(t)
	project.runFastChecked(t)
	project.editBody(t, 0, "int y = \"s\";")

	edited := project.runFastChecked(t)
	if edited.FastCheckReused || project.worker.checks != 2 {
		t.Fatalf("expected another edit to be checked, got %+v after %d checks", edited, project.worker.checks)
	}

	again := project.runFastChecked(t)
	if !again.FastCheckReused || project.worker.checks != 2 {
		t.Errorf("expected the new check to be replayed, got %+v after %d checks", again, project.worker.checks)
	}
}

// Verifies a check is not replayed once the worker build changed.
func TestFastCheckDoesNotReplayAfterTheWorkerBuildChanged(t *testing.T) {
	project := newEditedFastPathProject(t)
	project.runFastChecked(t)
	project.worker.key = "worker-b"
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")

	outcome := project.run(t, recorder, false)

	assertFallback(t, outcome, recorder, "baseline was fingerprinted by another worker build")
}

// Verifies a check is not replayed once the generator output it read is gone, so the replay is
// looked up only after everything the check needs has been confirmed.
func TestFastCheckDoesNotReplayWhenTheGeneratorOutputIsGone(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	generatedPath := project.recordBaselineWithGeneratorOutput(t)
	project.editBody(t, 0, "edited")
	project.runFastChecked(t)
	removeGeneratorOutput(t, generatedPath)
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")

	outcome := project.run(t, recorder, false)

	assertFallback(t, outcome, recorder, "generator output of the baseline is gone")
}

// Verifies a full compile leaves no record of the check that came before it: the files the check
// keeps for Alpha are the ones the first full compile left.
func TestFullCompileLeavesNoRecordOfAnEarlierFastCheck(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	recorded := snapshotPaths(project.outputSnapshot(t))
	project.editBody(t, 0, "edited")
	project.runFastChecked(t)

	project.run(t, newCachedSchedulerRecorder("Alpha", "Beta"), true)

	assertStrings(t, "files kept for Alpha", snapshotPaths(project.outputSnapshot(t)), recorded)
}

// Verifies a check whose result cannot be recorded fails the run, as a compile whose result cannot
// be recorded does.
func TestFastCheckFailsTheRunWhenItsResultCannotBeRecorded(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	project.editBody(t, 0, "edited")
	var removeErr error
	project.worker.onCheck = func() { removeErr = os.RemoveAll(project.outputDirectory()) }

	_, err := project.runWithContext(context.Background(), newCachedSchedulerRecorder("Alpha", "Beta"), false)

	if removeErr != nil {
		t.Fatalf("failed to remove the output directory: %v", removeErr)
	}
	want := "failed to write " + project.outputDirectory() + string(filepath.Separator)
	if err == nil || !strings.HasPrefix(err.Error(), want) {
		t.Errorf("err = %v, want it to start with %q", err, want)
	}
}

// Verifies a check of sources edited while it ran is not recorded under the key of the bytes before
// the edit, so putting those bytes back checks them again rather than replaying the edited file.
func TestFastCheckDoesNotRecordACheckOfSourcesEditedDuringIt(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	project.editBody(t, 0, "X")
	var editErr error
	project.worker.onCheck = func() {
		editErr = os.WriteFile(project.absoluteSource(0), []byte(bodyContent(0, "Y")), 0o600)
	}
	project.runFastChecked(t)
	if editErr != nil {
		t.Fatalf("failed to edit the source during the check: %v", editErr)
	}
	if project.worker.checks != 1 {
		t.Fatalf("expected one check, got %d", project.worker.checks)
	}
	project.worker.onCheck = nil
	project.editBody(t, 0, "X")

	result := project.runFastChecked(t)

	if result.FastCheckReused || project.worker.checks != 2 {
		t.Errorf("expected the reverted source to be checked again, got %+v after %d checks", result, project.worker.checks)
	}
}

// hasDiagnosticCode reports whether any of the diagnostics has the code.
func hasDiagnosticCode(diagnostics []Diagnostic, code string) bool {
	for _, diagnostic := range diagnostics {
		if diagnostic.Code == code {
			return true
		}
	}

	return false
}

// snapshotPaths lists the paths of a snapshot in order.
func snapshotPaths(snapshot map[string]string) []string {
	paths := make([]string, 0, len(snapshot))
	for path := range snapshot {
		paths = append(paths, path)
	}
	slices.Sort(paths)

	return paths
}
