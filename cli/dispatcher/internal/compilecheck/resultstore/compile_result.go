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
	// nothing about whether this one would compile the same thing. So is the order the sources are
	// handed to csc in, which decides the bytes of what it writes without being in the key: version 1
	// compiled them in ordinal order rather than Unity's. Version 2 had no shape, so it could not say
	// whether it may still apply while some of its references are compiling.
	resultManifestFormatVersion = 3

	resultManifestExtension = ".result.json"
)

// CompileResult is what one compile of an assembly reported.
type CompileResult[D any] struct {
	Succeeded   bool
	Diagnostics []D
}

// KeyIdentity is the input key a compile ran on, together with the parts of it that tell whether the
// compile may still apply while some of the project assemblies it references are compiling.
type KeyIdentity struct {
	Key string
	// Shape digests every input of the key except the references to project assemblies.
	Shape string
	// ProjectReferences maps each project assembly the unit references to the identity the key holds
	// for it.
	ProjectReferences map[string]string
}

// unitResultManifest is what one finished compile left behind for the next run: the inputs it ran
// on, what it reported, and the outputs it wrote.
type unitResultManifest[D any] struct {
	FormatVersion     int
	InputKey          string
	Shape             string
	ProjectReferences map[string]string
	Succeeded         bool
	Diagnostics       []D
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
	manifest, found := s.loadManifest(assembly)
	if !found || manifest.InputKey != key || key == "" {
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

// MayStillApply reports whether the assembly's recorded compile may become reusable once the
// references named in unknown finish compiling: every project reference outside unknown has the
// identity it was recorded with, the same project assemblies are referenced, the outputs still hold
// what that compile wrote, and shape gives the recorded shape.
// Why shape is a function: it reads every source of the assembly, and is asked for only when every
// other check has passed, which in most cases where the answer is no it has not.
// Why anything unexpected means no: the caller then compiles without waiting, which is what it did
// before this check existed.
func (s Store[D]) MayStillApply(
	assembly string, projectReferences map[string]string, unknown map[string]bool, shape func() (string, bool),
) bool {
	manifest, found := s.loadManifest(assembly)
	if !found || !sameKnownReferences(manifest.ProjectReferences, projectReferences, unknown) {
		return false
	}
	if !outputsStillMatch(s.directoryPath(), manifest.Outputs) {
		return false
	}
	current, computed := shape()

	return computed && current == manifest.Shape
}

// sameKnownReferences reports whether two sets of project reference identities name the same
// assemblies and agree on every one outside unknown.
func sameKnownReferences(recorded map[string]string, current map[string]string, unknown map[string]bool) bool {
	if len(recorded) != len(current) {
		return false
	}
	for name, identity := range current {
		recordedIdentity, found := recorded[name]
		if !found {
			return false
		}
		if !unknown[name] && recordedIdentity != identity {
			return false
		}
	}

	return true
}

// loadManifest reads the assembly's manifest when one of the current format is on disk.
func (s Store[D]) loadManifest(assembly string) (unitResultManifest[D], bool) {
	content, err := os.ReadFile(manifestPath(s.directoryPath(), assembly))
	if err != nil {
		return unitResultManifest[D]{}, false
	}

	var manifest unitResultManifest[D]
	if err := json.Unmarshal(content, &manifest); err != nil {
		return unitResultManifest[D]{}, false
	}

	return manifest, manifest.FormatVersion == resultManifestFormatVersion
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

// RecordCompile records what a compile of the assembly ran on (identity) and what it reported, together
// with the outputs it wrote, named in the output directory. It records nothing when that compile
// claimed success without producing every one of those outputs.
// Why it is written through a temporary file: a run interrupted while writing would otherwise leave
// half a manifest behind, and the next run would read it as a description of outputs it never saw.
func (s Store[D]) RecordCompile(
	assembly string, identity KeyIdentity, result CompileResult[D], outputs []string,
) error {
	hashes, complete, err := hashOutputs(s.directoryPath(), outputs, result.Succeeded)
	if err != nil {
		return err
	}
	if !complete {
		return nil
	}

	content, err := json.Marshal(unitResultManifest[D]{
		FormatVersion:     resultManifestFormatVersion,
		InputKey:          identity.Key,
		Shape:             identity.Shape,
		ProjectReferences: identity.ProjectReferences,
		Succeeded:         result.Succeeded,
		Diagnostics:       result.Diagnostics,
		Outputs:           hashes,
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
