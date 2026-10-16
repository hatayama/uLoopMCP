package compilecheck

import (
	"context"
	"os/exec"
	"slices"
	"testing"
	"time"
)

// compilerWorkMarks runs one unit through reuseOrCompile with a hook, a compiler and, when given, a
// worker check that all note when they are called, in the order they are.
func compilerWorkMarks(
	t *testing.T, project *cachedSchedulerProject, fastPath *fastPathRunner, name string,
) (UnitResult, []string) {
	t.Helper()
	makeOutputDirectory(t, project.root, project.plan)
	marks := []string{}
	if fastPath != nil {
		fastPath.Worker.(*fakeFastCheckWorker).onCheck = func() { marks = append(marks, "check") }
	}
	compile := newCachedSchedulerRecorder(name).runner()
	compiler := Compiler{
		Paths:       project.paths,
		ProjectRoot: project.root,
		Timeout:     time.Minute,
		Run: func(ctx context.Context, cmd *exec.Cmd) (string, string, int, error) {
			marks = append(marks, "compile")

			return compile(ctx, cmd)
		},
		FastPath:           fastPath,
		beforeCompilerWork: func() { marks = append(marks, "hook") },
	}
	result, err := compiler.reuseOrCompile(context.Background(), project.plan, project.plan.Units[planIndexOf(t, project.plan, name)])
	if err != nil {
		t.Fatalf("expected %s to produce a result, got error: %v", name, err)
	}

	return result, marks
}

// Verifies the compiler-work hook runs once, right before csc starts or the worker checks the changed
// files, and never for a unit whose result is replayed from its record or from the last check of the
// same edit.
func TestReuseOrCompileCallsTheCompilerWorkHookOnlyBeforeACompileOrACheck(t *testing.T) {
	t.Run("full compile", func(t *testing.T) {
		project := newCachedSchedulerProject(t, nil, "Alpha")

		_, marks := compilerWorkMarks(t, project, nil, "Alpha")

		if !slices.Equal(marks, []string{"hook", "compile"}) {
			t.Errorf("marks = %v, want the hook once before the compile", marks)
		}
	})
	t.Run("replayed", func(t *testing.T) {
		project := newCachedSchedulerProject(t, nil, "Alpha")
		compilerWorkMarks(t, project, nil, "Alpha")

		result, marks := compilerWorkMarks(t, project, nil, "Alpha")

		if !result.Reused || len(marks) != 0 {
			t.Errorf("reused = %v, marks = %v; want a replay with no hook", result.Reused, marks)
		}
	})
	t.Run("fast check", func(t *testing.T) {
		project := newEditedFastPathProject(t)

		result, marks := compilerWorkMarks(t, project.cachedSchedulerProject, project.runner(false), "Alpha")

		if !result.FastChecked || result.FastCheckReused || !slices.Equal(marks, []string{"hook", "check"}) {
			t.Errorf("fast checked = %v, reused = %v, marks = %v; want the hook once before the check",
				result.FastChecked, result.FastCheckReused, marks)
		}
	})
	t.Run("fast check replayed", func(t *testing.T) {
		project := newEditedFastPathProject(t)
		compilerWorkMarks(t, project.cachedSchedulerProject, project.runner(false), "Alpha")

		result, marks := compilerWorkMarks(t, project.cachedSchedulerProject, project.runner(false), "Alpha")

		if !result.FastCheckReused || len(marks) != 0 {
			t.Errorf("fast check reused = %v, marks = %v; want a replayed check with no hook", result.FastCheckReused, marks)
		}
	})
}
