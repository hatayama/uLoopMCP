package compilecheck

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io/fs"
	"os"
	"path/filepath"

	"github.com/hatayama/unity-cli-loop/dispatcher/internal/compilecheck/fastcheck"
)

// baselineRecord is what a successful full compile leaves for the next changed-files check.
type baselineRecord struct {
	FormatVersion int
	// InputKey is the key of the full compile this baseline describes; its manifest carries the same.
	InputKey string
	// NonSourceKey covers every input but the source contents, so any other change compiles in full.
	NonSourceKey string
	// WorkerKey names the worker build that computed the fingerprints.
	WorkerKey          string
	GeneratedDirectory string // relative to the project root
	// GeneratedFiles maps each generated file, relative to GeneratedDirectory, to its content hash,
	// so a check never runs without the generator output its baseline compile had.
	GeneratedFiles map[string]string
	Files          map[string]baselineFile
}

// baselineFile describes one source as the full compile saw it.
type baselineFile struct {
	ContentHash string
	Fingerprint string
}

// RecordBaseline records what the next changed-files check of a unit compares against, after a
// successful full compile. nonSourceKey is the unit's non-source key computed before the compile.
// Why every content hash must match the one the input key read, and the non-source key the one
// computed before the compile: the manifest describes the inputs csc compiled, and an input changed
// during the compile would otherwise enter the baseline as if that compile had seen it, and its
// effect would never be checked by the next run.
func (runner *fastPathRunner) RecordBaseline(
	ctx context.Context, c Compiler, plan BuildPlan, unit CompileUnit, keyResult inputKeyResult, nonSourceKey string,
) error {
	if nonSourceKey == "" {
		return errors.New("inputs other than source contents could not be read before the compile")
	}
	currentNonSourceKey, err := computeNonSourceKey(c.ProjectRoot, c.Paths, plan, unit)
	if err != nil {
		return err
	}
	if currentNonSourceKey != nonSourceKey {
		return errors.New("inputs other than source contents changed during the compile")
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

	generatedFiles, err := hashGeneratedFiles(filepath.Join(c.ProjectRoot, generatedDirectory(plan, unit)))
	if err != nil {
		return err
	}

	return writeBaselineRecord(filepath.Join(c.ProjectRoot, plan.OutputDir), unit, baselineRecord{
		FormatVersion:      baselineRecordFormatVersion,
		InputKey:           keyResult.Key,
		NonSourceKey:       nonSourceKey,
		WorkerKey:          worker.Key,
		GeneratedDirectory: filepath.ToSlash(generatedDirectory(plan, unit)),
		GeneratedFiles:     generatedFiles,
		Files:              files,
	})
}

// hashGeneratedFiles hashes every generated C# file under directoryPath, keyed by its slash-separated
// path relative to it. A missing directory holds no files.
func hashGeneratedFiles(directoryPath string) (map[string]string, error) {
	hashes := map[string]string{}
	err := filepath.WalkDir(directoryPath, func(path string, entry fs.DirEntry, walkErr error) error {
		if walkErr != nil {
			if os.IsNotExist(walkErr) && path == directoryPath {
				return filepath.SkipDir
			}

			return walkErr
		}
		if entry.IsDir() || filepath.Ext(path) != ".cs" {
			return nil
		}
		relative, err := filepath.Rel(directoryPath, path)
		if err != nil {
			return err
		}
		hash, err := hashFile(path)
		if err != nil {
			return err
		}
		hashes[filepath.ToSlash(relative)] = hash

		return nil
	})
	if err != nil {
		return nil, fmt.Errorf("failed to read the generator output: %w", err)
	}

	return hashes, nil
}

// generatedFilesMatch reports whether the generator output on disk is exactly what the baseline
// recorded: no file missing, added, or changed.
// Why: the worker reads this output instead of running the generators, and checking without it
// reports errors wherever code uses what the generators produce.
func generatedFilesMatch(directoryPath string, recorded map[string]string) bool {
	current, err := hashGeneratedFiles(directoryPath)
	if err != nil || len(current) != len(recorded) {
		return false
	}
	for name, hash := range recorded {
		if current[name] != hash {
			return false
		}
	}

	return true
}

// baselineFiles pairs each source's content hash, as the input key read it, with its fingerprint.
func baselineFiles(
	projectRoot string, unit CompileUnit, keyResult inputKeyResult, fingerprints fastcheck.FingerprintResponse,
) (map[string]baselineFile, error) {
	files := make(map[string]baselineFile, len(unit.Sources))
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
		files[source] = baselineFile{ContentHash: contentHash, Fingerprint: fingerprint}
	}

	return files, nil
}

// writeBaselineRecord writes a baseline through a temporary file, like the manifest.
func writeBaselineRecord(outputDirectoryPath string, unit CompileUnit, record baselineRecord) error {
	content, err := json.Marshal(record)
	if err != nil {
		return fmt.Errorf("failed to record the baseline of %s: %w", unit.Assembly.AssemblyName, err)
	}

	return writeFileAtomically(outputDirectoryPath, baselineRecordPath(outputDirectoryPath, unit), content)
}

// baselineRecordPath names where one unit's baseline lives.
func baselineRecordPath(outputDirectoryPath string, unit CompileUnit) string {
	return filepath.Join(outputDirectoryPath, unit.Assembly.AssemblyName+baselineRecordExtension)
}

// generatedDirectory names, relative to the project root, where a full compile saves the generator
// output of a unit.
func generatedDirectory(plan BuildPlan, unit CompileUnit) string {
	return filepath.Join(plan.OutputDir, unit.Assembly.AssemblyName+generatedDirectoryExtension)
}
