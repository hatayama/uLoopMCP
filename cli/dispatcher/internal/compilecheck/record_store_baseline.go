package compilecheck

import (
	"encoding/json"
	"fmt"
	"io/fs"
	"os"
	"path/filepath"
)

const (
	baselineRecordFormatVersion = 3
	baselineRecordExtension     = ".fastcheck.json"
	generatedDirectoryExtension = ".generated"
)

// baselineRecord is what a successful full compile leaves for the next changed-files check.
type baselineRecord struct {
	FormatVersion int
	// InputKey is the key of the full compile this baseline describes; its manifest carries the same.
	InputKey string
	// NonSourceKey covers every input but the source contents, so any other change compiles in full.
	NonSourceKey nonSourceIdentity
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

// baselineInputs is what the caller knows about the full compile a baseline describes: its input
// key, its non-source key, the worker build that fingerprinted its sources, and each source.
type baselineInputs struct {
	InputKey     string
	NonSourceKey nonSourceIdentity
	WorkerKey    string
	Files        map[string]baselineFile
}

// storedBaseline is a recorded baseline together with what its full compile reported.
type storedBaseline struct {
	InputKey     string
	NonSourceKey nonSourceIdentity
	WorkerKey    string
	Files        map[string]baselineFile
	// GeneratedDirectory is relative to the project root and slash-separated, as recorded.
	GeneratedDirectory string
	// Diagnostics is what the full compile this baseline describes reported.
	Diagnostics    []Diagnostic
	generatedFiles map[string]string
}

// generatedDirectory names, relative to the project root, where a full compile saves the generator
// output of an assembly.
func (s recordStore) generatedDirectory(assembly string) string {
	return filepath.Join(s.outputDir, assembly+generatedDirectoryExtension)
}

// loadBaseline reads the baseline of an assembly together with what the full compile it describes
// reported, or says why there is none to use.
func (s recordStore) loadBaseline(assembly string) (storedBaseline, string) {
	content, err := os.ReadFile(baselineRecordPath(s.directoryPath(), assembly))
	if err != nil {
		return storedBaseline{}, s.missingBaselineReason(assembly)
	}

	var record baselineRecord
	if err := json.Unmarshal(content, &record); err != nil || record.FormatVersion != baselineRecordFormatVersion {
		return storedBaseline{}, "no baseline: it was recorded by another version of compile-check"
	}

	// In the normal flow the record and the manifest are written together under one key, and a full
	// compile removes both, so the checks below only catch a state someone changed by hand.
	const goneFromDisk = "baseline compile is no longer on disk"
	manifestContent, err := os.ReadFile(manifestPath(s.directoryPath(), assembly))
	if err != nil {
		return storedBaseline{}, goneFromDisk
	}

	var manifest unitResultManifest
	if err := json.Unmarshal(manifestContent, &manifest); err != nil ||
		manifest.InputKey != record.InputKey || !manifest.Succeeded ||
		!outputsStillMatch(s.directoryPath(), manifest.Outputs) {
		return storedBaseline{}, goneFromDisk
	}

	return storedBaseline{
		InputKey:           record.InputKey,
		NonSourceKey:       record.NonSourceKey,
		WorkerKey:          record.WorkerKey,
		Files:              record.Files,
		GeneratedDirectory: record.GeneratedDirectory,
		Diagnostics:        manifest.Diagnostics,
		generatedFiles:     record.GeneratedFiles,
	}, ""
}

// missingBaselineReason says why an assembly has no baseline, from the manifest of its last full
// compile.
// Why the reasons are told apart: a full compile with errors drops the baseline an earlier compile
// left, and a reason that read as "never compiled" sent users looking for a compile that had run.
// Why a missing manifest says nothing about what happened: a run that skips or blocks the unit
// removes the manifest and the baseline alike, so nothing on disk tells that apart from no compile.
func (s recordStore) missingBaselineReason(assembly string) string {
	const notOnRecord = "no baseline: no full compile of this assembly is on record"
	content, err := os.ReadFile(manifestPath(s.directoryPath(), assembly))
	if err != nil {
		return notOnRecord
	}

	var manifest unitResultManifest
	if err := json.Unmarshal(content, &manifest); err != nil {
		return notOnRecord
	}
	if !manifest.Succeeded {
		return "no baseline: the last full compile had errors"
	}

	return "no baseline: the last full compile did not record one"
}

// generatorOutputUnchanged reports whether the generator output on disk is exactly what the
// baseline recorded.
func (s recordStore) generatorOutputUnchanged(baseline storedBaseline) bool {
	return generatedFilesMatch(projectPath(s.projectRoot, baseline.GeneratedDirectory), baseline.generatedFiles)
}

// recordBaseline records what the next changed-files check of an assembly compares against, with
// the generator output its full compile saved.
func (s recordStore) recordBaseline(assembly string, inputs baselineInputs) error {
	generatedDirectory := s.generatedDirectory(assembly)
	generatedFiles, err := hashGeneratedFiles(filepath.Join(s.projectRoot, generatedDirectory))
	if err != nil {
		return err
	}

	return writeBaselineRecord(s.directoryPath(), assembly, baselineRecord{
		FormatVersion:      baselineRecordFormatVersion,
		InputKey:           inputs.InputKey,
		NonSourceKey:       inputs.NonSourceKey,
		WorkerKey:          inputs.WorkerKey,
		GeneratedDirectory: filepath.ToSlash(generatedDirectory),
		GeneratedFiles:     generatedFiles,
		Files:              inputs.Files,
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

// writeBaselineRecord writes a baseline through a temporary file, like the manifest.
func writeBaselineRecord(directoryPath string, assembly string, record baselineRecord) error {
	content, err := json.Marshal(record)
	if err != nil {
		return fmt.Errorf("failed to record the baseline of %s: %w", assembly, err)
	}

	return writeFileAtomically(directoryPath, baselineRecordPath(directoryPath, assembly), content)
}

// baselineRecordPath names where one assembly's baseline lives.
func baselineRecordPath(directoryPath string, assembly string) string {
	return filepath.Join(directoryPath, assembly+baselineRecordExtension)
}
