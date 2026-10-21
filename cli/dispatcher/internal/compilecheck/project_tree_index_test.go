package compilecheck

import (
	"errors"
	"path/filepath"
	"strings"
	"testing"
)

// Verifies that of two .asmdef files declaring the same assembly, the one the walk meets first wins:
// root order first, then the walk order inside a root, which is not the order of the full paths.
func TestIndexAssemblyDefinitionsKeepsTheFirstDuplicateInWalkOrder(t *testing.T) {
	projectRoot := t.TempDir()
	// "a" is walked before "a.b", while the full path ".../a.b/Y.asmdef" sorts before ".../a/X.asmdef".
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "a", "X.asmdef"), `{"name":"SiblingDup"}`)
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "a.b", "Y.asmdef"), `{"name":"SiblingDup"}`)
	// The directory "A" is walked before the file "A.asmdef" beside it, which sorts first as a path.
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "Z", "A", "X.asmdef"), `{"name":"NestedDup"}`)
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "Z", "A.asmdef"), `{"name":"NestedDup"}`)
	// Packages is walked before the package cache, whose path "Library/..." sorts first.
	writeFileAt(t, filepath.Join(projectRoot, "Packages", "Dup", "Dup.asmdef"), `{"name":"RootDup"}`)
	writeFileAt(t, filepath.Join(projectRoot, "Library", "PackageCache", "pkg", "Dup", "Dup.asmdef"), `{"name":"RootDup"}`)

	index, err := IndexAssemblyDefinitions(walkProjectTree(projectRoot))
	if err != nil {
		t.Fatalf("IndexAssemblyDefinitions returned an error: %v", err)
	}

	want := map[string]string{
		"SiblingDup": filepath.Join(projectRoot, "Assets", "a", "X.asmdef"),
		"NestedDup":  filepath.Join(projectRoot, "Assets", "Z", "A", "X.asmdef"),
		"RootDup":    filepath.Join(projectRoot, "Packages", "Dup", "Dup.asmdef"),
	}
	for name, wantPath := range want {
		if got := index[name].Path; got != wantPath {
			t.Fatalf("%s resolved to %s, want %s", name, got, wantPath)
		}
	}
}

// Verifies a broken .asmdef in an earlier root is reported ahead of a directory a later root cannot
// read, because indexing reads each root's files before it reports that root's walk error.
func TestIndexAssemblyDefinitionsReportsAnEarlierRootsBrokenAsmdefFirst(t *testing.T) {
	projectRoot := t.TempDir()
	broken := filepath.Join(projectRoot, "Assets", "Broken", "Broken.asmdef")
	writeFileAt(t, broken, "not json")
	lockDirectory(t, filepath.Join(projectRoot, "Packages", "Locked"))

	_, err := IndexAssemblyDefinitions(walkProjectTree(projectRoot))

	wantPrefix := "failed to index assembly definitions under " + filepath.Join(projectRoot, "Assets") + ": failed to read " + broken
	if err == nil || !strings.HasPrefix(err.Error(), wantPrefix) {
		t.Fatalf("err = %v, want it to start with %q", err, wantPrefix)
	}
}

// Verifies that inside one root a broken .asmdef the walk meets before a directory it cannot read is
// reported, not the unreadable directory.
func TestIndexAssemblyDefinitionsReportsABrokenAsmdefMetBeforeAnUnreadableDirectory(t *testing.T) {
	projectRoot := t.TempDir()
	broken := filepath.Join(projectRoot, "Assets", "A", "Broken.asmdef")
	writeFileAt(t, broken, "not json")
	lockDirectory(t, filepath.Join(projectRoot, "Assets", "Z_Locked"))

	_, err := IndexAssemblyDefinitions(walkProjectTree(projectRoot))

	wantPrefix := "failed to index assembly definitions under " + filepath.Join(projectRoot, "Assets") + ": failed to read " + broken
	if err == nil || !strings.HasPrefix(err.Error(), wantPrefix) {
		t.Fatalf("err = %v, want it to start with %q", err, wantPrefix)
	}
}

// Verifies a directory an earlier root cannot read is reported ahead of a broken .asmdef in a later
// root, which indexing never reached.
func TestIndexAssemblyDefinitionsReportsAnEarlierRootsWalkErrorFirst(t *testing.T) {
	projectRoot := t.TempDir()
	lockDirectory(t, filepath.Join(projectRoot, "Assets", "Locked"))
	writeFileAt(t, filepath.Join(projectRoot, "Packages", "Broken", "Broken.asmdef"), "not json")

	_, err := IndexAssemblyDefinitions(walkProjectTree(projectRoot))

	wantPrefix := "failed to index assembly definitions under " + filepath.Join(projectRoot, "Assets") + ": "
	if err == nil || !strings.HasPrefix(err.Error(), wantPrefix) || !strings.Contains(err.Error(), "Locked") {
		t.Fatalf("err = %v, want the unreadable Assets directory reported", err)
	}
}

// Verifies a directory that cannot be read, with nothing else wrong, fails the index under its root.
func TestIndexAssemblyDefinitionsReportsAnUnreadableDirectory(t *testing.T) {
	projectRoot := t.TempDir()
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "A", "A.asmdef"), `{"name":"A"}`)
	lockDirectory(t, filepath.Join(projectRoot, "Packages", "Locked"))

	_, err := IndexAssemblyDefinitions(walkProjectTree(projectRoot))

	wantPrefix := "failed to index assembly definitions under " + filepath.Join(projectRoot, "Packages") + ": "
	if err == nil || !strings.HasPrefix(err.Error(), wantPrefix) || !strings.Contains(err.Error(), "Locked") {
		t.Fatalf("err = %v, want the unreadable Packages directory reported", err)
	}
}

// Verifies .asmref files are listed in path order whatever root and walk order they were met in.
func TestIndexAssemblyReferencesSortsByPath(t *testing.T) {
	projectRoot := t.TempDir()
	writeFileAt(t, filepath.Join(projectRoot, "Packages", "P", "P.asmref"), `{"reference":"P"}`)
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "a.b", "B.asmref"), `{"reference":"B"}`)
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "a", "A.asmref"), `{"reference":"A"}`)

	references, err := IndexAssemblyReferences(walkProjectTree(projectRoot))
	if err != nil {
		t.Fatalf("IndexAssemblyReferences returned an error: %v", err)
	}

	got := []string{}
	for _, reference := range references {
		got = append(got, reference.Reference)
	}
	if strings.Join(got, ",") != "B,A,P" {
		t.Fatalf("references = %v, want B,A,P (path order)", got)
	}
}

// Verifies a directory that cannot be read fails the .asmref index under its root.
func TestIndexAssemblyReferencesReportsAnUnreadableDirectory(t *testing.T) {
	projectRoot := t.TempDir()
	lockDirectory(t, filepath.Join(projectRoot, "Assets", "Locked"))

	_, err := IndexAssemblyReferences(walkProjectTree(projectRoot))

	wantPrefix := "failed to index assembly references under " + filepath.Join(projectRoot, "Assets") + ": "
	if err == nil || !strings.HasPrefix(err.Error(), wantPrefix) {
		t.Fatalf("err = %v, want the unreadable Assets directory reported", err)
	}
}

// Verifies that of two broken .asmdef files in one root, the one the walk meets first is reported,
// which is not the one whose full path sorts first.
func TestIndexAssemblyDefinitionsReportsTheFirstBrokenAsmdefInWalkOrder(t *testing.T) {
	projectRoot := t.TempDir()
	first := filepath.Join(projectRoot, "Assets", "a", "X.asmdef")
	writeFileAt(t, first, "not json")
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "a.b", "Y.asmdef"), "not json")

	_, err := IndexAssemblyDefinitions(walkProjectTree(projectRoot))

	wantPrefix := "failed to index assembly definitions under " + filepath.Join(projectRoot, "Assets") + ": failed to read " + first
	if err == nil || !strings.HasPrefix(err.Error(), wantPrefix) {
		t.Fatalf("err = %v, want it to start with %q", err, wantPrefix)
	}
}

// Verifies the first broken .asmdef in walk order is reported even when a later file's read finishes
// first: the earlier read is held until the later one has returned its error.
func TestIndexAssemblyDefinitionsReportsTheFirstErrorWhateverFinishesFirst(t *testing.T) {
	projectRoot := t.TempDir()
	first := filepath.Join(projectRoot, "Assets", "a", "X.asmdef")
	later := filepath.Join(projectRoot, "Assets", "a.b", "Y.asmdef")
	writeFileAt(t, first, "")
	writeFileAt(t, later, "")
	laterReturned := make(chan struct{})
	errFirst := errors.New("first failed")

	_, err := indexAssemblyDefinitionsReading(walkProjectTree(projectRoot), func(path string) assemblyDefinitionRead {
		if path == first {
			<-laterReturned

			return assemblyDefinitionRead{err: errFirst}
		}
		defer close(laterReturned)

		return assemblyDefinitionRead{err: errors.New("later failed")}
	})

	if !errors.Is(err, errFirst) {
		t.Fatalf("err = %v, want the error of the file met first", err)
	}
}
