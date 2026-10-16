package compilecheck

import (
	"context"
	"encoding/json"
	"errors"
	"os"
	"path/filepath"
	"reflect"
	"runtime"
	"strings"
	"testing"

	"github.com/hatayama/unity-cli-loop/dispatcher/internal/compilecheck/resultstore"
)

// surfaceVerdictProject is one assembly whose rebuilt reference assembly differs in bytes from the
// one the last Unity build left, with a fake worker to ask about the difference.
type surfaceVerdictProject struct {
	root   string
	plan   BuildPlan
	unit   CompileUnit
	worker *fakeFastCheckWorker
}

func newSurfaceVerdictProject(t *testing.T) *surfaceVerdictProject {
	t.Helper()
	unityPath := filepath.Join(planDagDirectory, "A.ref.dll")
	projectRoot, plan, unit := newReferenceSurfaceProject(t, unityPath)
	project := &surfaceVerdictProject{root: projectRoot, plan: plan, unit: unit, worker: newFakeFastCheckWorker()}
	project.writeUnityBuild(t, "unity surface")
	project.writeProduced(t, "rebuilt surface")

	return project
}

func (project *surfaceVerdictProject) writeUnityBuild(t *testing.T, content string) {
	t.Helper()
	writeReferenceAssembly(t, filepath.Join(project.root, project.unit.Assembly.RefOutputPath), content)
}

func (project *surfaceVerdictProject) writeProduced(t *testing.T, content string) {
	t.Helper()
	writeReferenceAssembly(t, filepath.Join(project.root, project.plan.OutputDir, "A.ref.dll"), content)
}

// verdict asks for the unit's verdict with the changed-files check worker in place.
func (project *surfaceVerdictProject) verdict() SurfaceVerdict {
	compiler := Compiler{ProjectRoot: project.root, FastPath: &fastPathRunner{Worker: project.worker}}

	return compiler.referenceSurfaceVerdict(context.Background(), project.plan, project.unit)
}

func (project *surfaceVerdictProject) recordPath() string {
	return filepath.Join(project.root, project.plan.OutputDir, "A.surface.json")
}

// rewriteRecord replaces one field of the recorded verdict, the way a record left by another build
// or for other assemblies would hold it.
func (project *surfaceVerdictProject) rewriteRecord(t *testing.T, field string, value any) {
	t.Helper()
	content, err := os.ReadFile(project.recordPath())
	if err != nil {
		t.Fatalf("expected a recorded verdict, got %v", err)
	}
	record := map[string]any{}
	if err := json.Unmarshal(content, &record); err != nil {
		t.Fatalf("failed to parse the recorded verdict: %v", err)
	}
	if _, found := record[field]; !found {
		t.Fatalf("expected the recorded verdict to hold %s, got %v", field, record)
	}
	record[field] = value
	rewritten, err := json.Marshal(record)
	if err != nil {
		t.Fatalf("failed to encode the recorded verdict: %v", err)
	}
	if err := os.WriteFile(project.recordPath(), rewritten, 0o600); err != nil {
		t.Fatalf("failed to rewrite the recorded verdict: %v", err)
	}
}

func assertVerdict(t *testing.T, got SurfaceVerdict, kept bool, reason string) {
	t.Helper()
	if got.Kept != kept || got.Reason != reason {
		t.Fatalf("expected kept=%v with reason %q, got %+v", kept, reason, got)
	}
}

func assertComparisons(t *testing.T, worker *fakeFastCheckWorker, want int) {
	t.Helper()
	if len(worker.surfaceComparisons) != want {
		t.Fatalf("expected %d comparisons by the worker, got %d", want, len(worker.surfaceComparisons))
	}
}

// Verifies identical bytes keep the surface without asking the worker.
func TestReferenceSurfaceVerdictKeepsIdenticalBytesWithoutTheWorker(t *testing.T) {
	project := newSurfaceVerdictProject(t)
	project.writeProduced(t, "unity surface")

	got := project.verdict()

	assertVerdict(t, got, true, SurfaceSameBytes)
	assertComparisons(t, project.worker, 0)
	if got.Asked {
		t.Errorf("expected a byte match not to count as asking the worker, got %+v", got)
	}
}

// Verifies the worker is asked about any two reference assemblies whose bytes differ, whatever they
// contain - a private member renumbering the compiler's private nested types changes the bytes of an
// assembly that embeds no attribute type at all - is handed the rebuilt one and Unity's in that
// order, and leaves no copy behind.
func TestReferenceSurfaceVerdictAsksTheWorkerWhateverTheAssembliesContain(t *testing.T) {
	project := newSurfaceVerdictProject(t)
	project.worker.surfaceOutcome = "same"

	got := project.verdict()

	assertVerdict(t, got, true, SurfaceSameAPI)
	assertComparisons(t, project.worker, 1)
	want := fakeSurfaceComparison{Produced: "rebuilt surface", UnityBuild: "unity surface"}
	if project.worker.surfaceComparisons[0] != want {
		t.Errorf("expected the worker to compare %+v, got %+v", want, project.worker.surfaceComparisons[0])
	}
	if !got.Asked || got.FromRecord {
		t.Errorf("expected a verdict the worker gave, got %+v", got)
	}
	leftovers, err := filepath.Glob(filepath.Join(project.root, project.plan.OutputDir, "*.surface-compare-*"))
	if err != nil || len(leftovers) != 0 {
		t.Errorf("expected the copies handed to the worker to be removed, got %v (%v)", leftovers, err)
	}
}

// Verifies the worker's "same" keeps the surface and is recorded for the next run.
func TestReferenceSurfaceVerdictKeepsTheSurfaceTheWorkerCallsTheSame(t *testing.T) {
	project := newSurfaceVerdictProject(t)
	project.worker.surfaceOutcome = "same"

	assertVerdict(t, project.verdict(), true, SurfaceSameAPI)

	if _, err := os.Stat(project.recordPath()); err != nil {
		t.Errorf("expected the verdict to be recorded, got %v", err)
	}
}

// Verifies the worker's "different" changes the surface, with its detail, and is recorded too.
func TestReferenceSurfaceVerdictRecordsADifferenceTheWorkerFound(t *testing.T) {
	project := newSurfaceVerdictProject(t)

	got := project.verdict()

	assertVerdict(t, got, false, SurfaceChanged)
	if got.Detail != "fake difference" {
		t.Errorf("expected the worker's detail, got %q", got.Detail)
	}
	if _, err := os.Stat(project.recordPath()); err != nil {
		t.Errorf("expected the verdict to be recorded, got %v", err)
	}
}

// Verifies the worker compares the very bytes the record is keyed by: when Unity rewrites its
// reference assembly while the comparison runs, the answer and its record still describe the pair
// read before it, so a later run meeting that pair again is not answered for another one.
func TestReferenceSurfaceVerdictComparesTheBytesItKeysTheRecordBy(t *testing.T) {
	project := newSurfaceVerdictProject(t)
	project.worker.surfaceByContent = true
	project.worker.onCompare = func() { project.writeUnityBuild(t, "rebuilt surface") }

	assertVerdict(t, project.verdict(), false, SurfaceChanged)

	project.worker.onCompare = nil
	project.writeUnityBuild(t, "unity surface")
	project.worker.surfaceByContent = false

	assertVerdict(t, project.verdict(), false, SurfaceChanged)
}

// Verifies a difference the worker tells by names carries them and no unnamed reason.
func TestReferenceSurfaceVerdictCarriesTheChangedNames(t *testing.T) {
	project := newSurfaceVerdictProject(t)
	project.worker.surfaceNames = []string{"C", "M"}

	got := project.verdict()

	assertVerdict(t, got, false, SurfaceChanged)
	if !reflect.DeepEqual(got.ChangedNames, []string{"C", "M"}) || got.Unnamed != "" || !got.namedChange() {
		t.Errorf("expected a named change of C and M, got %+v", got)
	}
}

// Verifies a difference the worker cannot tell by names is unnamed and carries no names.
func TestReferenceSurfaceVerdictTreatsAnUnnamedAnswerAsUnnamed(t *testing.T) {
	project := newSurfaceVerdictProject(t)
	project.worker.surfaceUnnamed = "operator op_Addition"

	got := project.verdict()

	if got.Unnamed != "operator op_Addition" || len(got.ChangedNames) != 0 || got.namedChange() {
		t.Errorf("expected an unnamed change, got %+v", got)
	}
}

// Verifies a verdict given without the worker is unnamed, with its reason as the reason.
func TestReferenceSurfaceVerdictWithoutAWorkerIsUnnamed(t *testing.T) {
	project := newSurfaceVerdictProject(t)
	compiler := Compiler{ProjectRoot: project.root}

	got := compiler.referenceSurfaceVerdict(context.Background(), project.plan, project.unit)

	assertVerdict(t, got, false, SurfaceBytesDiffer)
	if got.Unnamed != SurfaceBytesDiffer || got.namedChange() {
		t.Errorf("expected an unnamed verdict, got %+v", got)
	}
}

// Verifies the record of a named difference answers again with the same names.
func TestReferenceSurfaceVerdictFromRecordCarriesTheChangedNames(t *testing.T) {
	project := newSurfaceVerdictProject(t)
	project.worker.surfaceNames = []string{"C", "M"}
	project.verdict()
	project.worker.surfaceNames = nil
	project.worker.surfaceUnnamed = "asked again"

	got := project.verdict()

	assertComparisons(t, project.worker, 1)
	if !got.FromRecord || !reflect.DeepEqual(got.ChangedNames, []string{"C", "M"}) || got.Unnamed != "" {
		t.Errorf("expected the recorded names, got %+v", got)
	}
}

// Verifies a different answer carrying neither names nor a reason is unnamed: the fake worker in
// the scheduler tests hands its answer over without the client's own check, so this is the one
// place such an answer is kept from reading as a change no dependent writes.
func TestVerdictFromWorkerTreatsADifferenceWithoutNamesAsUnnamed(t *testing.T) {
	got := verdictFromWorker(resultstore.SurfaceVerdict{Same: false})

	if got.Unnamed == "" || got.namedChange() {
		t.Errorf("expected an unnamed verdict, got %+v", got)
	}
}

// Verifies the verdicts nobody named a change in - kept, the zero value of a unit that failed or
// has no dependents, and every reason the worker was not asked - never read as a named change.
func TestOnlyANamedVerdictIsANamedChange(t *testing.T) {
	verdicts := []SurfaceVerdict{
		{Kept: true, Reason: SurfaceSameAPI},
		{},
		{Reason: SurfaceBytesDiffer, Unnamed: SurfaceBytesDiffer},
		{Reason: SurfaceChanged, ChangedNames: []string{"C"}, Unnamed: "operator op_Addition"},
		{Reason: SurfaceChanged},
	}
	for _, verdict := range verdicts {
		if verdict.namedChange() {
			t.Errorf("expected %+v not to be a named change", verdict)
		}
	}
	if !(SurfaceVerdict{Reason: SurfaceChanged, ChangedNames: []string{"C"}}).namedChange() {
		t.Error("expected a verdict with names and no unnamed reason to be a named change")
	}
}

// Verifies a matching record answers "same" without asking the worker again.
func TestReferenceSurfaceVerdictUsesAMatchingRecordThatSaysSame(t *testing.T) {
	project := newSurfaceVerdictProject(t)
	project.worker.surfaceOutcome = "same"
	project.verdict()
	project.worker.surfaceOutcome = ""

	got := project.verdict()

	assertVerdict(t, got, true, SurfaceSameAPI)
	assertComparisons(t, project.worker, 1)
	if !got.Asked || !got.FromRecord {
		t.Errorf("expected a verdict taken from the record, got %+v", got)
	}
}

// Verifies a matching record answers "different" without asking the worker again.
func TestReferenceSurfaceVerdictUsesAMatchingRecordThatSaysDifferent(t *testing.T) {
	project := newSurfaceVerdictProject(t)
	project.verdict()
	project.worker.surfaceOutcome = "same"

	got := project.verdict()

	assertVerdict(t, got, false, SurfaceChanged)
	assertComparisons(t, project.worker, 1)
	if !got.FromRecord || got.Detail != "fake difference" {
		t.Errorf("expected the recorded verdict with its detail, got %+v", got)
	}
}

// recordSameThenAnswerDifferent records a "same" verdict and makes the worker answer "different"
// from then on, so a test can tell whether the next verdict came from the record or the worker.
func recordSameThenAnswerDifferent(t *testing.T) *surfaceVerdictProject {
	t.Helper()
	project := newSurfaceVerdictProject(t)
	project.worker.surfaceOutcome = "same"
	assertVerdict(t, project.verdict(), true, SurfaceSameAPI)
	project.worker.surfaceOutcome = ""

	return project
}

// Verifies a record another version of the record format wrote is not used.
func TestReferenceSurfaceVerdictIgnoresARecordOfAnotherVersion(t *testing.T) {
	project := recordSameThenAnswerDifferent(t)
	project.rewriteRecord(t, "FormatVersion", -1)

	assertVerdict(t, project.verdict(), false, SurfaceChanged)
	assertComparisons(t, project.worker, 2)
}

// Verifies a record made for another rebuilt reference assembly is not used.
func TestReferenceSurfaceVerdictIgnoresARecordOfAnotherProducedAssembly(t *testing.T) {
	project := recordSameThenAnswerDifferent(t)
	project.writeProduced(t, "rebuilt surface after another edit")

	assertVerdict(t, project.verdict(), false, SurfaceChanged)
	assertComparisons(t, project.worker, 2)
}

// Verifies a record made against another Unity build is not used.
func TestReferenceSurfaceVerdictIgnoresARecordOfAnotherUnityBuild(t *testing.T) {
	project := recordSameThenAnswerDifferent(t)
	project.writeUnityBuild(t, "unity surface after another build")

	assertVerdict(t, project.verdict(), false, SurfaceChanged)
	assertComparisons(t, project.worker, 2)
}

// Verifies a record another worker build gave is not used: what the worker writes out is part of
// its code.
func TestReferenceSurfaceVerdictIgnoresARecordOfAnotherWorker(t *testing.T) {
	project := recordSameThenAnswerDifferent(t)
	project.worker.key = "worker-b"

	assertVerdict(t, project.verdict(), false, SurfaceChanged)
	assertComparisons(t, project.worker, 2)
}

// Verifies a worker that cannot be built counts as a changed surface, without a comparison.
func TestReferenceSurfaceVerdictTreatsAnUnavailableWorkerAsChanged(t *testing.T) {
	project := newSurfaceVerdictProject(t)
	project.worker.surfaceOutcome = "same"
	project.worker.ensureErr = errors.New("worker build failed")

	got := project.verdict()

	if got.Kept || !strings.HasPrefix(got.Reason, SurfaceCouldNotComparePrefix) {
		t.Fatalf("expected a changed surface that could not be compared, got %+v", got)
	}
	assertComparisons(t, project.worker, 0)
}

// Verifies a comparison that failed counts as a changed surface and leaves no record behind, so the
// next run asks again.
func TestReferenceSurfaceVerdictTreatsAFailedComparisonAsChangedAndRecordsNothing(t *testing.T) {
	project := newSurfaceVerdictProject(t)
	project.worker.surfaceErr = context.DeadlineExceeded

	got := project.verdict()

	if got.Kept || !strings.HasPrefix(got.Reason, SurfaceCouldNotComparePrefix) {
		t.Fatalf("expected a changed surface that could not be compared, got %+v", got)
	}
	if _, err := os.Stat(project.recordPath()); !os.IsNotExist(err) {
		t.Errorf("expected no recorded verdict, got %v", err)
	}
}

// Verifies a verdict that cannot be recorded still keeps the surface.
func TestReferenceSurfaceVerdictKeepsTheSurfaceWhenTheRecordCannotBeWritten(t *testing.T) {
	project := newSurfaceVerdictProject(t)
	project.worker.surfaceOutcome = "same"
	if err := os.MkdirAll(filepath.Join(project.recordPath(), "blocked"), outputDirPermissions); err != nil {
		t.Fatalf("failed to block the record path: %v", err)
	}

	assertVerdict(t, project.verdict(), true, SurfaceSameAPI)
}

// Verifies a run without the worker compares bytes only, as a run always did before the worker
// could compare surfaces.
func TestReferenceSurfaceVerdictWithoutAWorkerComparesBytesOnly(t *testing.T) {
	project := newSurfaceVerdictProject(t)
	compiler := Compiler{ProjectRoot: project.root}

	assertVerdict(t, compiler.referenceSurfaceVerdict(context.Background(), project.plan, project.unit), false, SurfaceBytesDiffer)
}

// Verifies a dependent picked only for an assembly whose rebuilt reference assembly the worker calls
// the same surface is skipped and counted as skipped, and that the surface check is reported on the
// referenced unit's result.
func TestCompileUnitsSkipsDependentsWhenTheWorkerCallsTheSurfaceTheSame(t *testing.T) {
	project := newCachedSchedulerProject(t, map[string][]string{"Beta": {"Alpha"}}, "Alpha", "Beta")
	markSelectedAsDependent(t, project.plan, "Beta")
	worker := newFakeFastCheckWorker()
	worker.surfaceOutcome = "same"
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")
	recorder.surfaces["Alpha"] = "rebuilt Alpha"

	outcome := compileWithFastPath(t, project, worker, recorder, "Alpha", "Beta")

	if recorder.runs["Beta"] != 0 || outcome.Skipped != 1 {
		t.Errorf("expected Beta to be skipped, got %d runs and %d skipped", recorder.runs["Beta"], outcome.Skipped)
	}
	alpha := resultOf(t, outcome.Units, "Alpha")
	if !alpha.Surface.Asked || alpha.Surface.Reason != SurfaceSameAPI {
		t.Errorf("expected Alpha to report the worker's same-API verdict, got %+v", alpha.Surface)
	}
}

// Verifies a unit that failed is never compared, even when the previous run's outputs it leaves
// behind would compare.
func TestCompileUnitsDoesNotCompareTheSurfaceOfAFailedUnit(t *testing.T) {
	project := newCachedSchedulerProject(t, map[string][]string{"Beta": {"Alpha"}}, "Alpha", "Beta")
	worker := newFakeFastCheckWorker()
	worker.surfaceOutcome = "same"
	first := newCachedSchedulerRecorder("Alpha", "Beta")
	first.surfaces["Alpha"] = "rebuilt Alpha"
	compileWithFastPath(t, project, worker, first, "Alpha", "Beta")
	worker.surfaceComparisons = nil

	project.editSource(t, "Alpha", "class Alpha { broken }")
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")
	recorder.output["Alpha"] = fakeCompilerOutput{stdout: "Assets/Alpha/Alpha.cs(1,1): error CS1002: ; expected", exitCode: 1}
	compileWithFastPath(t, project, worker, recorder, "Alpha", "Beta")

	assertComparisons(t, worker, 0)
}

// Verifies a comparison whose copies cannot be written counts as changed, without asking the worker.
func TestReferenceSurfaceVerdictTreatsUnwritableCopiesAsChanged(t *testing.T) {
	if runtime.GOOS == "windows" {
		t.Skip("directory permissions do not make a directory read-only on Windows")
	}
	project := newSurfaceVerdictProject(t)
	project.worker.surfaceOutcome = "same"
	outputDirectory := filepath.Join(project.root, project.plan.OutputDir)
	if err := os.Chmod(outputDirectory, 0o500); err != nil {
		t.Fatalf("failed to make the output directory read-only: %v", err)
	}
	t.Cleanup(func() { _ = os.Chmod(outputDirectory, outputDirPermissions) })

	got := project.verdict()

	if got.Kept || !strings.HasPrefix(got.Reason, SurfaceCouldNotComparePrefix) {
		t.Fatalf("expected a changed surface that could not be compared, got %+v", got)
	}
	assertComparisons(t, project.worker, 0)
}
