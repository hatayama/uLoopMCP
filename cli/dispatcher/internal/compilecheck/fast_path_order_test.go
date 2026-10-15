package compilecheck

import (
	"context"
	"os"
	"path/filepath"
	"testing"
)

// recordBaselineWithGeneratorOutput runs the plan once with a generator that writes one file into
// Alpha's generator output, and checks the baseline recorded it.
// Why the baseline needs a generated file: with none recorded, an emptied generator output still
// matches the baseline, and the check that it is gone could run anywhere in the order unnoticed.
func (project *fastPathProject) recordBaselineWithGeneratorOutput(t *testing.T) string {
	t.Helper()
	generatedDirectoryPath := generatedDirectory(project.plan, project.plan.Units[0])
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")
	recorder.during = func(unit string) {
		if unit == "Alpha" {
			project.writeFile(t, filepath.Join(generatedDirectoryPath, "Gen", "G.g.cs"), "class G { }")
		}
	}
	project.run(t, recorder, false)
	if record := project.readBaseline(t); len(record.GeneratedFiles) != 1 {
		t.Fatalf("expected the generated file in the baseline, got %v", record.GeneratedFiles)
	}

	return filepath.Join(project.root, generatedDirectoryPath)
}

// removeGeneratorOutput deletes Alpha's saved generator output.
func removeGeneratorOutput(t *testing.T, generatedPath string) {
	t.Helper()
	if err := os.RemoveAll(generatedPath); err != nil {
		t.Fatalf("failed to remove the generator output: %v", err)
	}
}

// addAlphaSource adds a source to Alpha, which changes its non-source key through the source list.
func (project *fastPathProject) addAlphaSource(t *testing.T) {
	t.Helper()
	added := "Assets/Alpha/Added.cs"
	project.writeFile(t, added, "class Added | body")
	project.plan.Units[0].Sources = append(project.plan.Units[0].Sources, added)
}

// tryAlpha calls the changed-files check on Alpha directly with the given non-source key.
// Why not through a run: with every source unchanged the input key matches the last compile, and
// reuse would answer before the check is tried.
func (project *fastPathProject) tryAlpha(t *testing.T, nonSourceKey nonSourceIdentity) fastPathAttempt {
	t.Helper()
	makeOutputDirectory(t, project.root, project.plan)
	unit := project.plan.Units[0]
	keyResult, err := computeInputKey(project.root, project.paths, project.plan, unit)
	if err != nil {
		t.Fatalf("expected a key, got error: %v", err)
	}
	compiler := Compiler{Paths: project.paths, ProjectRoot: project.root}

	return project.runner(false).Try(context.Background(), compiler, project.plan, unit, keyResult, nonSourceKey)
}

// Verifies a changed non-source input is reported ahead of a missing generator output.
func TestFastPathReportsAChangedNonSourceInputBeforeAMissingGeneratorOutput(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	generatedPath := project.recordBaselineWithGeneratorOutput(t)
	project.addAlphaSource(t)
	removeGeneratorOutput(t, generatedPath)
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")

	outcome := project.run(t, recorder, false)

	assertFallback(t, outcome, recorder, "inputs other than source contents changed:")
}

// Verifies a baseline whose compile outputs were changed by hand is reported ahead of a changed
// non-source input.
func TestFastPathReportsAGoneBaselineCompileBeforeAChangedNonSourceInput(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	project.editBody(t, 0, "edited")
	writeOutputFile(t, project.outputDirectory(), "Alpha"+assemblyExtension, "changed by hand")
	project.addAlphaSource(t)
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")

	outcome := project.run(t, recorder, false)

	assertFallback(t, outcome, recorder, "baseline compile is no longer on disk")
}

// Verifies a missing generator output is reported ahead of too many changed files.
func TestFastPathReportsAMissingGeneratorOutputBeforeTooManyChangedFiles(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	generatedPath := project.recordBaselineWithGeneratorOutput(t)
	removeGeneratorOutput(t, generatedPath)
	for index := 0; index <= fastPathMaximumChangedFiles; index++ {
		project.editBody(t, index, "edited")
	}
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")

	outcome := project.run(t, recorder, false)

	assertFallback(t, outcome, recorder, "generator output of the baseline is gone")
}

// Verifies a missing generator output is reported ahead of a baseline that agrees with every source.
func TestFastPathReportsAMissingGeneratorOutputBeforeNoChangedFile(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	generatedPath := project.recordBaselineWithGeneratorOutput(t)
	removeGeneratorOutput(t, generatedPath)
	nonSourceKey, err := computeNonSourceKey(project.root, project.paths, project.plan, project.plan.Units[0])
	if err != nil {
		t.Fatalf("expected a non-source key, got error: %v", err)
	}

	attempt := project.tryAlpha(t, nonSourceKey)

	if attempt.Used || attempt.Reason != "generator output of the baseline is gone" {
		t.Errorf("expected the missing generator output fallback, got %+v", attempt)
	}
}

// Verifies a missing baseline is reported ahead of a non-source key that could not be read.
func TestFastPathReportsAMissingBaselineBeforeAnUnreadableNonSourceKey(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)

	attempt := project.tryAlpha(t, nonSourceIdentity{})

	if attempt.Used || attempt.Reason != "no baseline: no full compile of this assembly is on record" {
		t.Errorf("expected the missing baseline fallback, got %+v", attempt)
	}
}
