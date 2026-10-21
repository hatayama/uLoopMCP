package compilecheck

import (
	"context"
	"errors"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"testing"
	"time"

	"github.com/hatayama/unity-cli-loop/dispatcher/internal/compilecheck/fastcheck"
	"github.com/hatayama/unity-cli-loop/dispatcher/internal/compilecheck/resultstore"
)

// Verifies that a successful full compile records a baseline whose content hashes are the ones the
// input key read and whose input key is the manifest's.
func TestBaselineRecordedAfterSuccessfulCompile(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)

	project.run(t, newCachedSchedulerRecorder("Alpha", "Beta"), false)

	record := project.baseline(t)
	keyResult, err := computeInputKey(project.root, project.paths, project.plan, project.plan.Units[0])
	if err != nil {
		t.Fatalf("expected a key, got error: %v", err)
	}
	// Why a usable baseline shows it carries the manifest's input key: the store hands one back only
	// when the two keys agree.
	if record.InputKey != keyResult.Key {
		t.Errorf("expected the baseline to carry the manifest's input key, got %s", record.InputKey)
	}
	for source, hash := range keyResult.SourceHashes {
		if record.Files[source].ContentHash != hash {
			t.Fatalf("expected the content hash of %s to be %s, got %+v", source, hash, record.Files[source])
		}
	}
}

// Verifies that a failed full compile records no baseline.
func TestBaselineNotRecordedForFailedCompile(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")
	recorder.output["Alpha"] = failingCompilerOutput("Alpha")

	project.run(t, recorder, false)

	if project.hasBaseline() || project.worker.fingerprints != 0 {
		t.Errorf("expected no baseline after a failed compile, got %d fingerprint calls", project.worker.fingerprints)
	}
}

// Verifies that a unit picked only as a dependent records no baseline.
func TestBaselineNotRecordedForDependentUnit(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)

	project.compileAlphaAsDependent(t)

	if project.hasBaseline() || project.worker.fingerprints != 0 {
		t.Error("expected no baseline for a unit picked only as a dependent")
	}
}

// Verifies that an assembly with fewer sources than the minimum records no baseline.
func TestBaselineNotRecordedForSmallAssembly(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources-1)
	// Why Alpha keeps Unity's reference assembly: a differing one is compared by the worker, which
	// would build it for a reason other than the changed-files check.
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")
	recorder.surfaces["Alpha"] = keptSurfaceOf("Alpha")

	project.run(t, recorder, false)

	if project.hasBaseline() || project.worker.ensures != 0 {
		t.Errorf("expected no baseline and no worker for a small assembly, got %d builds", project.worker.ensures)
	}
}

// Verifies that a baseline that cannot be recorded leaves a note and does not fail the run.
func TestBaselineFailureDoesNotFailRun(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.worker.ensureErr = errors.New("csc failed")

	outcome := project.run(t, newCachedSchedulerRecorder("Alpha", "Beta"), false)

	result := resultOf(t, outcome.Units, "Alpha")
	if !result.Succeeded || result.FastPathNote != "baseline not recorded: csc failed" {
		t.Errorf("expected a successful compile with a note, got %+v", result)
	}
	if project.hasBaseline() {
		t.Error("expected no baseline when the worker is unavailable")
	}
}

// Verifies that a source edited while csc compiled it leaves no baseline, and says why.
func TestBaselineNotRecordedWhenSourceChangedDuringCompile(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")
	recorder.during = func(name string) {
		if name == "Alpha" {
			project.editBody(t, 0, "edited during the compile")
		}
	}

	outcome := project.run(t, recorder, false)

	result := resultOf(t, outcome.Units, "Alpha")
	if !strings.Contains(result.FastPathNote, "source changed during the compile: "+project.sourceName(0)) {
		t.Errorf("expected a note naming the edited source, got %q", result.FastPathNote)
	}
	if project.hasBaseline() {
		t.Error("expected no baseline when a source changed during the compile")
	}
}

// Verifies that a successful full compile under --full still records a baseline.
func TestBaselineRecordedUnderFullFlag(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)

	project.run(t, newCachedSchedulerRecorder("Alpha", "Beta"), true)

	if !project.hasBaseline() {
		t.Error("expected --full to record a baseline")
	}
}

// Verifies that the full compile writes its generator output where the baseline points.
func TestCompileUnitPassesGeneratedFilesOut(t *testing.T) {
	written, _ := compileSecondUnit(t, "", 0)

	plan := newCompilerPlan()
	want := quoteFlag(generatedFilesOutFlagPrefix, filepath.ToSlash(resultstore.Open[Diagnostic]("", plan.OutputDir).GeneratedDirectory(plan.Units[1].Assembly.AssemblyName)))
	if !strings.Contains(written, want) {
		t.Errorf("expected %s in the response file, got:\n%s", want, written)
	}
}

// Verifies that the worker build is shared by later callers, and that a cancelled build is tried again.
func TestOnceWithContextSharesTheAnswerButNotACancellation(t *testing.T) {
	calls := 0
	ensure := onceWithContext(func(ctx context.Context) (fastcheck.WorkerPaths, error) {
		calls++
		if calls == 1 {
			return fastcheck.WorkerPaths{}, context.Canceled
		}

		return fastcheck.WorkerPaths{Key: "built"}, nil
	})

	if _, err := ensure(context.Background()); !errors.Is(err, context.Canceled) {
		t.Fatalf("expected the first call to be cancelled, got %v", err)
	}
	for range 2 {
		paths, err := ensure(context.Background())
		if err != nil || paths.Key != "built" {
			t.Fatalf("expected the built worker, got %+v and %v", paths, err)
		}
	}
	if calls != 2 {
		t.Errorf("expected two builds, got %d", calls)
	}
}

// Verifies that the worker the run builds is built at most once, even when the build fails, and that
// a failing worker call is an error.
func TestBootstrappedWorkerBuildsAtMostOncePerRun(t *testing.T) {
	root := t.TempDir()
	starts := 0
	run := func(_ context.Context, _ *exec.Cmd) (string, string, int, error) {
		starts++
		return "", "", 0, errors.New("fork/exec dotnet: resource temporarily unavailable")
	}
	worker := newBootstrappedWorker(
		EditorCompilerPaths{DotnetHostPath: "/dotnet", CompilerDllPath: "/csc.dll", CompilerDirectory: root, SharedFrameworkPath: root},
		filepath.Join(root, "cache"), root, run)

	for range 2 {
		if _, err := worker.Ensure(context.Background()); err == nil {
			t.Fatal("expected the worker build to fail")
		}
	}
	if starts != 1 {
		t.Fatalf("expected one build attempt per run, got %d", starts)
	}

	paths := fastcheck.WorkerPaths{Key: "k", DllPath: "/worker.dll", DotnetHostPath: "/dotnet"}
	if _, err := worker.Fingerprint(context.Background(), paths, fastcheck.FingerprintRequest{}); err == nil {
		t.Error("expected a failing fingerprint call to be an error")
	}
	if _, err := worker.Check(context.Background(), paths, fastcheck.CheckRequest{}); err == nil {
		t.Error("expected a failing check call to be an error")
	}
	if _, err := worker.CompareSurface(context.Background(), paths, fastcheck.CompareSurfaceRequest{}); err == nil {
		t.Error("expected a failing surface comparison to be an error")
	}
}

// Verifies that a worker diagnostic in a file that is not a source of the assembly, such as saved
// generator output, is spelled relative to the project root.
func TestMergeDiagnosticsSpellsOtherFilesRelativeToTheProjectRoot(t *testing.T) {
	projectRoot := t.TempDir()
	unit := CompileUnit{Sources: []string{"Assets/A.cs"}}
	fresh := []fastcheck.WorkerDiagnostic{{
		File: filepath.Join(projectRoot, "Library", "Gen", "G.cs"), Line: 1, Column: 1, Severity: severityWarning, Code: "CS0168",
	}}

	merged := mergeDiagnostics(projectRoot, unit, nil, fresh, []string{"Assets/A.cs"})

	if len(merged) != 1 || merged[0].File != "Library/Gen/G.cs" {
		t.Errorf("expected the generated file relative to the project root, got %+v", merged)
	}
}

// Verifies that the directory csc saves generator output to exists before csc starts, because the
// csc of Roslyn 4.3 creates the per-generator folders under it but not the directory itself.
func TestCompileUnitCreatesGeneratedFilesOutDirectory(t *testing.T) {
	projectRoot := t.TempDir()
	plan := newCompilerPlan()
	makeOutputDirectory(t, projectRoot, plan)
	writeFileAt(t, filepath.Join(projectRoot, plan.OutputDir, "A.ref.dll"), "")
	existed := false
	compiler := Compiler{
		Paths:       EditorCompilerPaths{DotnetHostPath: "/dotnet", CompilerDllPath: "/csc.dll"},
		ProjectRoot: projectRoot,
		Timeout:     time.Minute,
		Run: func(_ context.Context, _ *exec.Cmd) (string, string, int, error) {
			info, err := os.Stat(filepath.Join(projectRoot, resultstore.Open[Diagnostic](projectRoot, plan.OutputDir).GeneratedDirectory(plan.Units[1].Assembly.AssemblyName)))
			existed = err == nil && info.IsDir()
			return "", "", 0, nil
		},
	}

	if _, err := compiler.CompileUnit(context.Background(), plan, plan.Units[1]); err != nil {
		t.Fatalf("expected the unit to compile, got error: %v", err)
	}

	if !existed {
		t.Error("expected the generator output directory to exist when csc starts")
	}
}

// Verifies that a baseline is not recorded when the non-source key could not be read before the
// compile, since nothing then says which inputs the compile saw.
func TestRecordBaselineRefusesAMissingNonSourceKey(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	makeOutputDirectory(t, project.root, project.plan)
	unit := project.plan.Units[0]
	keyResult, err := computeInputKey(project.root, project.paths, project.plan, unit)
	if err != nil {
		t.Fatalf("expected a key, got error: %v", err)
	}
	compiler := Compiler{Paths: project.paths, ProjectRoot: project.root}

	err = project.runner(false).RecordBaseline(context.Background(), compiler, project.plan, unit, keyResult, nonSourceIdentity{})

	if err == nil || project.hasBaseline() {
		t.Errorf("expected no baseline without a non-source key, got %v", err)
	}
}
