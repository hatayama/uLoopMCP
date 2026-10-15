package compilecheck

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
)

const (
	// resultManifestFormatVersion guards against reading a manifest an older or newer build wrote:
	// what belongs in the key is part of this check's code, so a manifest from another version says
	// nothing about whether this one would compile the same thing.
	resultManifestFormatVersion = 1

	resultManifestExtension  = ".result.json"
	resultManifestTempSuffix = ".tmp"
)

// recordStore reads and writes what earlier runs of the check recorded for each assembly in one
// output directory: the result of its last compile, the baseline the changed-files check compares
// against, and the generator output that baseline was compiled with.
type recordStore struct {
	projectRoot string
	// outputDir is relative to the project root, which is how the records name the generator output.
	outputDir string
}

// recordedCompile is what one compile of an assembly reported.
type recordedCompile struct {
	Succeeded   bool
	Diagnostics []Diagnostic
}

// unitResultManifest is what one finished compile left behind for the next run: the inputs it ran
// on, what it reported, and the outputs it wrote.
type unitResultManifest struct {
	FormatVersion int
	InputKey      string
	Succeeded     bool
	Diagnostics   []Diagnostic
	// Outputs maps the name of each file the compile wrote in the check's output directory to its
	// content hash, so a run that finds them changed underneath compiles again.
	Outputs map[string]string
}

// openRecordStore opens the records kept in outputDir, a directory relative to projectRoot.
func openRecordStore(projectRoot string, outputDir string) recordStore {
	return recordStore{projectRoot: projectRoot, outputDir: outputDir}
}

// directoryPath names the output directory the records live in.
func (s recordStore) directoryPath() string {
	return filepath.Join(s.projectRoot, s.outputDir)
}

// manifestPath names where one assembly's manifest lives.
func manifestPath(directoryPath string, assembly string) string {
	return filepath.Join(directoryPath, assembly+resultManifestExtension)
}

// reusableResult reports what a previous compile of the assembly found, when that compile ran on
// the inputs key describes and its outputs are still on disk exactly as it wrote them.
// Why anything unexpected simply means no: the caller compiles the unit, which is what it would
// have done anyway. Nothing here is worth failing a run over.
func (s recordStore) reusableResult(assembly string, key string) (recordedCompile, bool) {
	content, err := os.ReadFile(manifestPath(s.directoryPath(), assembly))
	if err != nil {
		return recordedCompile{}, false
	}

	var manifest unitResultManifest
	if err := json.Unmarshal(content, &manifest); err != nil {
		return recordedCompile{}, false
	}
	if manifest.FormatVersion != resultManifestFormatVersion || manifest.InputKey != key || key == "" {
		return recordedCompile{}, false
	}
	if !outputsStillMatch(s.directoryPath(), manifest.Outputs) {
		return recordedCompile{}, false
	}

	// Why the recorded outcome is replayed rather than assumed successful: a compile that reported
	// errors reports them again until one of its inputs moves, and swallowing them here would turn a
	// broken project into a clean run.
	return recordedCompile{Succeeded: manifest.Succeeded, Diagnostics: manifest.Diagnostics}, true
}

// outputsStillMatch reports whether every file a compile wrote is still there and still holds what
// it wrote.
func outputsStillMatch(directoryPath string, outputs map[string]string) bool {
	for name, recordedHash := range outputs {
		currentHash, err := hashFile(filepath.Join(directoryPath, name))
		if err != nil || currentHash != recordedHash {
			return false
		}
	}

	return true
}

// recordCompile records what a compile of the assembly ran on and what it reported, together with
// the outputs it wrote, named in the output directory. It records nothing when that compile claimed
// success without producing every one of those outputs.
// Why it is written through a temporary file: a run interrupted while writing would otherwise leave
// half a manifest behind, and the next run would read it as a description of outputs it never saw.
func (s recordStore) recordCompile(assembly string, key string, result recordedCompile, outputs []string) error {
	hashes, complete, err := hashOutputs(s.directoryPath(), outputs, result.Succeeded)
	if err != nil {
		return err
	}
	if !complete {
		return nil
	}

	content, err := json.Marshal(unitResultManifest{
		FormatVersion: resultManifestFormatVersion,
		InputKey:      key,
		Succeeded:     result.Succeeded,
		Diagnostics:   result.Diagnostics,
		Outputs:       hashes,
	})
	if err != nil {
		return fmt.Errorf("failed to record the result of %s: %w", assembly, err)
	}

	return writeFileAtomically(s.directoryPath(), manifestPath(s.directoryPath(), assembly), content)
}

// writeFileAtomically writes content to path through a temporary file in directoryPath.
func writeFileAtomically(directoryPath string, path string, content []byte) error {
	// Why the temporary name is unique per writer: two runs of the same project writing the same
	// file at once must not rename each other's half-written file into place.
	temporary, err := os.CreateTemp(directoryPath, filepath.Base(path)+".*"+resultManifestTempSuffix)
	if err != nil {
		return fmt.Errorf("failed to write %s: %w", path, err)
	}
	temporaryPath := temporary.Name()
	if _, err := temporary.Write(content); err != nil {
		_ = temporary.Close()
		_ = os.Remove(temporaryPath)

		return fmt.Errorf("failed to write %s: %w", temporaryPath, err)
	}
	if err := temporary.Close(); err != nil {
		_ = os.Remove(temporaryPath)

		return fmt.Errorf("failed to write %s: %w", temporaryPath, err)
	}
	if err := os.Rename(temporaryPath, path); err != nil {
		_ = os.Remove(temporaryPath)

		return fmt.Errorf("failed to write %s: %w", path, err)
	}

	return nil
}

// hashOutputs hashes the named files a compile wrote in directoryPath.
// Why a missing output is tolerated only for a failed compile: that is the compile which writes
// neither the assembly nor its reference assembly. A successful compile that left one of them out
// is not a compile whose result describes anything - recording it would replay a success backed by
// files no run ever produced, and the outputs check would pass because it has nothing to check.
func hashOutputs(directoryPath string, names []string, succeeded bool) (map[string]string, bool, error) {
	outputs := map[string]string{}
	for _, name := range names {
		hashed, err := hashFile(filepath.Join(directoryPath, name))
		if os.IsNotExist(err) {
			if succeeded {
				return nil, false, nil
			}

			continue
		}
		if err != nil {
			return nil, false, err
		}
		outputs[name] = hashed
	}

	return outputs, true, nil
}

// forget removes every record of the assembly: its manifest, its baseline, and its generator output.
func (s recordStore) forget(assembly string) error {
	names := []string{
		// Why the manifest goes with the outputs: it describes those very files, so leaving it behind
		// would let a later run replay diagnostics for outputs this one removed.
		assembly + resultManifestExtension,
		// The baseline and the generator output describe the same compile as the manifest.
		assembly + baselineRecordExtension,
	}
	for _, name := range names {
		path := filepath.Join(s.directoryPath(), name)
		if err := os.Remove(path); err != nil && !os.IsNotExist(err) {
			return fmt.Errorf("failed to remove %s: %w", path, err)
		}
	}

	generatedPath := filepath.Join(s.directoryPath(), assembly+generatedDirectoryExtension)
	if err := os.RemoveAll(generatedPath); err != nil {
		return fmt.Errorf("failed to remove %s: %w", generatedPath, err)
	}

	return nil
}
