package compilecheck

import (
	"context"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"testing"
	"time"
)

// Verifies a unit without a reference assembly names its assembly alone as an output.
func TestUnitOutputNamesSkipsAnAbsentReferenceAssemblyPath(t *testing.T) {
	unit := CompileUnit{Assembly: ResponseFile{
		AssemblyName: "Alpha",
		OutputPath:   filepath.Join(planDagDirectory, "Alpha"+assemblyExtension),
	}}

	names := unitOutputNames(unit)

	if len(names) != 1 || names[0] != "Alpha"+assemblyExtension {
		t.Fatalf("names=%v", names)
	}
}

// Verifies removing a unit's outputs removes both outputs and leaves no record of the compile that
// wrote them: no compile result, no baseline, and no saved generator output.
func TestRemoveUnitOutputsLeavesNoRecords(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	generatedPath := filepath.Join(project.root, project.records().GeneratedDirectory("Alpha"))
	if _, err := os.Stat(generatedPath); err != nil {
		t.Fatalf("expected the compile to leave its generator output directory: %v", err)
	}
	compiler := Compiler{Paths: project.paths, ProjectRoot: project.root}

	if err := compiler.removeUnitOutputs(project.plan, project.plan.Units[0]); err != nil {
		t.Fatalf("expected the outputs to be removed, got error: %v", err)
	}

	for _, name := range unitOutputNames(project.plan.Units[0]) {
		if _, err := os.Stat(filepath.Join(project.outputDirectory(), name)); !os.IsNotExist(err) {
			t.Errorf("expected %s to be removed, got %v", name, err)
		}
	}
	if project.hasRecordedCompile("Alpha") || project.hasBaseline() {
		t.Error("expected no compile result and no baseline after the outputs were removed")
	}
	if _, err := os.Stat(generatedPath); !os.IsNotExist(err) {
		t.Errorf("expected the generator output to be removed, got %v", err)
	}
}

// Verifies a compile whose result cannot be recorded fails the run rather than reporting a result
// the next run could not reuse.
func TestCompileUnitsFailsWhenAResultCannotBeRecorded(t *testing.T) {
	project := newCachedSchedulerProject(t, nil, "Alpha")
	recorder := newCachedSchedulerRecorder("Alpha")
	recorder.output["Alpha"] = failingCompilerOutput("Alpha")
	compileRunner := recorder.runner()
	compiler := Compiler{
		Paths:       project.paths,
		ProjectRoot: project.root,
		Timeout:     time.Minute,
		// Why the output directory goes during the compile: the compile failed, so its missing outputs
		// are allowed, and only writing the record finds nowhere to go.
		Run: func(ctx context.Context, cmd *exec.Cmd) (string, string, int, error) {
			stdout, stderr, exitCode, err := compileRunner(ctx, cmd)
			if removeErr := os.RemoveAll(project.outputDirectory()); removeErr != nil {
				t.Errorf("failed to remove the output directory: %v", removeErr)
			}
			return stdout, stderr, exitCode, err
		},
	}

	_, err := compileUnits(context.Background(), compiler, project.plan, 1)

	if err == nil || !strings.HasPrefix(err.Error(), "failed to write "+project.outputDirectory()+string(filepath.Separator)) {
		t.Fatalf("err = %v, want the record write failure", err)
	}
}
