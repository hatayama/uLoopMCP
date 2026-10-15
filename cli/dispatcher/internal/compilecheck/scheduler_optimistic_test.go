package compilecheck

import (
	"context"
	"fmt"
	"sort"
	"strings"
	"testing"
	"time"
)

// weightHold is how long the fake compile of an assembly selected for its own sources takes when a
// test needs the dependents below it to stay unstarted while another unit starts.
const weightHold = 60 * time.Millisecond

// optimisticPlanRun is a plan of named units with Unity's reference assembly of each one on disk and
// a fake compiler that, unless told otherwise, writes a reference assembly that differs from it.
// Why Unity's files are written for every unit: the surface verdict compares the two files, and a
// unit without Unity's file never counts as having kept its surface.
type optimisticPlanRun struct {
	plan     BuildPlan
	recorder *schedulerRecorder
	compiler Compiler
}

// newOptimisticPlanRun lays out the plan, marking the named dependents as units this run picks up
// only through a reference.
func newOptimisticPlanRun(
	t *testing.T, references map[string][]string, dependents []string, names ...string,
) *optimisticPlanRun {
	t.Helper()
	plan := newSchedulerPlan(references, names...)
	markSelectedAsDependent(t, plan, dependents...)
	recorder := newSchedulerRecorder(0)
	compiler := newSchedulerCompiler(t, recorder)
	for _, name := range names {
		writeUnityReferenceAssembly(t, compiler.ProjectRoot, name, keptSurfaceOf(name))
		recorder.surfaces[name] = "changed " + name
	}

	return &optimisticPlanRun{plan: plan, recorder: recorder, compiler: compiler}
}

// keep makes the named units come out with Unity's public surface.
func (run *optimisticPlanRun) keep(names ...string) {
	for _, name := range names {
		run.recorder.surfaces[name] = keptSurfaceOf(name)
	}
}

// fail makes the named unit report a compile error and write no reference assembly.
func (run *optimisticPlanRun) fail(name string) {
	run.recorder.output[name] = failingCompilerOutput(name)
	delete(run.recorder.surfaces, name)
}

// compile runs the plan and fails the test when the run could not finish.
func (run *optimisticPlanRun) compile(t *testing.T) compileOutcome {
	t.Helper()
	outcome, err := compileUnits(context.Background(), run.compiler, run.plan, 8)
	if err != nil {
		t.Fatalf("expected the run to succeed, got error: %v", err)
	}

	return outcome
}

// newOptimisticCachedProject lays out a project the input key can read, marking the named dependents
// as units this run picks up only through a reference, and a fake compiler that writes a reference
// assembly differing from Unity's for every unit.
func newOptimisticCachedProject(
	t *testing.T, references map[string][]string, dependents []string, names ...string,
) (*cachedSchedulerProject, *schedulerRecorder) {
	t.Helper()
	project := newCachedSchedulerProject(t, references, names...)
	markSelectedAsDependent(t, project.plan, dependents...)

	return project, newCachedSchedulerRecorder(names...)
}

// chainReferences is the plan most tests here use: X and K are selected for their own sources, D1
// references both, and D2 references X and D1. K takes long enough that D1 has not started when D2
// is ready to.
var chainReferences = map[string][]string{"D1": {"X", "K"}, "D2": {"X", "D1"}}

// sortedNames copies names into ordinal order, for comparing sets of names.
func sortedNames(names []string) []string {
	sorted := append([]string{}, names...)
	sort.Strings(sorted)

	return sorted
}

// warningOutput is what csc prints for an assembly that compiles with one warning.
func warningOutput(name string, message string) fakeCompilerOutput {
	return fakeCompilerOutput{
		stdout: "Assets/" + name + "/" + name + ".cs(1,7): warning CS0168: " + message,
	}
}

// Verifies a dependent that must compile starts as soon as every assembly it references that was
// selected for its own sources finished, reading Unity's reference assembly for the dependent it
// references that has not even started - not the output an earlier run left for it - and keeps that
// compile when the dependent turns out to keep Unity's surface.
func TestCompileUnitsStartsADependentBeforeTheDependentsItReferencesFinish(t *testing.T) {
	project, recorder := newOptimisticCachedProject(t, chainReferences, []string{"D1", "D2"}, "X", "K", "D1", "D2")
	recorder.holdByUnit["K"] = weightHold
	recorder.surfaces["K"] = keptSurfaceOf("K")
	recorder.surfaces["D1"] = keptSurfaceOf("D1")
	writeStaleUnitOutput(t, project.root, project.plan, "D1")

	results := project.compileOrFail(t, recorder)

	assertStrings(t, "units finished when D2 started", sortedNames(recorder.finishedWhen["D2"]), []string{"X"})
	if recorder.runs["D2"] != 1 {
		t.Errorf("D2 compiled %d times, want 1", recorder.runs["D2"])
	}
	assertReferenceFlags(t, project.writtenResponseFile(t, "D2"+responseFileExtension),
		[]string{unityReferenceFlag("D1"), producedReferenceFlag(project.plan, "X")},
		[]string{producedReferenceFlag(project.plan, "D1")})
	assertStrings(t, "results", compiledNames(results), []string{"X", "K", "D1", "D2"})
	assertStrings(t, "D2 assumed", resultOf(t, results, "D2").AssumedReferences, []string{"D1"})
	if again := resultOf(t, results, "D2").CompiledAgainAfter; again != "" {
		t.Errorf("D2 compiled again after %q, want once", again)
	}
}

// Verifies an assembly selected for its own sources starts before a dependent it references
// finishes, too.
func TestCompileUnitsStartsAnEditedAssemblyBeforeTheDependentsItReferencesFinish(t *testing.T) {
	run := newOptimisticPlanRun(t, map[string][]string{"D1": {"X", "K"}, "Y": {"X", "D1"}},
		[]string{"D1"}, "X", "K", "D1", "Y")
	run.recorder.holdByUnit["K"] = weightHold
	run.keep("K", "D1")

	outcome := run.compile(t)

	assertStrings(t, "units finished when Y started", sortedNames(run.recorder.finishedWhen["Y"]), []string{"X"})
	assertStrings(t, "results", compiledNames(outcome.Units), []string{"X", "K", "D1", "Y"})
	assertStrings(t, "Y assumed", resultOf(t, outcome.Units, "Y").AssumedReferences, []string{"D1"})
}

// Verifies a dependent still waits for a reference selected for its own sources: such a reference
// is likely to change its surface, so starting before it would mostly be wasted.
func TestCompileUnitsWaitsForAReferenceWhoseOwnSourcesChanged(t *testing.T) {
	run := newOptimisticPlanRun(t, map[string][]string{"D": {"X", "Y"}}, []string{"D"}, "X", "Y", "D")
	run.recorder.holdByUnit["Y"] = weightHold
	run.keep("Y")

	run.compile(t)

	if !containsLine(run.recorder.finishedWhen["D"], "Y") {
		t.Errorf("D started before Y finished; finished then: %v", run.recorder.finishedWhen["D"])
	}
}

// Verifies a dependent none of whose finished references changed its surface does not start early:
// it may still be skipped once the rest finish, and here it is.
func TestCompileUnitsDoesNotStartADependentBeforeAReferenceIsKnownToHaveChanged(t *testing.T) {
	run := newOptimisticPlanRun(t, map[string][]string{"D1": {"X"}, "D2": {"K", "D1"}},
		[]string{"D1", "D2"}, "X", "K", "D1", "D2")
	run.recorder.holdByUnit["D1"] = weightHold
	run.keep("K", "D1")

	outcome := run.compile(t)

	if run.recorder.started["D2"] {
		t.Error("D2 should not have started before any reference was known to have changed")
	}
	assertStrings(t, "results", compiledNames(outcome.Units), []string{"X", "K", "D1"})
	if outcome.Skipped != 1 {
		t.Errorf("skipped = %d, want 1", outcome.Skipped)
	}
}

// Verifies a dependent blocked by a reference with errors does not start early, although another
// reference it waits for is a dependent still compiling.
func TestCompileUnitsDoesNotStartABlockedDependentOptimistically(t *testing.T) {
	run := newOptimisticPlanRun(t, map[string][]string{"D1": {"X"}, "D2": {"A", "D1"}},
		[]string{"D1", "D2"}, "X", "A", "D1", "D2")
	run.recorder.holdByUnit["D1"] = weightHold
	run.fail("A")
	run.keep("D1")

	outcome := run.compile(t)

	if run.recorder.started["D2"] {
		t.Error("a blocked dependent should not have started")
	}
	assertStrings(t, "blocked", outcome.Blocked, []string{"D2"})
	assertStrings(t, "results", compiledNames(outcome.Units), []string{"X", "A", "D1"})
}

// Verifies a dependent that started before a dependent it references finished is compiled again,
// against this run's output, when that reference changed its surface, and that only the second
// compile's diagnostics are reported.
func TestCompileUnitsCompilesADependentAgainWhenAnAssumedReferenceChangedItsSurface(t *testing.T) {
	project, recorder := newOptimisticCachedProject(t, chainReferences, []string{"D1", "D2"}, "X", "K", "D1", "D2")
	recorder.holdByUnit["K"] = weightHold
	recorder.holdByUnit["D2"] = 5 * time.Millisecond
	recorder.surfaces["K"] = keptSurfaceOf("K")
	recorder.outputByRun["D2"] = []fakeCompilerOutput{
		warningOutput("D2", "first compile"), warningOutput("D2", "second compile"),
	}

	results := project.compileOrFail(t, recorder)

	if recorder.runs["D2"] != 2 {
		t.Fatalf("D2 compiled %d times, want 2", recorder.runs["D2"])
	}
	if !containsLine(recorder.finishedWhen["D2"], "D1") {
		t.Errorf("the second compile of D2 started before D1 finished; finished then: %v", recorder.finishedWhen["D2"])
	}
	assertReferenceFlags(t, project.writtenResponseFile(t, "D2"+responseFileExtension),
		[]string{producedReferenceFlag(project.plan, "D1")}, []string{unityReferenceFlag("D1")})
	d2 := resultOf(t, results, "D2")
	if len(d2.Diagnostics) != 1 || d2.Diagnostics[0].Message != "second compile" {
		t.Errorf("D2 diagnostics = %+v, want only the second compile's warning", d2.Diagnostics)
	}
	if d2.CompiledAgainAfter != "D1" || d2.DiscardedDuration <= 0 || len(d2.AssumedReferences) != 0 {
		t.Errorf("D2 = %+v, want compiled again after D1, with a discarded duration and nothing assumed", d2)
	}
}

// Verifies a dependent whose two assumed references both changed their surface, at different
// times, is compiled twice and no more.
func TestCompileUnitsCompilesAStaleDependentOnlyOnceMore(t *testing.T) {
	run := newOptimisticPlanRun(t, map[string][]string{
		"D1": {"X", "K1"}, "D1b": {"X", "K2"}, "D2": {"X", "D1", "D1b"},
	}, []string{"D1", "D1b", "D2"}, "X", "K1", "K2", "D1", "D1b", "D2")
	run.recorder.holdByUnit["K1"] = 40 * time.Millisecond
	run.recorder.holdByUnit["K2"] = 80 * time.Millisecond
	run.keep("K1", "K2")

	run.compile(t)

	if run.recorder.runs["D2"] != 2 {
		t.Fatalf("D2 compiled %d times, want 2", run.recorder.runs["D2"])
	}
	for _, reference := range []string{"D1", "D1b"} {
		if !containsLine(run.recorder.finishedWhen["D2"], reference) {
			t.Errorf("the last compile of D2 started before %s finished; finished then: %v",
				reference, run.recorder.finishedWhen["D2"])
		}
	}
}

// Verifies the surface verdict of a compile that was thrown away decides nothing: the dependent
// below it compiles again against the output of the compile that was kept.
func TestCompileUnitsIgnoresTheVerdictOfADiscardedCompile(t *testing.T) {
	references := map[string][]string{"D1": {"X", "K"}, "D2": {"X", "D1"}, "D3": {"X", "D2"}}
	project, recorder := newOptimisticCachedProject(t, references, []string{"D1", "D2", "D3"},
		"X", "K", "D1", "D2", "D3")
	recorder.holdByUnit["K"] = weightHold
	recorder.surfaces["K"] = keptSurfaceOf("K")
	recorder.surfacesByRun["D2"] = []string{keptSurfaceOf("D2"), "changed D2"}

	project.compileOrFail(t, recorder)

	if recorder.runs["D3"] != 2 {
		t.Fatalf("D3 compiled %d times, want 2", recorder.runs["D3"])
	}
	assertReferenceFlags(t, project.writtenResponseFile(t, "D3"+responseFileExtension),
		[]string{producedReferenceFlag(project.plan, "D2")}, []string{unityReferenceFlag("D2")})
}

// Verifies the errors of a compile that was thrown away block nothing: the dependent below it is
// compiled, and the unit is reported with the outcome of the compile that was kept.
func TestCompileUnitsIgnoresTheErrorsOfADiscardedCompile(t *testing.T) {
	run := newOptimisticPlanRun(t, map[string][]string{"D1": {"X", "K"}, "D2": {"X", "D1"}, "D3": {"X", "D2"}},
		[]string{"D1", "D2", "D3"}, "X", "K", "D1", "D2", "D3")
	run.recorder.holdByUnit["K"] = weightHold
	run.keep("K")
	run.recorder.outputByRun["D2"] = []fakeCompilerOutput{failingCompilerOutput("D2"), {}}
	run.recorder.surfacesByRun["D2"] = []string{"", keptSurfaceOf("D2")}

	outcome := run.compile(t)

	if len(outcome.Blocked) != 0 {
		t.Errorf("blocked = %v, want none", outcome.Blocked)
	}
	if !resultOf(t, outcome.Units, "D2").Succeeded {
		t.Errorf("D2 = %+v, want the successful second compile", resultOf(t, outcome.Units, "D2"))
	}
	if !containsLine(compiledNames(outcome.Units), "D3") || run.recorder.runs["D3"] != 1 {
		t.Errorf("D3 compiled %d times and results are %v, want one compile reported",
			run.recorder.runs["D3"], compiledNames(outcome.Units))
	}
}

// Verifies a dependent that started before a dependent it references failed is blocked, its result
// thrown away, and the block carried on to the units below it.
func TestCompileUnitsBlocksADependentThatStartedBeforeItsReferenceFailed(t *testing.T) {
	run := newOptimisticPlanRun(t, map[string][]string{"D1": {"X", "K"}, "D2": {"X", "D1"}, "D3": {"X", "D2"}},
		[]string{"D1", "D2", "D3"}, "X", "K", "D1", "D2", "D3")
	run.recorder.holdByUnit["K"] = weightHold
	run.keep("K")
	run.fail("D1")

	outcome := run.compile(t)

	assertStrings(t, "results", compiledNames(outcome.Units), []string{"X", "K", "D1"})
	if len(resultOf(t, outcome.Units, "D1").Diagnostics) == 0 {
		t.Error("expected D1's errors to be reported")
	}
	assertStrings(t, "blocked", outcome.Blocked, []string{"D2", "D3"})
	if outcome.Skipped != 0 {
		t.Errorf("skipped = %d, want 0", outcome.Skipped)
	}
	if run.recorder.runs["D2"] != 1 || run.recorder.runs["D3"] != 1 {
		t.Errorf("D2 and D3 compiled %d and %d times, want once each before the failure was known",
			run.recorder.runs["D2"], run.recorder.runs["D3"])
	}
}

// Verifies a dependent that assumed a reference later skipped - because everything that reference
// reads kept its surface - is settled with the compile it started early.
func TestCompileUnitsSettlesADependentWhoseAssumedReferenceWasSkipped(t *testing.T) {
	run := newOptimisticPlanRun(t, map[string][]string{"R": {"K"}, "U": {"X", "R"}},
		[]string{"R", "U"}, "X", "K", "R", "U")
	run.recorder.holdByUnit["K"] = weightHold
	run.keep("K")

	outcome := run.compile(t)

	assertStrings(t, "results", compiledNames(outcome.Units), []string{"X", "K", "U"})
	if outcome.Skipped != 1 || len(outcome.Blocked) != 0 {
		t.Errorf("skipped = %d and blocked = %v, want 1 and none", outcome.Skipped, outcome.Blocked)
	}
	if run.recorder.runs["U"] != 1 {
		t.Errorf("U compiled %d times, want 1", run.recorder.runs["U"])
	}
	assertStrings(t, "units finished when U started", sortedNames(run.recorder.finishedWhen["U"]), []string{"X"})
	assertStrings(t, "U assumed", resultOf(t, outcome.Units, "U").AssumedReferences, []string{"R"})
}

// Verifies a dependent that assumed a reference later blocked by an error further up is blocked
// too, with its result thrown away and the error that caused it still reported.
func TestCompileUnitsBlocksADependentWhoseAssumedReferenceWasBlocked(t *testing.T) {
	run := newOptimisticPlanRun(t, map[string][]string{"R": {"A"}, "U": {"X", "R"}},
		[]string{"R", "U"}, "X", "A", "R", "U")
	run.recorder.holdByUnit["A"] = weightHold
	run.fail("A")

	outcome := run.compile(t)

	assertStrings(t, "results", compiledNames(outcome.Units), []string{"X", "A"})
	if len(resultOf(t, outcome.Units, "A").Diagnostics) == 0 {
		t.Error("expected A's errors to be reported")
	}
	assertStrings(t, "blocked", outcome.Blocked, []string{"R", "U"})
	if run.recorder.runs["U"] != 1 {
		t.Errorf("U compiled %d times, want 1", run.recorder.runs["U"])
	}
}

// Verifies an assembly selected for its own sources that started before a dependent it references
// finished is compiled again, against this run's output, when that dependent changed its surface.
func TestCompileUnitsCompilesAnEditedAssemblyAgainWhenAnAssumedReferenceChangedItsSurface(t *testing.T) {
	project, recorder := newOptimisticCachedProject(t, map[string][]string{"D1": {"X", "K"}, "Y": {"X", "D1"}},
		[]string{"D1"}, "X", "K", "D1", "Y")
	recorder.holdByUnit["K"] = weightHold
	recorder.surfaces["K"] = keptSurfaceOf("K")

	results := project.compileOrFail(t, recorder)

	if recorder.runs["Y"] != 2 {
		t.Fatalf("Y compiled %d times, want 2", recorder.runs["Y"])
	}
	assertReferenceFlags(t, project.writtenResponseFile(t, "Y"+responseFileExtension),
		[]string{producedReferenceFlag(project.plan, "D1")}, []string{unityReferenceFlag("D1")})
	if again := resultOf(t, results, "Y").CompiledAgainAfter; again != "D1" {
		t.Errorf("Y compiled again after %q, want D1", again)
	}
}

// Verifies an assembly selected for its own sources that started before a dependent it references
// failed is blocked, and its result thrown away.
func TestCompileUnitsBlocksAnEditedAssemblyWhoseAssumedReferenceFailed(t *testing.T) {
	run := newOptimisticPlanRun(t, map[string][]string{"D1": {"X", "K"}, "Y": {"X", "D1"}},
		[]string{"D1"}, "X", "K", "D1", "Y")
	run.recorder.holdByUnit["K"] = weightHold
	run.keep("K")
	run.fail("D1")

	outcome := run.compile(t)

	assertStrings(t, "blocked", outcome.Blocked, []string{"Y"})
	assertStrings(t, "results", compiledNames(outcome.Units), []string{"X", "K", "D1"})
	if run.recorder.runs["Y"] != 1 {
		t.Errorf("Y compiled %d times, want 1", run.recorder.runs["Y"])
	}
}

// newChainRecorder prepares the fake compiler for the chain plan of the cached project, with K
// keeping its surface and slow enough that D1 has not started when D2 can.
func newChainRecorder() *schedulerRecorder {
	recorder := newCachedSchedulerRecorder("X", "K", "D1", "D2")
	recorder.holdByUnit["K"] = weightHold
	recorder.surfaces["K"] = keptSurfaceOf("K")

	return recorder
}

// Verifies a dependent whose record may still be replayed once the dependents it references finish
// waits for them instead of starting early, so a second run over the same inputs replays it rather
// than compiling it twice again.
func TestCompileUnitsWaitsForAnAssumedReferenceWhenItsRecordMayStillApply(t *testing.T) {
	project, _ := newOptimisticCachedProject(t, chainReferences, []string{"D1", "D2"}, "X", "K", "D1", "D2")
	first := newChainRecorder()
	project.compileOrFail(t, first)
	if first.runs["D2"] != 2 {
		t.Fatalf("the first run compiled D2 %d times, want 2", first.runs["D2"])
	}

	second := newChainRecorder()
	results := project.compileOrFail(t, second)

	d2 := resultOf(t, results, "D2")
	if !d2.Reused || second.runs["D2"] != 0 || !d2.WaitedForRecord {
		t.Errorf("D2 = %+v after %d compiles, want replayed after waiting for its record", d2, second.runs["D2"])
	}
	if _, started := second.finishedWhen["D2"]; started {
		t.Error("D2 should not have started a compile in the second run")
	}
}

// Verifies a dependent whose record can no longer be replayed - a reference that already finished
// came out different - starts early again rather than waiting.
func TestCompileUnitsStartsADependentWhenItsRecordCannotApply(t *testing.T) {
	project, _ := newOptimisticCachedProject(t, chainReferences, []string{"D1", "D2"}, "X", "K", "D1", "D2")
	project.compileOrFail(t, newChainRecorder())

	project.editSource(t, "X", "class X { int Added; }")
	second := newChainRecorder()
	second.surfaces["X"] = "surface X again"
	results := project.compileOrFail(t, second)

	d2 := resultOf(t, results, "D2")
	if second.runs["D2"] != 2 || d2.WaitedForRecord || d2.Reused {
		t.Errorf("D2 = %+v after %d compiles, want compiled twice without waiting", d2, second.runs["D2"])
	}
}

// Verifies the record of a compile that started early, against a reference that then kept its
// surface, is replayed by the next run, whose key reads that reference the same way.
func TestCompileUnitsReplaysADependentStartedBeforeItsReferencesFinished(t *testing.T) {
	project, _ := newOptimisticCachedProject(t, chainReferences, []string{"D1", "D2"}, "X", "K", "D1", "D2")
	first := newChainRecorder()
	first.surfaces["D1"] = keptSurfaceOf("D1")
	firstResults := project.compileOrFail(t, first)
	if first.runs["D2"] != 1 {
		t.Fatalf("the first run compiled D2 %d times, want 1", first.runs["D2"])
	}
	assertStrings(t, "D2 assumed", resultOf(t, firstResults, "D2").AssumedReferences, []string{"D1"})

	second := newChainRecorder()
	second.surfaces["D1"] = keptSurfaceOf("D1")
	results := project.compileOrFail(t, second)

	if d2 := resultOf(t, results, "D2"); !d2.Reused || second.runs["D2"] != 0 {
		t.Errorf("D2 = %+v after %d compiles, want replayed", d2, second.runs["D2"])
	}
}

// Verifies an edited assembly large enough for the changed-files check is checked on its changed
// files when it starts before a dependent it references finishes, assuming that dependent kept
// Unity's surface the way its baseline read it.
func TestCompileUnitsFastChecksAnEditedAssemblyStartedBeforeItsReferencesFinish(t *testing.T) {
	project := newCachedSchedulerProject(t, map[string][]string{
		"Gamma": {"Alpha", "K"}, "Beta": {"Alpha", "Gamma"},
	}, "Alpha", "K", "Gamma", "Beta")
	markSelectedAsDependent(t, project.plan, "Gamma")
	sources := make([]string, 0, fastPathMinimumSources)
	for index := 0; index < fastPathMinimumSources; index++ {
		source := fmt.Sprintf("Assets/Beta/File%03d.cs", index)
		project.writeFile(t, source, bodyContent(index, "body"))
		sources = append(sources, source)
	}
	project.plan.Units[indexOfUnit(t, project.plan, "Beta")].Sources = sources
	worker := newFakeFastCheckWorker()
	keptRecorder := func() *schedulerRecorder {
		recorder := newCachedSchedulerRecorder("Alpha", "K", "Gamma", "Beta")
		recorder.holdByUnit["K"] = weightHold
		recorder.surfaces["Alpha"] = keptSurfaceOf("Alpha")
		recorder.surfaces["K"] = keptSurfaceOf("K")
		recorder.surfaces["Gamma"] = keptSurfaceOf("Gamma")

		return recorder
	}
	compileWithFastPath(t, project, worker, keptRecorder(), "Alpha", "K", "Gamma", "Beta")
	if !project.hasBaselineOf("Beta") {
		t.Fatal("expected the full compile of Beta to record a baseline")
	}

	// Why K is edited as well: it then compiles again, slowly, so Gamma is still waiting on it when
	// Beta can start. A replayed K would let Gamma finish first.
	project.editSource(t, "K", "class K { void Body() { } }")
	project.writeFile(t, "Assets/Beta/File007.cs", bodyContent(7, "edited body"))
	checksBefore := worker.checks
	outcome := compileWithFastPath(t, project, worker, keptRecorder(), "Alpha", "K", "Gamma", "Beta")

	beta := resultOf(t, outcome.Units, "Beta")
	if !beta.FastChecked || beta.FastPathFallbackReason != "" {
		t.Fatalf("expected Beta to be checked on its changed files, got %+v", beta)
	}
	assertStrings(t, "Beta assumed", beta.AssumedReferences, []string{"Gamma"})
	if checks := worker.checks - checksBefore; checks != 1 {
		t.Errorf("the worker checked %d times, want 1", checks)
	}
}

// Verifies a plan whose references never let a unit become ready - a cycle - ends the run with an
// error naming the units left over, instead of an empty success.
func TestCompileUnitsReportsAPlanItCannotFinish(t *testing.T) {
	plan := newSchedulerPlan(map[string][]string{"A": {"B"}, "B": {"A"}}, "A", "B")
	compiler := newSchedulerCompiler(t, newSchedulerRecorder(0))

	_, err := compileUnits(context.Background(), compiler, plan, 2)

	if err == nil {
		t.Fatal("expected the run to fail")
	}
	if !strings.Contains(err.Error(), "A") || !strings.Contains(err.Error(), "B") {
		t.Errorf("error = %v, want it to name A and B", err)
	}
}
