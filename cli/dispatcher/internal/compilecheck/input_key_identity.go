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
		if _, err := writeReferenceLine(digest, projectRoot, plan, unit, reference); err != nil {
			return err
		}
	}

	return nil
}

// writeShapeReferenceIdentity writes the lines writeReferenceIdentity writes for every reference
// that is not a project assembly, and puts the sha256 of the line of each one that is, with which
// file that line reads, into projectReferences under its name.
// Why a name seen twice is simply overwritten: the same reference resolves to the same line.
func writeShapeReferenceIdentity(
	digest io.Writer, projectRoot string, plan BuildPlan, unit CompileUnit,
	projectReferences map[string]resultstore.ReferenceIdentity,
) error {
	for _, reference := range unit.Assembly.References {
		name, isProjectAssembly := projectAssemblyReferenceName(reference, plan.DagDir)
		if !isProjectAssembly {
			if _, err := writeReferenceLine(digest, projectRoot, plan, unit, reference); err != nil {
				return err
			}

			continue
		}
		var line bytes.Buffer
		produced, err := writeReferenceLine(&line, projectRoot, plan, unit, reference)
		if err != nil {
			return err
		}
		sum := sha256.Sum256(line.Bytes())
		kind := resultstore.UnityReference
		if produced {
			kind = resultstore.ProducedReference
		}
		projectReferences[name] = resultstore.ReferenceIdentity{Kind: kind, Hash: hex.EncodeToString(sum[:])}
	}

	return nil
}

// writeReferenceLine writes the line one reference contributes to the key, and reports whether that
// line reads this run's output rather than Unity's build.
func writeReferenceLine(
	digest io.Writer, projectRoot string, plan BuildPlan, unit CompileUnit, reference string,
) (bool, error) {
	resolved := rewriteReference(projectRoot, plan, unit, reference)
	path := projectPath(projectRoot, resolved)
	if isUnderDirectory(resolved, plan.OutputDir) {
		return true, writeFileContentEntry(digest, "produced-reference", resolved, path)
	}

	return false, writeStatEntry(digest, "reference", resolved, path)
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
