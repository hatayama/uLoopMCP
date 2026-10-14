package dispatcher

import (
	"bytes"
	"reflect"
	"strings"
	"testing"

	"github.com/hatayama/unity-cli-loop/dispatcher/internal/compilecheck"
)

// Verifies that --full is read and turns the changed-files check off for the run.
func TestFullFlagSetsDisableFastPath(t *testing.T) {
	options, err := parseCompileCheckOptions([]string{compileCheckFullFlag}, "")
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	if runOptions := newCompileCheckRunOptions(options, "/projects/sample", "/editor"); !runOptions.DisableFastPath {
		t.Fatal("expected --full to disable the changed-files check")
	}
	if runOptions := newCompileCheckRunOptions(compileCheckOptions{}, "/projects/sample", "/editor"); runOptions.DisableFastPath {
		t.Fatal("expected the changed-files check to stay on without --full")
	}
}

// Verifies that the help lists --full.
func TestCompileCheckHelpListsFullFlag(t *testing.T) {
	var stdout bytes.Buffer
	printCompileCheckHelp(&stdout)

	if !strings.Contains(stdout.String(), "--full               Compile changed assemblies in full instead of checking only their changed files") {
		t.Fatalf("expected --full in the help, got:\n%s", stdout.String())
	}
}

// fastCheckedResult is a run with one compiled, one reused, and one fast-checked assembly.
func fastCheckedResult() compilecheck.Result {
	return compilecheck.Result{
		DagDir: "Library/Bee/artifacts/1234.dag",
		Units: []compilecheck.UnitResult{
			{
				Assembly: "Compiled", Succeeded: true, FastPathFallbackReason: "too many changed files",
				FastPathNote: "baseline not recorded: source changed during the compile: Assets/A.cs",
			},
			{Assembly: "Reused", Succeeded: true, Reused: true},
			{
				Assembly: "Fast", Succeeded: true, FastChecked: true,
				FastCheckChangedFiles: []string{"Assets/Fast/A.cs"},
				UncheckedCodes:        []string{"CS0169"},
				GeneratorsNotRerun:    true,
			},
		},
	}
}

// Verifies that fast-checked assemblies are listed apart from compiled and reused ones, with what
// each check covered.
func TestResponseListsFastCheckedAssembliesApart(t *testing.T) {
	response := buildCompileCheckResponse(fastCheckedResult(), "/projects/sample")

	assertCompileCheckNames(t, "compiled", response.CompiledAssemblies, []string{"Compiled"})
	assertCompileCheckNames(t, "reused", response.ReusedAssemblies, []string{"Reused"})
	assertCompileCheckNames(t, "fast-checked", response.FastCheckedAssemblies, []string{"Fast"})
	want := []compileCheckFastCheck{{
		Assembly: "Fast", ChangedFiles: []string{"Assets/Fast/A.cs"}, UncheckedCodes: []string{"CS0169"}, GeneratorsNotRerun: true,
	}}
	if !reflect.DeepEqual(response.FastChecks, want) {
		t.Errorf("fast checks = %+v, want %+v", response.FastChecks, want)
	}
}

// Verifies that only units that fell back with a reason are listed as fallbacks.
func TestResponseListsFallbackReasons(t *testing.T) {
	response := buildCompileCheckResponse(fastCheckedResult(), "/projects/sample")

	want := []compileCheckFastPathFallback{{Assembly: "Compiled", Reason: "too many changed files"}}
	if !reflect.DeepEqual(response.FastPathFallbacks, want) {
		t.Errorf("fallbacks = %+v, want %+v", response.FastPathFallbacks, want)
	}
}

// Verifies that the message counts the fast-checked assemblies and asks for --full before finishing.
func TestMessageMentionsFastCheckedAssemblies(t *testing.T) {
	response := buildCompileCheckResponse(fastCheckedResult(), "/projects/sample")

	want := "Compiled 1 assemblies, reused 1 and fast-checked 1 with 0 errors and 0 warnings." +
		" 1 assemblies were checked on their changed files only; run with --full before finishing to cover assembly-wide rules."
	if response.Message != want {
		t.Errorf("message = %q, want %q", response.Message, want)
	}
}

// Verifies that a run answered only by the changed-files check, with an error, reports the error
// count instead of saying no assemblies changed.
func TestFastCheckOnlyRunWithErrorDoesNotSayNoAssembliesChanged(t *testing.T) {
	response := buildCompileCheckResponse(compilecheck.Result{
		DagDir: "Library/Bee/artifacts/1234.dag",
		Units: []compilecheck.UnitResult{{
			Assembly: "Fast", FastChecked: true,
			Diagnostics: []compilecheck.Diagnostic{{Severity: "error", Code: "CS0029", Message: "cannot convert", File: "Assets/Fast/A.cs", Line: 3, Column: 9}},
		}},
	}, "/projects/sample")

	if strings.Contains(response.Message, compileCheckNoChangeMessage) || !strings.HasPrefix(response.Message, "Fast-checked 1 assemblies with 1 errors and 0 warnings.") {
		t.Errorf("unexpected message %q", response.Message)
	}
	if response.ErrorCount != 1 || response.Success {
		t.Errorf("expected one error and a failed run, got %d errors and success %t", response.ErrorCount, response.Success)
	}
}

func assertCompileCheckNames(t *testing.T, label string, got []string, want []string) {
	t.Helper()
	if !reflect.DeepEqual(got, want) {
		t.Errorf("%s = %v, want %v", label, got, want)
	}
}

// Verifies that a baseline that was not recorded is reported with its reason, so the next run's
// fallback for lack of a baseline can be traced back.
func TestResponseListsBaselineNotes(t *testing.T) {
	response := buildCompileCheckResponse(fastCheckedResult(), "/projects/sample")

	want := []compileCheckFastPathFallback{{
		Assembly: "Compiled", Reason: "baseline not recorded: source changed during the compile: Assets/A.cs",
	}}
	if !reflect.DeepEqual(response.FastPathBaselineNotes, want) {
		t.Errorf("baseline notes = %+v, want %+v", response.FastPathBaselineNotes, want)
	}
}
