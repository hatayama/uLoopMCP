package compilecheck

import (
	"bytes"
	"context"
	"crypto/sha256"
	"encoding/hex"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"time"

	"github.com/hatayama/unity-cli-loop/dispatcher/internal/compilecheck/fastcheck"
	"github.com/hatayama/unity-cli-loop/dispatcher/internal/compilecheck/resultstore"
)

const (
	// SurfaceSameBytes means the rebuilt reference assembly is byte-identical to Unity's.
	SurfaceSameBytes = "same bytes"
	// SurfaceSameAPI means the bytes differ but the worker found the same API for every dependent.
	SurfaceSameAPI = "same API"
	// SurfaceChanged means the worker found an API a dependent may read differently.
	SurfaceChanged = "changed"
	// SurfaceBytesDiffer means the bytes differ and the run had no worker to compare the API with.
	SurfaceBytesDiffer = "bytes differ"
	// SurfaceCouldNotComparePrefix starts the reason of a verdict the worker could not give.
	SurfaceCouldNotComparePrefix = "could not compare: "
	// SurfaceCouldNotReadPrefix starts the reason of a verdict whose reference assemblies could not
	// both be read.
	SurfaceCouldNotReadPrefix = "could not read: "
)

// SurfaceVerdict says whether a unit's rebuilt reference assembly shows its dependents the API of
// the one the last Unity build produced, and why.
type SurfaceVerdict struct {
	// Kept means every dependent compiles against the same API either way.
	Kept bool
	// Reason is one of the Surface* values, or starts with one of the prefixes.
	Reason string
	// Detail is what the worker said differs, when it answered.
	Detail string
	// Asked marks a verdict the worker gave, now or in a record of the same comparison.
	Asked bool
	// FromRecord marks a verdict taken from the record of the same comparison.
	FromRecord bool
	// Duration is how long asking took, record lookup included.
	Duration time.Duration
	// ChangedNames are the identifiers a dependent has to write to be affected by the change, sorted;
	// empty when kept and when unnamed.
	ChangedNames []string
	// Unnamed says why the change cannot be told by names; empty when kept and when named.
	Unnamed string
}

// namedChange reports whether a dependent that writes none of the verdict's names can treat the
// reference as kept. Every other verdict that is not kept - unnamed, not compared, the zero value
// of a unit that failed or has no dependents - affects every dependent.
func (v SurfaceVerdict) namedChange() bool {
	return !v.Kept && v.Unnamed == "" && len(v.ChangedNames) > 0
}

// surfaceComparer is what the surface comparison needs from the worker.
type surfaceComparer interface {
	Ensure(ctx context.Context) (fastcheck.WorkerPaths, error)
	CompareSurface(
		ctx context.Context, worker fastcheck.WorkerPaths, request fastcheck.CompareSurfaceRequest,
	) (fastcheck.CompareSurfaceResponse, error)
}

// referenceSurfaceVerdict says whether compiling this unit left its reference assembly showing every
// dependent the API the last Unity build showed it, so the dependents need not be compiled again.
// Why the comparison is against Unity's own artifact rather than this check's previous output: the
// dependents were last compiled against Unity's, so only that one answers whether they would see
// anything new. Comparing against the previous run would only say the surface has not moved since
// that run, and would hide an error a dependent has been carrying all along.
// Why differing bytes go to the worker: the compiler writes private bookkeeping into a reference
// assembly - nullable context attributes that follow private members, attribute types it embeds,
// renumbered closure and state machine types - so an edit no dependent can see still moves bytes.
// Why anything unreadable or unanswered counts as changed: compiling a dependent that did not need
// it costs time, while skipping one that did costs a missed error.
func (c Compiler) referenceSurfaceVerdict(ctx context.Context, plan BuildPlan, unit CompileUnit) SurfaceVerdict {
	if unit.Assembly.RefOutputPath == "" {
		return unnamedVerdict(SurfaceCouldNotReadPrefix + "the assembly has no reference assembly")
	}

	producedPath := filepath.Join(c.ProjectRoot, plan.OutputDir, filepath.Base(unit.Assembly.RefOutputPath))
	produced, err := os.ReadFile(producedPath)
	if err != nil {
		return unnamedVerdict(SurfaceCouldNotReadPrefix + "this run produced no reference assembly")
	}
	unityBuildPath := filepath.Join(c.ProjectRoot, unit.Assembly.RefOutputPath)
	unityBuild, err := os.ReadFile(unityBuildPath)
	if err != nil {
		return unnamedVerdict(SurfaceCouldNotReadPrefix + "the last Unity build left no reference assembly")
	}
	if bytes.Equal(produced, unityBuild) {
		return SurfaceVerdict{Kept: true, Reason: SurfaceSameBytes}
	}

	comparer := c.surfaceComparer()
	if comparer == nil {
		return unnamedVerdict(SurfaceBytesDiffer)
	}

	startedAt := time.Now()
	verdict := c.askSurface(ctx, plan, unit, comparer, comparedPair{produced: produced, unityBuild: unityBuild})
	verdict.Asked = true
	verdict.Duration = time.Since(startedAt)

	return verdict
}

// surfaceComparer is the worker the run shares with the changed-files check, or nil without one.
// Why it is used under --full too: --full stops trying the changed-files check, while whether a
// dependent needs compiling is a separate question.
func (c Compiler) surfaceComparer() surfaceComparer {
	if c.FastPath == nil || c.FastPath.Worker == nil {
		return nil
	}

	return c.FastPath.Worker
}

// comparedPair is the two reference assemblies of one comparison, as read once.
type comparedPair struct {
	produced   []byte
	unityBuild []byte
}

// askSurface answers from the record of the same comparison when there is one, and otherwise asks
// the worker and records what it said.
// Why the worker reads copies rather than the two paths: Unity can rewrite its reference assembly
// while the worker is built or runs, and an answer about bytes other than the ones the record is
// keyed by would later answer for a pair nobody compared.
// Why only an answer is recorded: a failure says nothing about the two assemblies, and the next
// run, which meets the same pair until Unity builds again, should ask again rather than repeat it.
// Why a record that cannot be written leaves the verdict standing: it only saves the next run a
// comparison.
func (c Compiler) askSurface(
	ctx context.Context, plan BuildPlan, unit CompileUnit, comparer surfaceComparer, pair comparedPair,
) SurfaceVerdict {
	worker, err := comparer.Ensure(ctx)
	if err != nil {
		return unnamedVerdict(SurfaceCouldNotComparePrefix + err.Error())
	}

	inputs := resultstore.SurfaceInputs{
		ProducedSha256: sha256Hex(pair.produced), UnityBuildSha256: sha256Hex(pair.unityBuild), WorkerKey: worker.Key,
	}
	records := c.records(plan)
	assembly := unit.Assembly.AssemblyName
	if recorded, found := records.ReusableSurfaceVerdict(assembly, inputs); found {
		verdict := verdictFromWorker(recorded)
		verdict.FromRecord = true

		return verdict
	}

	request, cleanup, err := writeComparedPair(filepath.Join(c.ProjectRoot, plan.OutputDir), assembly, pair)
	if err != nil {
		return unnamedVerdict(SurfaceCouldNotComparePrefix + err.Error())
	}
	defer cleanup()

	response, err := comparer.CompareSurface(ctx, worker, request)
	if err != nil {
		return unnamedVerdict(SurfaceCouldNotComparePrefix + err.Error())
	}

	answer := resultstore.SurfaceVerdict{
		Same: response.Outcome == fastcheck.SameSurfaceOutcome, Detail: response.Detail,
		ChangedNames: response.ChangedNames, UnnamedReason: response.UnnamedReason,
	}
	_ = records.RecordSurfaceVerdict(assembly, inputs, answer)

	return verdictFromWorker(answer)
}

// writeComparedPair writes the two reference assemblies of a comparison to files of their own in
// directory, and returns the request naming them with what removes them.
func writeComparedPair(
	directory string, assembly string, pair comparedPair,
) (fastcheck.CompareSurfaceRequest, func(), error) {
	paths := []string{}
	cleanup := func() {
		for _, path := range paths {
			_ = os.Remove(path)
		}
	}
	for _, content := range [][]byte{pair.produced, pair.unityBuild} {
		path, err := writeComparedCopy(directory, assembly, content)
		if err != nil {
			cleanup()

			return fastcheck.CompareSurfaceRequest{}, nil, err
		}
		paths = append(paths, path)
	}

	return fastcheck.CompareSurfaceRequest{ProducedPath: paths[0], UnityBuildPath: paths[1]}, cleanup, nil
}

func writeComparedCopy(directory string, assembly string, content []byte) (string, error) {
	file, err := os.CreateTemp(directory, assembly+".surface-compare-*.tmp")
	if err != nil {
		return "", fmt.Errorf("failed to write a reference assembly to compare: %w", err)
	}
	_, writeErr := file.Write(content)
	closeErr := file.Close()
	if err := errors.Join(writeErr, closeErr); err != nil {
		_ = os.Remove(file.Name())

		return "", fmt.Errorf("failed to write a reference assembly to compare: %w", err)
	}

	return file.Name(), nil
}

// unnamedVerdict is a verdict reached without the worker's answer: it affects every dependent.
func unnamedVerdict(reason string) SurfaceVerdict {
	return SurfaceVerdict{Reason: reason, Unnamed: reason}
}

// verdictFromWorker turns an answer into a verdict. Why a difference naming nothing is unnamed here
// and not only in the client: an answer reaches this point from a record or from any comparer, and
// a change that names nothing must never read as one no dependent writes.
func verdictFromWorker(answer resultstore.SurfaceVerdict) SurfaceVerdict {
	if answer.Same {
		return SurfaceVerdict{Kept: true, Reason: SurfaceSameAPI, Detail: answer.Detail}
	}

	verdict := SurfaceVerdict{Reason: SurfaceChanged, Detail: answer.Detail}
	switch {
	case answer.UnnamedReason != "":
		verdict.Unnamed = answer.UnnamedReason
	case len(answer.ChangedNames) == 0:
		verdict.Unnamed = fastcheck.WorkerNamedNoChange
	default:
		verdict.ChangedNames = answer.ChangedNames
	}

	return verdict
}

func sha256Hex(content []byte) string {
	sum := sha256.Sum256(content)

	return hex.EncodeToString(sum[:])
}
