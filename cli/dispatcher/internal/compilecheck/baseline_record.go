package compilecheck

import (
	"context"
	"errors"
	"fmt"
	"path/filepath"
	"strings"

	"github.com/hatayama/unity-cli-loop/dispatcher/internal/compilecheck/fastcheck"
	"github.com/hatayama/unity-cli-loop/dispatcher/internal/compilecheck/resultstore"
)

// RecordBaseline records what the next changed-files check of a unit compares against, after a
// successful full compile. nonSourceKey is the unit's non-source key computed before the compile.
// Why every content hash must match the one the input key read, and the non-source key the one
// computed before the compile: the manifest describes the inputs csc compiled, and an input changed
// during the compile would otherwise enter the baseline as if that compile had seen it, and its
// effect would never be checked by the next run.
func (runner *fastPathRunner) RecordBaseline(
	ctx context.Context, c Compiler, plan BuildPlan, unit CompileUnit, keyResult inputKeyResult,
	nonSourceKey nonSourceIdentity,
) error {
	if nonSourceKey.Key == "" {
		return errors.New("inputs other than source contents could not be read before the compile")
	}
	currentNonSourceKey, err := computeNonSourceKey(c.ProjectRoot, c.Paths, plan, unit)
	if err != nil {
		return err
	}
	if currentNonSourceKey.Key != nonSourceKey.Key {
		return errors.New("inputs other than source contents changed during the compile: " +
			strings.Join(changedInputKinds(currentNonSourceKey, nonSourceKey), ", "))
	}

	worker, err := runner.Worker.Ensure(ctx)
	if err != nil {
		return err
	}

	request := fastcheck.FingerprintRequest{
		ProjectRoot:      c.ProjectRoot,
		ResponseFilePath: filepath.Join(c.ProjectRoot, plan.OutputDir, unit.Assembly.AssemblyName+responseFileExtension),
		Sources:          make([]string, 0, len(unit.Sources)),
	}
	for _, source := range unit.Sources {
		request.Sources = append(request.Sources, projectPath(c.ProjectRoot, source))
	}

	fingerprints, err := runner.Worker.Fingerprint(ctx, worker, request)
	if err != nil {
		return err
	}

	files, err := baselineFiles(c.ProjectRoot, unit, keyResult, fingerprints)
	if err != nil {
		return err
	}

	return c.records(plan).RecordBaseline(unit.Assembly.AssemblyName, resultstore.BaselineInputs{
		InputKey:     keyResult.Key,
		NonSourceKey: resultstore.KeyDigest(nonSourceKey),
		WorkerKey:    worker.Key,
		Files:        files,
	})
}

// baselineFiles pairs each source's content hash, as the input key read it, with its fingerprint.
func baselineFiles(
	projectRoot string, unit CompileUnit, keyResult inputKeyResult, fingerprints fastcheck.FingerprintResponse,
) (map[string]resultstore.BaselineFile, error) {
	files := make(map[string]resultstore.BaselineFile, len(unit.Sources))
	for _, source := range unit.Sources {
		absolute := projectPath(projectRoot, source)
		fingerprint, hasFingerprint := fingerprints.Fingerprints[absolute]
		contentHash, hasHash := fingerprints.ContentHashes[absolute]
		if !hasFingerprint || !hasHash {
			return nil, fmt.Errorf("worker returned no fingerprint for %s", source)
		}
		if contentHash != keyResult.SourceHashes[source] {
			return nil, fmt.Errorf("source changed during the compile: %s", source)
		}
		files[source] = resultstore.BaselineFile{ContentHash: contentHash, Fingerprint: fingerprint}
	}

	return files, nil
}
