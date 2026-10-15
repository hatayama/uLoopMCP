package compilecheck

import (
	"bytes"
	"crypto/sha256"
	"encoding/hex"
	"fmt"
	"hash"
	"io"
	"os"
	"path/filepath"
	"sort"
	"strings"

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
	// sha256 of the line Key holds for it.
	// Why the plan does not decide which references go here: a reference entering or leaving the
	// plan leaves the bytes of Key as they were, so it must leave these as they were too.
	ProjectReferences map[string]string
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
) (string, map[string]string, error) {
	projectReferences := map[string]string{}
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
) (map[string]string, error) {
	projectReferences := map[string]string{}
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

// writeCompilerIdentity records which compiler would run: another one reports other diagnostics.
func writeCompilerIdentity(digest hash.Hash, paths EditorCompilerPaths) error {
	for _, path := range []string{paths.CompilerDllPath, paths.DotnetHostPath} {
		if err := writeStatEntry(digest, "compiler", path, path); err != nil {
			return err
		}
	}

	return nil
}

// writeResponseFileIdentity records the response files Bee wrote for this assembly, which carry its
// defines, its analyzer list and the path mapping its metadata is built with.
func writeResponseFileIdentity(digest hash.Hash, rsp ResponseFile) error {
	companionPath := strings.TrimSuffix(rsp.Path, responseFileExtension) + companionFileExtension
	// Why only the companion file may be absent: Bee writes one for some assemblies and not others,
	// so the ones it left out are keyed on that absence. It writes the primary response file for
	// every assembly it builds, so a run that cannot find one is looking at a state this key cannot
	// describe - and keying on its absence would let two different states hash the same.
	content, err := os.ReadFile(rsp.Path)
	if err != nil {
		return fmt.Errorf("failed to read %s: %w", rsp.Path, err)
	}
	writeContentEntry(digest, "response", filepath.Base(rsp.Path), content)

	companionContent, err := os.ReadFile(companionPath)
	if err != nil && !os.IsNotExist(err) {
		return fmt.Errorf("failed to read %s: %w", companionPath, err)
	}
	writeContentEntry(digest, "response", filepath.Base(companionPath), companionContent)

	return nil
}

// writeSourceIdentity records what the assembly compiles, by content and in a fixed order, and
// fills hashes with the content hash of each source it read.
// Why the content rather than the modification time: a checkout that puts a file back the way it
// was leaves it newer than the build, and recompiling it would report the diagnostics it already
// reported. The whole source set of the largest assembly hashes in tens of milliseconds.
func writeSourceIdentity(
	digest hash.Hash, projectRoot string, sources []string, hashes map[string]string,
) error {
	ordered := append([]string{}, sources...)
	sort.Strings(ordered)
	for _, source := range ordered {
		path := projectPath(projectRoot, source)
		contentHash, err := hashFile(path)
		if err != nil {
			return fmt.Errorf("failed to read %s: %w", path, err)
		}
		hashes[source] = contentHash
		_, _ = fmt.Fprintf(digest, "source\x00%s\x00%s\n", filepath.ToSlash(source), contentHash)
	}

	return nil
}

// writeHashedSources writes the same lines writeSourceIdentity writes, from hashes it already read.
func writeHashedSources(digest io.Writer, hashes map[string]string) {
	ordered := make([]string, 0, len(hashes))
	for source := range hashes {
		ordered = append(ordered, source)
	}
	sort.Strings(ordered)
	for _, source := range ordered {
		_, _ = fmt.Fprintf(digest, "source\x00%s\x00%s\n", filepath.ToSlash(source), hashes[source])
	}
}

// writeSourceNames records which sources the assembly compiles, without what they hold.
func writeSourceNames(digest hash.Hash, sources []string) {
	ordered := append([]string{}, sources...)
	sort.Strings(ordered)
	for _, source := range ordered {
		_, _ = fmt.Fprintf(digest, "source-name\x00%s\n", filepath.ToSlash(source))
	}
}

// writeReferenceIdentity records the assemblies this one compiles against, resolved the way the
// compile itself resolves them.
// Why the two rules: a reference this run rebuilt into a different surface is read by content,
// because a later rebuild can write the same bytes again with a new timestamp. Everything else -
// including a rebuilt reference that kept Unity's surface, which the compile reads from Unity's file -
// is read by timestamp, which moves whenever Unity rebuilt it, and a compile that did not need it
// costs less than a missed error.
func writeReferenceIdentity(
	digest io.Writer, projectRoot string, plan BuildPlan, unit CompileUnit,
) error {
	for _, reference := range unit.Assembly.References {
		if err := writeReferenceLine(digest, projectRoot, plan, unit, reference); err != nil {
			return err
		}
	}

	return nil
}

// writeShapeReferenceIdentity writes the lines writeReferenceIdentity writes for every reference
// that is not a project assembly, and puts the sha256 of the line of each one that is into
// projectReferences under its name.
// Why a name seen twice is simply overwritten: the same reference resolves to the same line.
func writeShapeReferenceIdentity(
	digest io.Writer, projectRoot string, plan BuildPlan, unit CompileUnit,
	projectReferences map[string]string,
) error {
	for _, reference := range unit.Assembly.References {
		name, isProjectAssembly := projectAssemblyReferenceName(reference, plan.DagDir)
		if !isProjectAssembly {
			if err := writeReferenceLine(digest, projectRoot, plan, unit, reference); err != nil {
				return err
			}

			continue
		}
		var line bytes.Buffer
		if err := writeReferenceLine(&line, projectRoot, plan, unit, reference); err != nil {
			return err
		}
		sum := sha256.Sum256(line.Bytes())
		projectReferences[name] = hex.EncodeToString(sum[:])
	}

	return nil
}

// writeReferenceLine writes the line one reference contributes to the key.
func writeReferenceLine(
	digest io.Writer, projectRoot string, plan BuildPlan, unit CompileUnit, reference string,
) error {
	resolved := rewriteReference(projectRoot, plan, unit, reference)
	path := projectPath(projectRoot, resolved)
	if isUnderDirectory(resolved, plan.OutputDir) {
		return writeFileContentEntry(digest, "produced-reference", resolved, path)
	}

	return writeStatEntry(digest, "reference", resolved, path)
}

// writeAnalyzerIdentity records the analyzer assemblies csc loads, which are shipped rather than
// edited, so their timestamps answer whether they moved.
func writeAnalyzerIdentity(digest hash.Hash, projectRoot string, analyzers []string) error {
	for _, analyzer := range analyzers {
		if err := writeStatEntry(digest, "analyzer", analyzer, projectPath(projectRoot, analyzer)); err != nil {
			return err
		}
	}

	return nil
}

// writeAnalyzerInputIdentity records the files the analyzers read - a ruleset, a banned-symbol list,
// an editorconfig handed over as /analyzerconfig - and refuses a key for a flag naming a file this
// digest does not follow.
func writeAnalyzerInputIdentity(digest hash.Hash, projectRoot string, rsp ResponseFile) error {
	inputs := []string{}
	if rsp.AdditionalFile != "" {
		inputs = append(inputs, rsp.AdditionalFile)
	}
	// Why both flag lists: the companion lines are Bee's second response file, and compiler.go
	// appends them to the invocation verbatim, so a flag naming a file csc reads decides the
	// diagnostics no matter which of the two files Bee put it in.
	for _, flags := range [][]string{rsp.OtherFlags, rsp.CompanionFlags} {
		for _, flag := range flags {
			value, isPathValued := pathValuedFlag(flag)
			if isPathValued {
				inputs = append(inputs, value)

				continue
			}
			if untrackedFileInputFlag(flag) {
				return fmt.Errorf("response file flag %q names a file the input key does not follow", flag)
			}
		}
	}

	for _, input := range inputs {
		if err := writeFileContentEntry(
			digest, "analyzer-input", input, projectPath(projectRoot, input)); err != nil {
			return err
		}
	}

	return nil
}

// writeOutputIdentity records what the compile is named, which is part of what it produces.
func writeOutputIdentity(digest hash.Hash, rsp ResponseFile) {
	for _, path := range []string{rsp.OutputPath, rsp.RefOutputPath} {
		_, _ = fmt.Fprintf(digest, "output\x00%s\n", filepath.Base(path))
	}
}

// pathValuedFlag reads the file path out of a flag whose content belongs in the key.
func pathValuedFlag(line string) (string, bool) {
	name, value, ok := splitFlag(line)
	if !ok {
		return "", false
	}
	for _, candidate := range pathValuedFlagNames {
		if name == candidate {
			return unquotePath(value), true
		}
	}

	return "", false
}

// untrackedFileInputFlag reports whether a flag names a file csc reads that the key does not follow.
func untrackedFileInputFlag(line string) bool {
	name, _, ok := splitFlag(line)
	if !ok {
		return false
	}
	for _, candidate := range untrackedFileInputFlagNames {
		if name == candidate {
			return true
		}
	}

	return false
}

// splitFlag reads one response file line as a flag name and its value.
// Why the name is lowered and both flag markers are accepted: csc treats -ruleset:, /ruleset: and
// /RuleSet: as the same flag, and a comparison that did not would silently follow none of them.
func splitFlag(line string) (string, string, bool) {
	if line == "" || (line[0] != '-' && line[0] != '/') {
		return "", "", false
	}
	name, value, found := strings.Cut(line[1:], ":")
	if !found {
		return "", "", false
	}

	return strings.ToLower(name), value, true
}

// writeStatEntry records a file by where it is, how big it is and when it last changed.
func writeStatEntry(digest io.Writer, kind string, name string, path string) error {
	info, err := os.Stat(path)
	if err != nil {
		return fmt.Errorf("failed to read %s: %w", path, err)
	}
	_, _ = fmt.Fprintf(digest, "%s\x00%s\x00%d\x00%d\n",
		kind, filepath.ToSlash(name), info.Size(), info.ModTime().UnixNano())

	return nil
}

// writeFileContentEntry records a file by where it is and what it holds.
func writeFileContentEntry(digest io.Writer, kind string, name string, path string) error {
	file, err := os.Open(path)
	if err != nil {
		return fmt.Errorf("failed to read %s: %w", path, err)
	}
	defer func() { _ = file.Close() }()

	content := sha256.New()
	if _, err := io.Copy(content, file); err != nil {
		return fmt.Errorf("failed to read %s: %w", path, err)
	}
	_, _ = fmt.Fprintf(digest, "%s\x00%s\x00%s\n",
		kind, filepath.ToSlash(name), hex.EncodeToString(content.Sum(nil)))

	return nil
}

// writeContentEntry records bytes already in hand under a name.
func writeContentEntry(digest hash.Hash, kind string, name string, content []byte) {
	sum := sha256.Sum256(content)
	_, _ = fmt.Fprintf(digest, "%s\x00%s\x00%s\n", kind, filepath.ToSlash(name), hex.EncodeToString(sum[:]))
}

// projectPath resolves a path a response file spells relative to the project root.
func projectPath(projectRoot string, path string) string {
	if filepath.IsAbs(path) {
		return path
	}

	return filepath.Join(projectRoot, path)
}
