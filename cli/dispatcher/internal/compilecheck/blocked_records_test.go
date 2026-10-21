package compilecheck

import (
	"fmt"
	"testing"

	"github.com/hatayama/unity-cli-loop/dispatcher/internal/compilecheck/fastcheck"
)

// blockAlphaOnce makes Alpha fail to compile, which blocks Beta, and checks that it did.
// Why Alpha's source is edited first: an untouched Alpha would be replayed from the previous run's
// record, and the failure set up here would never be read.
func blockAlphaOnce(t *testing.T, project *cachedSchedulerProject) {
	t.Helper()
	project.editSource(t, "Alpha", "class Alpha { broken }")
	failing := newCachedSchedulerRecorder("Alpha", "Beta")
	failing.output["Alpha"] = failingCompilerOutput("Alpha")
	// Why the failing compile writes no reference assembly: csc writes none when it reports errors.
	delete(failing.surfaces, "Alpha")
	outcome, err := project.compile(t.Context(), failing)
	if err != nil {
		t.Fatalf("expected the failing run to finish, got error: %v", err)
	}
	assertStrings(t, "blocked", outcome.Blocked, []string{"Beta"})
}

// newBlockableProject lays out Alpha and Beta, Beta referencing Alpha and picked only as its
// dependent, and compiles both once. Why Alpha's surface differs from Unity's: when it matches,
// Beta is skipped for that reason and the run never asks whether Beta's records can be reused.
func newBlockableProject(t *testing.T) *cachedSchedulerProject {
	t.Helper()
	project := newCachedSchedulerProject(t, map[string][]string{"Beta": {"Alpha"}}, "Alpha", "Beta")
	markSelectedAsDependent(t, project.plan, "Beta")
	project.compileOrFail(t, newCachedSchedulerRecorder("Alpha", "Beta"))

	return project
}

// Verifies a dependent blocked by one failed run is reused, without a compile, once the assembly it
// references comes back with the same reference assembly it was compiled against.
func TestCompileUnitsReusesABlockedDependentWhenItsReferenceComesBackTheSame(t *testing.T) {
	project := newBlockableProject(t)
	blockAlphaOnce(t, project)

	project.editSource(t, "Alpha", "class Alpha { }")
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")
	results := project.compileOrFail(t, recorder)

	if recorder.runs["Alpha"] != 1 {
		t.Errorf("Alpha should have compiled once, got %d", recorder.runs["Alpha"])
	}
	if recorder.runs["Beta"] != 0 {
		t.Errorf("Beta should not have compiled, got %d runs", recorder.runs["Beta"])
	}
	if !resultOf(t, results, "Beta").Reused {
		t.Errorf("Beta should have been reused, got %+v", resultOf(t, results, "Beta"))
	}
}

// Verifies a dependent blocked by one failed run is compiled again when the assembly it references
// comes back with a different reference assembly, since its record was made against the old one.
func TestCompileUnitsRecompilesABlockedDependentWhenItsReferenceComesBackChanged(t *testing.T) {
	project := newBlockableProject(t)
	blockAlphaOnce(t, project)

	project.editSource(t, "Alpha", "class Alpha { public void Added() { } }")
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")
	recorder.surfaces["Alpha"] = "changed surface Alpha"
	results := project.compileOrFail(t, recorder)

	if recorder.runs["Beta"] != 1 {
		t.Errorf("Beta should have compiled once, got %d runs", recorder.runs["Beta"])
	}
	if resultOf(t, results, "Beta").Reused {
		t.Error("Beta should not have been reused against a changed reference assembly")
	}
}

// failChangedFilesCheckOfAlpha edits one of Alpha's bodies into an error the changed-files check
// reports, runs the plan, and checks that the error blocked Beta.
func failChangedFilesCheckOfAlpha(t *testing.T, project *fastPathProject) {
	t.Helper()
	project.editBody(t, 0, "int x = \"s\";")
	project.worker.diagnostics = []fastcheck.WorkerDiagnostic{{
		File: project.absoluteSource(0), Line: 1, Column: 20, Severity: severityError, Code: "CS0029", Message: "cannot convert",
	}}
	outcome := project.run(t, newCachedSchedulerRecorder("Alpha", "Beta"), false)
	if result := resultOf(t, outcome.Units, "Alpha"); !result.FastChecked || result.Succeeded {
		t.Fatalf("expected a failed changed-files check, got %+v", result)
	}
	assertStrings(t, "blocked", outcome.Blocked, []string{"Beta"})
	project.worker.diagnostics = nil
}

// newBlockableFastPathProject lays out a fast path project whose Beta is picked only as Alpha's
// dependent, and records Alpha's baseline. Alpha's surface differs from Unity's for the reason
// newBlockableProject gives.
func newBlockableFastPathProject(t *testing.T) *fastPathProject {
	t.Helper()
	project := newFastPathProject(t, fastPathMinimumSources)
	markSelectedAsDependent(t, project.plan, "Beta")
	project.recordBaseline(t)

	return project
}

// Verifies fixing a typo the changed-files check reported leaves the dependent it blocked reused:
// the check writes no output, so Beta reads the reference assembly it was compiled against.
func TestFastPathFixAfterAnErrorReusesTheBlockedDependents(t *testing.T) {
	project := newBlockableFastPathProject(t)
	failChangedFilesCheckOfAlpha(t, project)

	project.editBody(t, 0, "fixed")
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")
	outcome := project.run(t, recorder, false)

	if result := resultOf(t, outcome.Units, "Alpha"); !result.FastChecked || !result.Succeeded {
		t.Fatalf("expected a clean changed-files check of Alpha, got %+v", result)
	}
	if recorder.runs["Beta"] != 0 || !resultOf(t, outcome.Units, "Beta").Reused {
		t.Errorf("expected Beta to be reused, got %d runs and %+v", recorder.runs["Beta"], resultOf(t, outcome.Units, "Beta"))
	}
}

// Verifies undoing a typo the changed-files check reported, so the body is back to the baseline,
// leaves both Alpha and the dependent it blocked reused.
func TestFastPathRevertAfterAnErrorReusesTheBlockedDependents(t *testing.T) {
	project := newBlockableFastPathProject(t)
	failChangedFilesCheckOfAlpha(t, project)

	project.editBody(t, 0, "body")
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")
	outcome := project.run(t, recorder, false)

	if result := resultOf(t, outcome.Units, "Alpha"); !result.Reused {
		t.Fatalf("expected Alpha to be reused, got %+v", result)
	}
	if recorder.runs["Beta"] != 0 || !resultOf(t, outcome.Units, "Beta").Reused {
		t.Errorf("expected Beta to be reused, got %d runs and %+v", recorder.runs["Beta"], resultOf(t, outcome.Units, "Beta"))
	}
}

// Verifies a run that blocks an assembly keeps its baseline, so once the failed reference comes
// back with the same reference assembly, a body edit in the blocked assembly is answered by the
// changed-files check instead of a full compile.
func TestBlockedRunKeepsTheBaselineOfTheBlockedAssembly(t *testing.T) {
	project := &fastPathProject{
		cachedSchedulerProject: newCachedSchedulerProject(t, map[string][]string{"Beta": {"Alpha"}}, "Alpha", "Beta"),
		worker:                 newFakeFastCheckWorker(),
	}
	sources := make([]string, 0, fastPathMinimumSources)
	for index := 0; index < fastPathMinimumSources; index++ {
		source := fmt.Sprintf("Assets/Beta/File%03d.cs", index)
		project.writeFile(t, source, fmt.Sprintf("class File%03d %s body", index, fakeDeclarationSeparator))
		sources = append(sources, source)
	}
	project.plan.Units[1].Sources = sources
	project.run(t, newCachedSchedulerRecorder("Alpha", "Beta"), false)
	if !project.hasBaselineOf("Beta") {
		t.Fatal("expected the first run to record Beta's baseline")
	}
	blockAlphaOnce(t, project.cachedSchedulerProject)

	project.editSource(t, "Alpha", "class Alpha { }")
	project.writeFile(t, sources[0], fmt.Sprintf("class File000 %s edited", fakeDeclarationSeparator))
	recorder := newCachedSchedulerRecorder("Alpha", "Beta")
	outcome := project.run(t, recorder, false)

	if result := resultOf(t, outcome.Units, "Beta"); !result.FastChecked || recorder.runs["Beta"] != 0 {
		t.Errorf("expected Beta to be answered by the changed-files check, got %d runs and %+v", recorder.runs["Beta"], result)
	}
}
