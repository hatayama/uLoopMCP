package compilecheck

import (
	"context"
	"errors"
	"fmt"
	"path/filepath"
	"runtime"
	"strings"
	"time"

	"github.com/hatayama/unity-cli-loop/dispatcher/internal/compilecheck/fastcheck"
)

const (
	fastCheckResponseFileSuffix = ".fastcheck.rsp"
	generatedFilesOutFlagPrefix = "-generatedfilesout:"

	// fastPathMinimumSources keeps small assemblies on the full compile: csc compiles them in a few
	// seconds, so paying the worker's fixed start-up cost (about 1.5 seconds) buys nothing.
	fastPathMinimumSources = 300
	// fastPathMaximumChangedFiles keeps a wide edit on the full compile, which a branch switch or a
	// rename across many files would otherwise push through a per-file check slower than csc.
	fastPathMaximumChangedFiles = 30
)

// fastCheckWorker is what the changed-files check needs from its worker, so tests can stand in
// for the real build and process.
type fastCheckWorker interface {
	Ensure(ctx context.Context) (fastcheck.WorkerPaths, error)
	Fingerprint(ctx context.Context, worker fastcheck.WorkerPaths, request fastcheck.FingerprintRequest) (fastcheck.FingerprintResponse, error)
	Check(ctx context.Context, worker fastcheck.WorkerPaths, request fastcheck.CheckRequest) (fastcheck.CheckResponse, error)
}

// fastPathRunner checks only the changed files of a unit against the last successful full compile,
// and records the baseline that check needs after each successful full compile.
type fastPathRunner struct {
	// TryDisabled stops trying the changed-files check (--full) without stopping the recording, so
	// the run after a --full run can still use it.
	TryDisabled bool
	Worker      fastCheckWorker
}

// fastPathAttempt is the outcome of trying the changed-files check on one unit: a result to use,
// the reason it fell back to a full compile, or an error that stops the run.
type fastPathAttempt struct {
	Used   bool
	Result UnitResult
	Reason string
	Err    error
}

func fallBack(reason string) fastPathAttempt {
	return fastPathAttempt{Reason: reason}
}

// shouldTry reports whether the changed-files check is worth trying on a unit before compiling it.
// Why a unit picked only as a dependent is left out: nothing it compiles from changed, so it has no
// changed file to check, and what moved under it is a reference the check does not follow.
// Why a small assembly is left out without a reason: it never gets a baseline, so listing it as a
// fallback on every run would only bury the fallbacks that say something.
func (runner *fastPathRunner) shouldTry(plan BuildPlan, unit CompileUnit, key string) bool {
	return runner != nil && !runner.TryDisabled && key != "" && !plan.All && !unit.SelectedAsDependent &&
		len(unit.Sources) >= fastPathMinimumSources
}

// shouldRecord reports whether a full compile of a unit should leave a baseline.
func (runner *fastPathRunner) shouldRecord(unit CompileUnit, result UnitResult) bool {
	return runner != nil && result.Succeeded && !unit.SelectedAsDependent &&
		len(unit.Sources) >= fastPathMinimumSources
}

// Try checks only the changed files of a unit when the last full compile left a usable baseline,
// and otherwise says why the unit has to be compiled in full.
// nonSourceKey is the unit's non-source key computed before anything ran, empty when it could not be.
func (runner *fastPathRunner) Try(
	ctx context.Context, c Compiler, plan BuildPlan, unit CompileUnit, keyResult inputKeyResult,
	nonSourceKey nonSourceIdentity,
) fastPathAttempt {
	records := c.records(plan)
	baseline, reason := records.loadBaseline(unit.Assembly.AssemblyName)
	if reason != "" {
		return fallBack(reason)
	}

	if nonSourceKey.Key == "" {
		return fallBack("inputs other than source contents could not be read")
	}
	if nonSourceKey.Key != baseline.NonSourceKey.Key {
		return fallBack("inputs other than source contents changed: " +
			strings.Join(changedInputKinds(nonSourceKey, baseline.NonSourceKey), ", "))
	}

	if !records.generatorOutputUnchanged(baseline) {
		return fallBack("generator output of the baseline is gone")
	}

	changed := changedSources(unit, keyResult, baseline.Files)
	if len(changed) == 0 {
		// The source contents and every other input match the record, so the input key matches it
		// too, and reuse would have answered first. Getting here means the record and the manifest
		// disagree; the full compile that follows removes both before it records them again.
		return fallBack("baseline record does not match its compile result")
	}

	if len(changed) > fastPathMaximumChangedFiles {
		return fallBack(fmt.Sprintf("too many changed files (%d, limit %d)", len(changed), fastPathMaximumChangedFiles))
	}

	return runner.check(ctx, c, plan, unit, baseline, changed)
}

// check runs the worker over the changed files and merges what it found with the diagnostics the
// last full compile reported for the files that did not change.
func (runner *fastPathRunner) check(
	ctx context.Context, c Compiler, plan BuildPlan, unit CompileUnit,
	baseline storedBaseline, changed []string,
) fastPathAttempt {
	startedAt := time.Now()
	worker, err := runner.Worker.Ensure(ctx)
	if ctxErr := ctx.Err(); ctxErr != nil {
		return fastPathAttempt{Err: ctxErr}
	}
	if err != nil {
		return fallBack("worker unavailable: " + err.Error())
	}

	if worker.Key != baseline.WorkerKey {
		return fallBack("baseline was fingerprinted by another worker build")
	}

	responseFilePath, err := writeRewrittenResponseFile(
		c.ProjectRoot, plan, unit, unit.Assembly.AssemblyName+fastCheckResponseFileSuffix, nil)
	if err != nil {
		return fallBack("worker failed: " + err.Error())
	}

	response, err := runner.Worker.Check(ctx, worker, buildCheckRequest(c.ProjectRoot, baseline, responseFilePath, changed))
	if ctxErr := ctx.Err(); ctxErr != nil {
		return fastPathAttempt{Err: ctxErr}
	}
	if err != nil {
		return fallBack("worker failed: " + err.Error())
	}

	if response.Outcome != fastcheck.CheckedOutcome {
		return fallBack(response.Outcome + ": " + projectRelativeReason(c.ProjectRoot, response.Reason, changed))
	}

	diagnostics := mergeDiagnostics(c.ProjectRoot, unit, baseline.Diagnostics, response.Diagnostics, changed)

	return fastPathAttempt{Used: true, Result: UnitResult{
		Assembly:              unit.Assembly.AssemblyName,
		Diagnostics:           diagnostics,
		Succeeded:             !containsError(diagnostics),
		Duration:              time.Since(startedAt),
		FastChecked:           true,
		FastCheckChangedFiles: changed,
		UncheckedCodes:        response.UncheckedCodes,
		GeneratorsNotRerun:    response.GeneratorsNotRerun,
	}}
}

// projectRelativeReason rewrites the absolute paths the worker put in a reason the way the output
// spells files. Why: callers paste the output into pull requests and logs, and an absolute path
// would carry the local machine's layout with it.
func projectRelativeReason(projectRoot string, reason string, changed []string) string {
	for _, source := range changed {
		reason = strings.ReplaceAll(reason, projectPath(projectRoot, source), source)
	}

	return strings.ReplaceAll(reason, projectRoot+string(filepath.Separator), "")
}

// buildCheckRequest names, in absolute paths, what the worker checks and against which baseline.
func buildCheckRequest(
	projectRoot string, baseline storedBaseline, responseFilePath string, changed []string,
) fastcheck.CheckRequest {
	request := fastcheck.CheckRequest{
		ProjectRoot:          projectRoot,
		ResponseFilePath:     responseFilePath,
		ChangedFiles:         make([]string, 0, len(changed)),
		BaselineFingerprints: map[string]string{},
	}
	if baseline.GeneratedDirectory != "" {
		request.GeneratedDirectory = projectPath(projectRoot, baseline.GeneratedDirectory)
	}
	for _, source := range changed {
		absolute := projectPath(projectRoot, source)
		request.ChangedFiles = append(request.ChangedFiles, absolute)
		request.BaselineFingerprints[absolute] = baseline.Files[source].Fingerprint
	}

	return request
}

// changedSources lists the sources whose content differs from the baseline, using the hashes the
// input key read rather than reading the files again.
func changedSources(unit CompileUnit, keyResult inputKeyResult, files map[string]baselineFile) []string {
	changed := []string{}
	for _, source := range unit.Sources {
		if keyResult.SourceHashes[source] != files[source].ContentHash {
			changed = append(changed, source)
		}
	}

	return changed
}

// mergeDiagnostics combines the last full compile's diagnostics for the files that did not change
// with what the worker found in the changed ones.
// Why duplicates are dropped by position and code: the analysis of a changed file can report a
// diagnostic in another file, which the full compile reported as well.
func mergeDiagnostics(
	projectRoot string, unit CompileUnit, carried []Diagnostic, fresh []fastcheck.WorkerDiagnostic, changed []string,
) []Diagnostic {
	changedSet := map[string]bool{}
	for _, source := range changed {
		changedSet[normalizeDiagnosticPath(projectRoot, source)] = true
	}

	spellings := map[string]string{}
	for _, source := range unit.Sources {
		spellings[normalizeDiagnosticPath(projectRoot, source)] = source
	}

	merged := []Diagnostic{}
	seen := map[string]bool{}
	add := func(diagnostic Diagnostic) {
		key := fmt.Sprintf("%s:%d:%d:%s",
			normalizeDiagnosticPath(projectRoot, diagnostic.File), diagnostic.Line, diagnostic.Column, diagnostic.Code)
		if seen[key] {
			return
		}
		seen[key] = true
		merged = append(merged, diagnostic)
	}

	for _, diagnostic := range carried {
		if !changedSet[normalizeDiagnosticPath(projectRoot, diagnostic.File)] {
			add(diagnostic)
		}
	}
	for _, diagnostic := range fresh {
		add(Diagnostic{
			Severity: diagnostic.Severity,
			Code:     diagnostic.Code,
			Message:  diagnostic.Message,
			File:     diagnosticFileSpelling(projectRoot, diagnostic.File, spellings),
			Line:     diagnostic.Line,
			Column:   diagnostic.Column,
		})
	}

	return merged
}

// diagnosticFileSpelling writes a worker diagnostic's absolute path the way csc would print it: as
// the response file spells the source, or relative to the project root for anything else.
func diagnosticFileSpelling(projectRoot string, file string, spellings map[string]string) string {
	if spelling, found := spellings[normalizeDiagnosticPath(projectRoot, file)]; found {
		return spelling
	}

	relative, err := filepath.Rel(projectRoot, file)
	if err != nil {
		return filepath.ToSlash(file)
	}

	return filepath.ToSlash(relative)
}

// normalizeDiagnosticPath turns a path csc printed, a response file spelled, or the worker reported
// into one comparable form.
// Why lowercased on Windows only: its file system ignores case, so two spellings name one file.
func normalizeDiagnosticPath(projectRoot string, path string) string {
	normalized := filepath.ToSlash(filepath.Clean(projectPath(projectRoot, path)))
	if runtime.GOOS == "windows" {
		return strings.ToLower(normalized)
	}

	return normalized
}

// bootstrappedWorker builds the worker on first use and calls it through fastcheck.Client.
// Why the build is shared: units compile in parallel, and each building its own copy of the worker
// on the first run would start one csc per unit for the same output.
type bootstrappedWorker struct {
	bootstrap     *fastcheck.Bootstrap
	tempDirectory string
	run           fastcheck.CommandRunner
	ensure        func(ctx context.Context) (fastcheck.WorkerPaths, error)
}

// newBootstrappedWorker prepares a worker that the Editor's toolchain builds into cacheRoot.
func newBootstrappedWorker(paths EditorCompilerPaths, cacheRoot string, tempDirectory string, run fastcheck.CommandRunner) *bootstrappedWorker {
	bootstrap := &fastcheck.Bootstrap{
		CompilerPaths: fastcheck.ToolchainPaths{
			DotnetHostPath:      paths.DotnetHostPath,
			CompilerDllPath:     paths.CompilerDllPath,
			CompilerDirectory:   paths.CompilerDirectory,
			SharedFrameworkPath: paths.SharedFrameworkPath,
		},
		CacheRoot: cacheRoot,
		Run:       run,
	}

	return &bootstrappedWorker{
		bootstrap:     bootstrap,
		tempDirectory: tempDirectory,
		run:           run,
		ensure:        onceWithContext(bootstrap.Ensure),
	}
}

func (w *bootstrappedWorker) Ensure(ctx context.Context) (fastcheck.WorkerPaths, error) {
	return w.ensure(ctx)
}

func (w *bootstrappedWorker) Fingerprint(
	ctx context.Context, worker fastcheck.WorkerPaths, request fastcheck.FingerprintRequest,
) (fastcheck.FingerprintResponse, error) {
	return w.client(worker).Fingerprint(ctx, request)
}

func (w *bootstrappedWorker) Check(
	ctx context.Context, worker fastcheck.WorkerPaths, request fastcheck.CheckRequest,
) (fastcheck.CheckResponse, error) {
	return w.client(worker).Check(ctx, request)
}

func (w *bootstrappedWorker) client(worker fastcheck.WorkerPaths) fastcheck.Client {
	return fastcheck.Client{Worker: worker, TempDirectory: w.tempDirectory, Run: w.run}
}

// onceWithContext runs ensure once and hands its answer to every later caller.
// Why a cancelled call is not remembered: it says nothing about whether the worker builds, and the
// run is ending anyway.
func onceWithContext(
	ensure func(ctx context.Context) (fastcheck.WorkerPaths, error),
) func(ctx context.Context) (fastcheck.WorkerPaths, error) {
	lock := make(chan struct{}, 1)
	done := false
	var paths fastcheck.WorkerPaths
	var result error

	return func(ctx context.Context) (fastcheck.WorkerPaths, error) {
		select {
		case lock <- struct{}{}:
		case <-ctx.Done():
			return fastcheck.WorkerPaths{}, ctx.Err()
		}
		defer func() { <-lock }()

		if done {
			return paths, result
		}

		paths, result = ensure(ctx)
		if result == nil || !errors.Is(result, context.Canceled) && !errors.Is(result, context.DeadlineExceeded) {
			done = true
		}

		return paths, result
	}
}
