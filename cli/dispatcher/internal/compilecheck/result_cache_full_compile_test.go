package compilecheck

import (
	"context"
	"os/exec"
	"slices"
	"testing"
	"time"
)

// fullCompileMarks runs one unit through reuseOrCompile with a hook and a compiler that both note
// when they are called, in the order they are.
func fullCompileMarks(
	t *testing.T, project *cachedSchedulerProject, fastPath *fastPathRunner, name string,
) (UnitResult, []string) {
	t.Helper()
	makeOutputDirectory(t, project.root, project.plan)
	marks := []string{}
	compile := newCachedSchedulerRecorder(name).runner()
	compiler := Compiler{
		Paths:       project.paths,
		ProjectRoot: project.root,
		Timeout:     time.Minute,
		Run: func(ctx context.Context, cmd *exec.Cmd) (string, string, int, error) {
			marks = append(marks, "compile")

			return compile(ctx, cmd)
		},
		FastPath:      fastPath,
		onFullCompile: func() { marks = append(marks, "full compile") },
	}
	result, err := compiler.reuseOrCompile(context.Background(), project.plan, project.plan.Units[planIndexOf(t, project.plan, name)])
	if err != nil {
		t.Fatalf("expected %s to produce a result, got error: %v", name, err)
	}

	return result, marks
}

// Verifies the full-compile hook runs once, before csc starts, and never for a unit whose result is
// replayed from its record or taken from a check of its changed files.
func TestReuseOrCompileCallsOnFullCompileOnlyBeforeACompile(t *testing.T) {
	t.Run("full compile", func(t *testing.T) {
		project := newCachedSchedulerProject(t, nil, "Alpha")

		_, marks := fullCompileMarks(t, project, nil, "Alpha")

		if !slices.Equal(marks, []string{"full compile", "compile"}) {
			t.Errorf("marks = %v, want the hook once before the compile", marks)
		}
	})
	t.Run("replayed", func(t *testing.T) {
		project := newCachedSchedulerProject(t, nil, "Alpha")
		fullCompileMarks(t, project, nil, "Alpha")

		result, marks := fullCompileMarks(t, project, nil, "Alpha")

		if !result.Reused || len(marks) != 0 {
			t.Errorf("reused = %v, marks = %v; want a replay with no hook", result.Reused, marks)
		}
	})
	t.Run("fast check", func(t *testing.T) {
		project := newEditedFastPathProject(t)

		result, marks := fullCompileMarks(t, project.cachedSchedulerProject, project.runner(false), "Alpha")

		if !result.FastChecked || len(marks) != 0 {
			t.Errorf("fast checked = %v, marks = %v; want a fast check with no hook", result.FastChecked, marks)
		}
	})
}
