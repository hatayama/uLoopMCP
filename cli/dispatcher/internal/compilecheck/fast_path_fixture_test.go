package compilecheck

import (
	"context"
	"crypto/sha256"
	"encoding/hex"
	"fmt"
	"io/fs"
	"os"
	"path/filepath"
	"strings"
	"sync"
	"testing"
	"time"

	"github.com/hatayama/unity-cli-loop/dispatcher/internal/compilecheck/fastcheck"
	"github.com/hatayama/unity-cli-loop/dispatcher/internal/compilecheck/resultstore"
)

// fakeDeclarationSeparator splits a fixture source into its declaration part and its body part, so
// the fake worker can tell a body edit from a declaration edit the way the real fingerprint does.
const fakeDeclarationSeparator = "|"

// fakeFastCheckWorker stands in for the changed-files check worker.
type fakeFastCheckWorker struct {
	mutex          sync.Mutex
	key            string
	ensureErr      error
	checkErr       error
	outcome        string
	reason         string
	diagnostics    []fastcheck.WorkerDiagnostic
	uncheckedCodes []string
	// generatorsNotRerun is what Check reports about the source generators.
	generatorsNotRerun bool
	onCheck            func()
	ensures            int
	fingerprints       int
	checks             int
	lastCheck          fastcheck.CheckRequest
	// surfaceOutcome is what CompareSurface answers; empty answers different, so a test that does
	// not care about the comparison sees every differing reference assembly as a changed surface.
	surfaceOutcome string
	surfaceErr     error
	// surfaceComparisons lists the produced reference assembly of every comparison, in call order.
	surfaceComparisons []string
}

func newFakeFastCheckWorker() *fakeFastCheckWorker {
	return &fakeFastCheckWorker{key: "worker-a", outcome: fastcheck.CheckedOutcome}
}

func (worker *fakeFastCheckWorker) Ensure(_ context.Context) (fastcheck.WorkerPaths, error) {
	worker.mutex.Lock()
	defer worker.mutex.Unlock()
	worker.ensures++

	return fastcheck.WorkerPaths{Key: worker.key}, worker.ensureErr
}

func (worker *fakeFastCheckWorker) Fingerprint(
	_ context.Context, _ fastcheck.WorkerPaths, request fastcheck.FingerprintRequest,
) (fastcheck.FingerprintResponse, error) {
	worker.mutex.Lock()
	worker.fingerprints++
	worker.mutex.Unlock()

	response := fastcheck.FingerprintResponse{Fingerprints: map[string]string{}, ContentHashes: map[string]string{}}
	for _, source := range request.Sources {
		content, err := os.ReadFile(source)
		if err != nil {
			return fastcheck.FingerprintResponse{}, err
		}
		sum := sha256.Sum256(content)
		response.Fingerprints[source] = fakeDeclarationOf(string(content))
		response.ContentHashes[source] = hex.EncodeToString(sum[:])
	}

	return response, nil
}

func (worker *fakeFastCheckWorker) Check(
	_ context.Context, _ fastcheck.WorkerPaths, request fastcheck.CheckRequest,
) (fastcheck.CheckResponse, error) {
	worker.mutex.Lock()
	worker.checks++
	worker.lastCheck = request
	onCheck := worker.onCheck
	worker.mutex.Unlock()
	if onCheck != nil {
		onCheck()
	}
	if worker.checkErr != nil {
		return fastcheck.CheckResponse{}, worker.checkErr
	}

	for _, changed := range request.ChangedFiles {
		content, err := os.ReadFile(changed)
		if err != nil {
			return fastcheck.CheckResponse{}, err
		}
		if fakeDeclarationOf(string(content)) != request.BaselineFingerprints[changed] {
			return fastcheck.CheckResponse{Outcome: fastcheck.DeclarationChangedOutcome, Reason: changed}, nil
		}
	}

	return fastcheck.CheckResponse{
		Outcome:            worker.outcome,
		Reason:             worker.reason,
		Diagnostics:        worker.diagnostics,
		UncheckedCodes:     worker.uncheckedCodes,
		GeneratorsNotRerun: worker.generatorsNotRerun,
	}, nil
}

func (worker *fakeFastCheckWorker) CompareSurface(
	_ context.Context, _ fastcheck.WorkerPaths, request fastcheck.CompareSurfaceRequest,
) (fastcheck.CompareSurfaceResponse, error) {
	worker.mutex.Lock()
	defer worker.mutex.Unlock()
	worker.surfaceComparisons = append(worker.surfaceComparisons, request.ProducedPath)
	if worker.surfaceErr != nil {
		return fastcheck.CompareSurfaceResponse{}, worker.surfaceErr
	}
	if worker.surfaceOutcome == "" {
		return fastcheck.CompareSurfaceResponse{Outcome: fastcheck.DifferentSurfaceOutcome, Detail: "fake difference"}, nil
	}

	return fastcheck.CompareSurfaceResponse{Outcome: worker.surfaceOutcome}, nil
}

// fakeDeclarationOf is the fake fingerprint: everything before the separator.
func fakeDeclarationOf(content string) string {
	declaration, _, _ := strings.Cut(content, fakeDeclarationSeparator)

	return declaration
}

// fastPathProject is a project whose Alpha assembly is large enough for the changed-files check,
// with Beta referencing it.
type fastPathProject struct {
	*cachedSchedulerProject
	worker *fakeFastCheckWorker
}

// newFastPathProject lays out Alpha with sourceCount sources and Beta referencing Alpha.
func newFastPathProject(t *testing.T, sourceCount int) *fastPathProject {
	t.Helper()
	project := &fastPathProject{
		cachedSchedulerProject: newCachedSchedulerProject(t, map[string][]string{"Beta": {"Alpha"}}, "Alpha", "Beta"),
		worker:                 newFakeFastCheckWorker(),
	}

	sources := make([]string, 0, sourceCount)
	for index := 0; index < sourceCount; index++ {
		source := project.sourceName(index)
		project.writeFile(t, source, fmt.Sprintf("class File%03d %s body", index, fakeDeclarationSeparator))
		sources = append(sources, source)
	}
	project.plan.Units[0].Sources = sources

	return project
}

// sourceName names Alpha's source at index, spelled as a response file spells it.
func (project *fastPathProject) sourceName(index int) string {
	return fmt.Sprintf("Assets/Alpha/File%03d.cs", index)
}

// editBody changes the body part of Alpha's source at index, keeping its declaration.
func (project *fastPathProject) editBody(t *testing.T, index int, body string) {
	t.Helper()
	project.writeFile(t, project.sourceName(index), bodyContent(index, body))
}

// bodyContent is what Alpha's source at index holds once its body is body.
func bodyContent(index int, body string) string {
	return fmt.Sprintf("class File%03d %s %s", index, fakeDeclarationSeparator, body)
}

// editDeclaration changes the declaration part of Alpha's source at index.
func (project *fastPathProject) editDeclaration(t *testing.T, index int) {
	t.Helper()
	project.writeFile(t, project.sourceName(index), fmt.Sprintf("class File%03d : Base %s body", index, fakeDeclarationSeparator))
}

// absoluteSource names Alpha's source at index as the worker sees it.
func (project *fastPathProject) absoluteSource(index int) string {
	return filepath.Join(project.root, project.sourceName(index))
}

// runner builds the fast path runner the compile uses.
func (project *fastPathProject) runner(tryDisabled bool) *fastPathRunner {
	return &fastPathRunner{TryDisabled: tryDisabled, Worker: project.worker}
}

// run compiles the plan with the fast path, failing the test when the run fails.
func (project *fastPathProject) run(t *testing.T, recorder *schedulerRecorder, tryDisabled bool) compileOutcome {
	t.Helper()
	outcome, err := project.runWithContext(context.Background(), recorder, tryDisabled)
	if err != nil {
		t.Fatalf("expected the run to succeed, got error: %v", err)
	}

	return outcome
}

// runWithContext compiles the plan with the fast path under ctx.
func (project *fastPathProject) runWithContext(
	ctx context.Context, recorder *schedulerRecorder, tryDisabled bool,
) (compileOutcome, error) {
	compiler := Compiler{
		Paths:       project.paths,
		ProjectRoot: project.root,
		Timeout:     time.Minute,
		Run:         recorder.runner(),
		FastPath:    project.runner(tryDisabled),
	}

	return compileUnits(ctx, compiler, project.plan, 4)
}

// compileAlphaAsDependent runs Alpha alone through reuseOrCompile, marked as picked only as a
// dependent. Why not through the scheduler: it skips a dependent whose references kept their surface,
// and Alpha references nothing.
func (project *fastPathProject) compileAlphaAsDependent(t *testing.T) UnitResult {
	t.Helper()
	makeOutputDirectory(t, project.root, project.plan)
	unit := project.plan.Units[0]
	unit.SelectedAsDependent = true
	compiler := Compiler{
		Paths:       project.paths,
		ProjectRoot: project.root,
		Timeout:     time.Minute,
		Run:         newCachedSchedulerRecorder("Alpha").runner(),
		FastPath:    project.runner(false),
	}
	result, err := compiler.reuseOrCompile(context.Background(), project.plan, unit)
	if err != nil {
		t.Fatalf("expected Alpha to compile, got error: %v", err)
	}

	return result
}

// recordBaseline runs the plan once so Alpha has a manifest and a baseline, and checks it does.
func (project *fastPathProject) recordBaseline(t *testing.T) {
	t.Helper()
	project.run(t, newCachedSchedulerRecorder("Alpha", "Beta"), false)
	if !project.hasBaseline() {
		t.Fatal("expected the first full compile to record a baseline")
	}
}

// hasBaseline reports whether Alpha has a baseline of the current format.
func (project *fastPathProject) hasBaseline() bool {
	return project.hasBaselineOf("Alpha")
}

// baseline reads Alpha's baseline through the store and fails the test when there is none to use.
func (project *fastPathProject) baseline(t *testing.T) resultstore.Baseline[Diagnostic] {
	t.Helper()
	baseline, reason := project.records().LoadBaseline("Alpha")
	if reason != "" {
		t.Fatalf("expected a usable baseline for Alpha, got %q", reason)
	}

	return baseline
}

// generatedFileCount counts the files under the generator output a baseline points at.
func (project *fastPathProject) generatedFileCount(t *testing.T, baseline resultstore.Baseline[Diagnostic]) int {
	t.Helper()
	count := 0
	err := filepath.WalkDir(projectPath(project.root, baseline.GeneratedDirectory),
		func(_ string, entry fs.DirEntry, err error) error {
			if err == nil && !entry.IsDir() {
				count++
			}

			return err
		})
	if err != nil {
		t.Fatalf("failed to read the generator output: %v", err)
	}

	return count
}

// outputSnapshot hashes every file the check keeps for Alpha, keyed by its path relative to the
// output directory, leaving out the response file the changed-files check writes for itself.
func (project *fastPathProject) outputSnapshot(t *testing.T) map[string]string {
	t.Helper()
	snapshot := map[string]string{}
	err := filepath.WalkDir(project.outputDirectory(), func(path string, entry fs.DirEntry, err error) error {
		if err != nil || entry.IsDir() {
			return err
		}
		relative, err := filepath.Rel(project.outputDirectory(), path)
		if err != nil {
			return err
		}
		first, _, _ := strings.Cut(filepath.ToSlash(relative), "/")
		if !strings.HasPrefix(first, "Alpha.") || first == "Alpha"+fastCheckResponseFileSuffix {
			return nil
		}
		hash, err := hashFile(path)
		if err != nil {
			return err
		}
		snapshot[filepath.ToSlash(relative)] = hash

		return nil
	})
	if err != nil {
		t.Fatalf("failed to read the output directory: %v", err)
	}

	return snapshot
}

// assertFallback checks that Alpha was compiled in full for a reason starting with prefix.
func assertFallback(t *testing.T, outcome compileOutcome, recorder *schedulerRecorder, prefix string) {
	t.Helper()
	result := resultOf(t, outcome.Units, "Alpha")
	if result.FastChecked || result.Reused {
		t.Fatalf("expected Alpha to be compiled in full, got %+v", result)
	}
	if recorder.runs["Alpha"] != 1 {
		t.Errorf("expected csc to compile Alpha once, got %d", recorder.runs["Alpha"])
	}
	if result.FastPathFallbackReason == "" || !strings.HasPrefix(result.FastPathFallbackReason, prefix) {
		t.Errorf("fallback reason = %q, want a reason starting with %q", result.FastPathFallbackReason, prefix)
	}
}

// assertNotTried checks that Alpha was compiled in full without trying the changed-files check.
func assertNotTried(t *testing.T, outcome compileOutcome, worker *fakeFastCheckWorker) {
	t.Helper()
	result := resultOf(t, outcome.Units, "Alpha")
	if result.FastChecked || result.FastPathFallbackReason != "" || worker.checks != 0 {
		t.Errorf("expected the changed-files check not to be tried, got %+v after %d checks", result, worker.checks)
	}
}
