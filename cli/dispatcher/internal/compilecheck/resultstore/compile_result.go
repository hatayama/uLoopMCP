package resultstore

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

	resultManifestExtension = ".result.json"
)

// CompileResult is what one compile of an assembly reported.
type CompileResult[D any] struct {
	Succeeded   bool
	Diagnostics []D
}

// unitResultManifest is what one finished compile left behind for the next run: the inputs it ran
// on, what it reported, and the outputs it wrote.
type unitResultManifest[D any] struct {
	FormatVersion int
	InputKey      string
	Succeeded     bool
	Diagnostics   []D
	// Outputs maps the name of each file the compile wrote in the check's output directory to its
	// content hash, so a run that finds them changed underneath compiles again.
	Outputs map[string]string
}

// manifestPath names where one assembly's manifest lives.
func manifestPath(directoryPath string, assembly string) string {
	return filepath.Join(directoryPath, assembly+resultManifestExtension)
}

// Reusable reports what a previous compile of the assembly found, when that compile ran on the
// inputs key describes and its outputs are still on disk exactly as it wrote them.
// Why anything unexpected simply means no: the caller compiles the unit, which is what it would
// have done anyway. Nothing here is worth failing a run over.
func (s Store[D]) Reusable(assembly string, key string) (CompileResult[D], bool) {
	content, err := os.ReadFile(manifestPath(s.directoryPath(), assembly))
	if err != nil {
		return CompileResult[D]{}, false
	}

	var manifest unitResultManifest[D]
	if err := json.Unmarshal(content, &manifest); err != nil {
		return CompileResult[D]{}, false
	}
	if manifest.FormatVersion != resultManifestFormatVersion || manifest.InputKey != key || key == "" {
		return CompileResult[D]{}, false
	}
	if !outputsStillMatch(s.directoryPath(), manifest.Outputs) {
		return CompileResult[D]{}, false
	}

	// Why the recorded outcome is replayed rather than assumed successful: a compile that reported
	// errors reports them again until one of its inputs moves, and swallowing them here would turn a
	// broken project into a clean run.
	return CompileResult[D]{Succeeded: manifest.Succeeded, Diagnostics: manifest.Diagnostics}, true
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

// RecordCompile records what a compile of the assembly ran on (key) and what it reported, together
// with the outputs it wrote, named in the output directory. It records nothing when that compile
// claimed success without producing every one of those outputs.
// Why it is written through a temporary file: a run interrupted while writing would otherwise leave
// half a manifest behind, and the next run would read it as a description of outputs it never saw.
func (s Store[D]) RecordCompile(assembly string, key string, result CompileResult[D], outputs []string) error {
	hashes, complete, err := hashOutputs(s.directoryPath(), outputs, result.Succeeded)
	if err != nil {
		return err
	}
	if !complete {
		return nil
	}

	content, err := json.Marshal(unitResultManifest[D]{
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
