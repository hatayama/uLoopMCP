package compilecheck

import (
	"os"
	"path/filepath"
	"testing"
	"time"
)

// recordMayStillApply asks whether the named unit's record may still apply under plan, with the
// named references still compiling: the unit is shaped the way it would start then, reading Unity's
// reference assembly for each of them.
func (project *cachedSchedulerProject) recordMayStillApply(
	t *testing.T, plan BuildPlan, name string, unknownNames ...string,
) bool {
	t.Helper()
	unknown := map[string]bool{}
	for _, unknownName := range unknownNames {
		unknown[unknownName] = true
	}
	for _, unit := range plan.Units {
		if unit.Assembly.AssemblyName != name {
			continue
		}
		unit.KeptSurfaceReferences = unknown
		compiler := Compiler{Paths: project.paths, ProjectRoot: project.root, Timeout: time.Minute}

		return compiler.recordMayStillApply(plan, unit, unknown)
	}
	t.Fatalf("the plan has no unit %s", name)

	return false
}

// newChangedReferenceProject compiles [X, D], D referencing X, where X changed its surface, so D's
// record holds the identity of the reference assembly this run produced for X.
func newChangedReferenceProject(t *testing.T) *cachedSchedulerProject {
	t.Helper()
	project := newCachedSchedulerProject(t, map[string][]string{"D": {"X"}}, "X", "D")
	project.compileOrFail(t, newCachedSchedulerRecorder("X", "D"))

	return project
}

// Verifies a record holding the produced identity of a reference still may apply while that
// reference is unknown - the next start reads Unity's file for it - even after the produced file
// changed, and stops applying once the unit's own sources change.
func TestCompileUnitsRecordsAKeyAUnitCanStillApplyLater(t *testing.T) {
	project := newChangedReferenceProject(t)

	if !project.recordMayStillApply(t, project.plan, "D", "X") {
		t.Fatal("the record should still apply while X is unknown")
	}

	writeOutputFile(t, project.outputDirectory(), "X"+referenceAssemblyExtension, "another surface X")
	if !project.recordMayStillApply(t, project.plan, "D", "X") {
		t.Error("the record should still apply while X is unknown, whatever X's output holds")
	}

	project.editSource(t, "D", "class D { int Added; }")
	if project.recordMayStillApply(t, project.plan, "D", "X") {
		t.Error("the record should not apply once D's own sources changed")
	}
}

// Verifies a record that read an unknown reference from Unity's build stops applying once Unity
// rebuilt that file: whatever the reference finishes as, the key can never read the recorded line
// again.
func TestRecordMayStillApplyRefusesAnUnknownReferenceUnityRebuilt(t *testing.T) {
	project := newCachedSchedulerProject(t, map[string][]string{"D": {"X"}}, "X", "D")
	recorder := newCachedSchedulerRecorder("X", "D")
	recorder.surfaces["X"] = keptSurfaceOf("X")
	project.compileOrFail(t, recorder)
	if !project.recordMayStillApply(t, project.plan, "D", "X") {
		t.Fatal("the record should still apply while Unity's file for X is as it read it")
	}

	rebuilt := time.Now().Add(time.Hour)
	unityReference := filepath.Join(project.root, planDagDirectory, "X"+referenceAssemblyExtension)
	if err := os.Chtimes(unityReference, rebuilt, rebuilt); err != nil {
		t.Fatalf("failed to move Unity's reference assembly of X: %v", err)
	}

	if project.recordMayStillApply(t, project.plan, "D", "X") {
		t.Error("the record should not apply once Unity rebuilt the reference assembly it read for X")
	}
}

// Verifies a record still may apply after a reference that kept Unity's surface left the plan: the
// compile reads the same file for it either way, so neither the key nor its parts move.
func TestMayStillApplyWhenAReferenceLeftThePlan(t *testing.T) {
	project := newCachedSchedulerProject(t, map[string][]string{
		"D1": {"X"}, "D2": {"X", "D1", "W"},
	}, "X", "D1", "D2", "W")
	recorder := newCachedSchedulerRecorder("X", "D1", "D2", "W")
	recorder.surfaces["W"] = keptSurfaceOf("W")
	project.compileOrFail(t, recorder)

	if !project.recordMayStillApply(t, project.withOnlyUnits("X", "D1", "D2"), "D2", "D1") {
		t.Error("the record should still apply after the kept reference W left the plan")
	}
}

// Verifies --all never waits on a record: it does not replay one.
func TestRecordMayStillApplyIsFalseForAll(t *testing.T) {
	project := newChangedReferenceProject(t)
	if !project.recordMayStillApply(t, project.plan, "D", "X") {
		t.Fatal("the record should still apply in an ordinary run")
	}

	all := project.plan
	all.All = true

	if project.recordMayStillApply(t, all, "D", "X") {
		t.Error("a run with --all should not wait on a record")
	}
}

// Verifies a unit whose key cannot be computed never waits on a record: it could not replay one.
func TestRecordMayStillApplyIsFalseWhenTheKeyCannotBeComputed(t *testing.T) {
	project := newChangedReferenceProject(t)
	if !project.recordMayStillApply(t, project.plan, "D", "X") {
		t.Fatal("the record should still apply while every input can be read")
	}

	if err := os.Remove(filepath.Join(project.root, "Assets", "D", "D.cs")); err != nil {
		t.Fatalf("failed to remove D's source: %v", err)
	}

	if project.recordMayStillApply(t, project.plan, "D", "X") {
		t.Error("a unit whose source is gone should not wait on a record")
	}
}
