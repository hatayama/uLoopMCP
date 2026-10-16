package compilecheck

import (
	"crypto/sha256"
	"encoding/hex"
	"fmt"
	"hash"
	"io"

	"github.com/hatayama/unity-cli-loop/dispatcher/internal/compilecheck/resultstore"
)

// pathValuedFlagNames are the flags whose value names a file the project edits and csc reads: they
// decide which diagnostics come out, so the key follows what is inside them rather than their
// timestamp - a checkout that restores a ruleset writes a new timestamp on the same content.
var pathValuedFlagNames = []string{"ruleset", "additionalfile", "analyzerconfig"}

// untrackedFileInputFlagNames are the flags naming a file csc reads that this key does not follow.
// Why a unit carrying one gets no key at all: reusing its diagnostics would mean reusing them
// across an edit to a file nothing here watches. Flags that only name where csc writes are absent
// from this list, because what a compile writes cannot change what it reports.
var untrackedFileInputFlagNames = []string{
	"keyfile", "win32manifest", "win32res", "win32icon", "resource", "linkresource",
	"embed", "link", "addmodule", "appconfig", "sourcelink",
}

// inputKeyResult is an input key together with the content hash of each source it was computed from.
type inputKeyResult struct {
	Key string
	// SourceHashes maps each source, spelled as unit.Sources spells it, to the sha256 of the bytes
	// the key read from it.
	SourceHashes map[string]string
	// Shape digests the same inputs as Key, in the same order, except the references to project
	// assemblies, which ProjectReferences carries one by one instead.
	// Why they are split out: a reference still compiling is read from Unity's build when a unit
	// starts before it finishes, and from this run's output when it changed. Comparing the rest
	// apart from it tells whether a recorded compile can still be replayed once it finishes.
	Shape string
	// ProjectReferences maps each project assembly the unit references, in the plan or not, to the
	// sha256 of the line Key holds for it and whether that line reads this run's output.
	// Why the plan does not decide which references go here: a reference entering or leaving the
	// plan leaves the bytes of Key as they were, so it must leave these as they were too.
	ProjectReferences map[string]resultstore.ReferenceIdentity
}

// identity is what a record of a compile under this key is kept with.
func (result inputKeyResult) identity() resultstore.KeyIdentity {
	return resultstore.KeyIdentity{Key: result.Key, Shape: result.Shape, ProjectReferences: result.ProjectReferences}
}

// computeInputKey digests everything that decides what csc reports for one assembly, so that two
// runs agreeing on this key would get the same diagnostics out of the compiler.
// Why an error rather than a key when an input cannot be read: a key built from what happens to be
// readable would match a later run that reads the same subset, and the compile it stands for never
// ran. The caller compiles such a unit instead of reusing anything.
// Why the source hashes come back with the key: a baseline recorded for the changed-files check
// must describe the very bytes this key, and so the compile it stands for, saw. Reading the sources
// again after the compile would let an edit made during it into the baseline unchecked.
func computeInputKey(
	projectRoot string, paths EditorCompilerPaths, plan BuildPlan, unit CompileUnit,
) (inputKeyResult, error) {
	sourceHashes := map[string]string{}
	key, err := computeKeyWith(projectRoot, paths, plan, unit, func(digest hash.Hash) error {
		return writeSourceIdentity(digest, projectRoot, unit.Sources, sourceHashes)
	})
	if err != nil {
		return inputKeyResult{}, err
	}
	shape, projectReferences, err := computeShape(projectRoot, paths, plan, unit, sourceHashes)
	if err != nil {
		return inputKeyResult{}, err
	}

	return inputKeyResult{
		Key: key, SourceHashes: sourceHashes, Shape: shape, ProjectReferences: projectReferences,
	}, nil
}

// computeShape digests the parts of the input key in the same order, writing the sources from the
// hashes the key read and leaving each project assembly reference out of the digest, and reports
// the identity of each of those references apart.
// Why the sources are not read again: the shape must describe the very bytes the key does.
func computeShape(
	projectRoot string, paths EditorCompilerPaths, plan BuildPlan, unit CompileUnit,
	sourceHashes map[string]string,
) (string, map[string]resultstore.ReferenceIdentity, error) {
	projectReferences := map[string]resultstore.ReferenceIdentity{}
	parts := keyParts(projectRoot, paths, plan, unit, func(digest hash.Hash) error {
		writeHashedSources(digest, sourceHashes)

		return nil
	})
	parts[referencesPartIndex].write = func(digest hash.Hash) error {
		return writeShapeReferenceIdentity(digest, projectRoot, plan, unit, projectReferences)
	}
	digest := sha256.New()
	for _, part := range parts {
		if err := part.write(digest); err != nil {
			return "", nil, err
		}
	}

	return hex.EncodeToString(digest.Sum(nil)), projectReferences, nil
}

// computeProjectReferences reports the identity of each project assembly the unit references,
// reading those references alone.
// Why apart from the key: it answers whether a record can still apply without reading the sources,
// and differs in most cases where the answer is no.
func computeProjectReferences(
	projectRoot string, plan BuildPlan, unit CompileUnit,
) (map[string]resultstore.ReferenceIdentity, error) {
	projectReferences := map[string]resultstore.ReferenceIdentity{}
	err := writeShapeReferenceIdentity(io.Discard, projectRoot, plan, unit, projectReferences)

	return projectReferences, err
}

// keyStillHolds reports whether the unit's inputs still give the key the run started from.
func (c Compiler) keyStillHolds(plan BuildPlan, unit CompileUnit, key string) bool {
	rechecked, err := computeInputKey(c.ProjectRoot, c.Paths, plan, unit)

	return err == nil && rechecked.Key == key
}

// nonSourceIdentity is the non-source key of a unit together with a digest of each kind of input it
// covers, so a fallback can say which kind changed.
type nonSourceIdentity struct {
	Key string
	// Parts maps each kind of input, named as a fallback reason names it, to its digest.
	Parts map[string]string
}

// keyPart is one kind of input of a unit and how it goes into a digest.
type keyPart struct {
	name  string
	write func(hash.Hash) error
}

// computeNonSourceKey digests the same inputs as the input key except the source contents, which it
// replaces with the list of source paths. Two runs agreeing on it differ at most in what the
// sources hold; adding or removing a source changes it.
func computeNonSourceKey(
	projectRoot string, paths EditorCompilerPaths, plan BuildPlan, unit CompileUnit,
) (nonSourceIdentity, error) {
	parts := keyParts(projectRoot, paths, plan, unit, func(digest hash.Hash) error {
		writeSourceNames(digest, unit.Sources)

		return nil
	})
	identity := nonSourceIdentity{Parts: make(map[string]string, len(parts))}
	digest := sha256.New()
	for _, part := range parts {
		partDigest := sha256.New()
		if err := part.write(partDigest); err != nil {
			return nonSourceIdentity{}, err
		}
		partKey := hex.EncodeToString(partDigest.Sum(nil))
		identity.Parts[part.name] = partKey
		_, _ = fmt.Fprintf(digest, "%s\x00%s\n", part.name, partKey)
	}
	identity.Key = hex.EncodeToString(digest.Sum(nil))

	return identity, nil
}

// changedInputKinds names, in a fixed order, the kinds of input whose digest differs between two
// non-source identities of the same unit.
func changedInputKinds(current nonSourceIdentity, recorded nonSourceIdentity) []string {
	kinds := []string{}
	for _, name := range keyPartNames {
		if current.Parts[name] != recorded.Parts[name] {
			kinds = append(kinds, name)
		}
	}

	return kinds
}

// keyPartNames lists the kinds of input in the order keyParts digests them.
var keyPartNames = []string{
	"compiler", "response file", "source list", "references", "analyzers", "analyzer inputs", "output name",
}

// referencesPartIndex is where keyParts digests the references.
const referencesPartIndex = 3

// keyParts lists every kind of input of one unit, letting the caller decide how the sources go in.
func keyParts(
	projectRoot string, paths EditorCompilerPaths, plan BuildPlan, unit CompileUnit,
	sourceWriter func(hash.Hash) error,
) []keyPart {
	return []keyPart{
		{keyPartNames[0], func(digest hash.Hash) error { return writeCompilerIdentity(digest, paths) }},
		{keyPartNames[1], func(digest hash.Hash) error { return writeResponseFileIdentity(digest, unit.Assembly) }},
		{keyPartNames[2], sourceWriter},
		{keyPartNames[referencesPartIndex], func(digest hash.Hash) error {
			return writeReferenceIdentity(digest, projectRoot, plan, unit)
		}},
		{keyPartNames[4], func(digest hash.Hash) error {
			return writeAnalyzerIdentity(digest, projectRoot, unit.Assembly.Analyzers)
		}},
		{keyPartNames[5], func(digest hash.Hash) error {
			return writeAnalyzerInputIdentity(digest, projectRoot, unit.Assembly)
		}},
		{keyPartNames[6], func(digest hash.Hash) error {
			writeOutputIdentity(digest, unit.Assembly)

			return nil
		}},
	}
}

// computeKeyWith digests every input of one unit into one digest, letting the caller decide how the
// sources go in.
// Why one digest rather than the parts: the input key names manifests that earlier runs wrote, and
// keeping its bytes as they were keeps those manifests reusable.
func computeKeyWith(
	projectRoot string, paths EditorCompilerPaths, plan BuildPlan, unit CompileUnit,
	sourceWriter func(hash.Hash) error,
) (string, error) {
	digest := sha256.New()
	for _, part := range keyParts(projectRoot, paths, plan, unit, sourceWriter) {
		if err := part.write(digest); err != nil {
			return "", err
		}
	}

	return hex.EncodeToString(digest.Sum(nil)), nil
}
