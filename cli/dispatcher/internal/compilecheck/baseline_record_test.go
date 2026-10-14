package compilecheck

import (
	"context"
	"errors"
	"os"
	"path/filepath"
	"strings"
	"testing"

	"github.com/hatayama/unity-cli-loop/dispatcher/internal/compilecheck/fastcheck"
)

// Verifies that a successful full compile records a baseline whose content hashes are the ones the
// input key read and whose input key is the manifest's.
func TestBaselineRecordedAfterSuccessfulCompile(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)

	project.run(t, newCachedSchedulerRecorder("Alpha", "Beta"), false)

	record := project.readBaseline(t)
	keyResult, err := computeInputKey(project.root, project.paths, project.plan, project.plan.Units[0])
	if err != nil {
		t.Fatalf("expected a key, got error: %v", err)
	}
	if record.InputKey != project.readManifest(t).InputKey || record.InputKey != keyResult.Key {
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

	project.run(t, newCachedSchedulerRecorder("Alpha", "Beta"), false)

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
	want := quoteFlag(generatedFilesOutFlagPrefix, filepath.ToSlash(generatedDirectory(plan, plan.Units[1])))
	if !strings.Contains(written, want) {
		t.Errorf("expected %s in the response file, got:\n%s", want, written)
	}
}

// Verifies that removing a unit's outputs also removes its baseline and its saved generator output.
func TestRemoveUnitOutputsRemovesBaselineAndGeneratedDirectory(t *testing.T) {
	outputDirectoryPath, unit := newManifestUnit(t)
	writeOutputFile(t, outputDirectoryPath, "Alpha"+baselineRecordExtension, "{}")
	generatedPath := filepath.Join(outputDirectoryPath, "Alpha"+generatedDirectoryExtension, "Nested")
	if err := os.MkdirAll(generatedPath, outputDirPermissions); err != nil {
		t.Fatalf("failed to create the generated directory: %v", err)
	}
	writeOutputFile(t, generatedPath, "G.cs", "class G {}")

	if err := removeUnitOutputs(outputDirectoryPath, unit); err != nil {
		t.Fatalf("expected the outputs to be removed, got error: %v", err)
	}

	for _, name := range []string{"Alpha" + baselineRecordExtension, "Alpha" + generatedDirectoryExtension} {
		if _, err := os.Stat(filepath.Join(outputDirectoryPath, name)); !os.IsNotExist(err) {
			t.Errorf("expected %s to be removed, got %v", name, err)
		}
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
