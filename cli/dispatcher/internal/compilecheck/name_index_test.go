package compilecheck

import (
	"os"
	"path/filepath"
	"testing"
)

func identifiersOf(content string) identifierSet {
	set := identifierSet{}
	scanIdentifiers([]byte(content), set)

	return set
}

func assertHasIdentifiers(t *testing.T, set identifierSet, present []string, absent []string) {
	t.Helper()
	for _, name := range present {
		if _, found := set[name]; !found {
			t.Errorf("expected %q among the identifiers, got %v", name, set)
		}
	}
	for _, name := range absent {
		if _, found := set[name]; found {
			t.Errorf("expected %q not to be among the identifiers, got %v", name, set)
		}
	}
}

// Verifies identifiers are cut the way C# spells them: a verbatim '@' left off, Unicode letters
// kept, words in strings and comments read too, a target-typed new found with or without a space,
// and an identifier that only starts with "new" not taken for one.
func TestScanIdentifiersCutsIdentifiersTheWayCSharpSpellsThem(t *testing.T) {
	set := identifiersOf("var @class = new (1); Point p = new(2); newFoo(3); // Commented\n" +
		"string s = \"Quoted\"; int 名前 = _under9; char c = '\\u0041';")

	assertHasIdentifiers(t, set,
		[]string{"class", "new(", "newFoo", "Commented", "Quoted", "名前", "_under9", "Point", "u0041"},
		[]string{"@class", "A", "9"})
}

// Verifies "newFoo(" alone is not read as a target-typed new.
func TestScanIdentifiersDoesNotTakeAnIdentifierStartingWithNewForANew(t *testing.T) {
	assertHasIdentifiers(t, identifiersOf("newFoo(1); renew (2);"), []string{"newFoo", "renew"}, []string{"new("})
}

// writeUnitSources writes the given project-relative files and returns a unit compiling them.
func writeUnitSources(t *testing.T, root string, files map[string]string) CompileUnit {
	t.Helper()
	unit := CompileUnit{Assembly: ResponseFile{AssemblyName: "Unit"}}
	for path, content := range files {
		absolute := filepath.Join(root, filepath.FromSlash(path))
		if err := os.MkdirAll(filepath.Dir(absolute), 0o755); err != nil {
			t.Fatalf("failed to create %s: %v", path, err)
		}
		if err := os.WriteFile(absolute, []byte(content), 0o600); err != nil {
			t.Fatalf("failed to write %s: %v", path, err)
		}
		unit.Sources = append(unit.Sources, path)
	}

	return unit
}

// Verifies a unit's identifiers come from every source and from every C# file under its saved
// generator output, nested directories included, and that reading the latter is reported.
func TestBuildUnitIdentifiersReadsSourcesAndGeneratedCode(t *testing.T) {
	root := t.TempDir()
	unit := writeUnitSources(t, root, map[string]string{"Assets/A.cs": "class First { }", "Assets/B.cs": "class Second { }"})
	generated := filepath.Join(root, "Generated")
	writeUnitSources(t, root, map[string]string{"Generated/sub/a.CS": "class Third { }", "Generated/notes.txt": "Fourth"})

	set, withGenerated, err := buildUnitIdentifiers(root, unit, generated)
	if err != nil {
		t.Fatalf("expected the identifiers to be read, got %v", err)
	}

	assertHasIdentifiers(t, set, []string{"First", "Second", "Third"}, []string{"Fourth"})
	if !withGenerated {
		t.Error("expected the generated code to be reported as read")
	}
}

// Verifies a unit without saved generator output is read from its sources alone and says so.
func TestBuildUnitIdentifiersWithoutGeneratedCode(t *testing.T) {
	root := t.TempDir()
	unit := writeUnitSources(t, root, map[string]string{"Assets/A.cs": "class First { }"})

	set, withGenerated, err := buildUnitIdentifiers(root, unit, filepath.Join(root, "Missing"))
	if err != nil {
		t.Fatalf("expected the identifiers to be read, got %v", err)
	}

	assertHasIdentifiers(t, set, []string{"First"}, nil)
	if withGenerated {
		t.Error("expected no generated code to be reported")
	}
}

// Verifies a source given by absolute path, as a local package's is, is read from that path.
func TestBuildUnitIdentifiersReadsAnAbsoluteSource(t *testing.T) {
	root := t.TempDir()
	elsewhere := t.TempDir()
	writeUnitSources(t, elsewhere, map[string]string{"Package/P.cs": "class Packaged { }"})
	unit := CompileUnit{
		Assembly: ResponseFile{AssemblyName: "Unit"},
		Sources:  []string{filepath.ToSlash(filepath.Join(elsewhere, "Package", "P.cs"))},
	}

	set, _, err := buildUnitIdentifiers(root, unit, filepath.Join(root, "Missing"))
	if err != nil {
		t.Fatalf("expected the identifiers to be read, got %v", err)
	}

	assertHasIdentifiers(t, set, []string{"Packaged"}, nil)
}

// Verifies a unit without sources, or with one that cannot be read, has no identifiers rather than
// an empty set, so it is never read as writing no changed name.
func TestBuildUnitIdentifiersRefusesAUnitItCannotRead(t *testing.T) {
	root := t.TempDir()
	missing := CompileUnit{Assembly: ResponseFile{AssemblyName: "Unit"}, Sources: []string{"Assets/Missing.cs"}}
	for label, unit := range map[string]CompileUnit{"no sources": {Assembly: ResponseFile{AssemblyName: "Unit"}}, "missing": missing} {
		if set, _, err := buildUnitIdentifiers(root, unit, filepath.Join(root, "Missing")); err == nil {
			t.Errorf("%s: expected an error, got %v", label, set)
		}
	}
}

// Verifies the background read is over once wait returns, and that lookup answers only for the
// units it was asked to read.
func TestNameIndexIsOverOnceWaitReturns(t *testing.T) {
	root := t.TempDir()
	unit := writeUnitSources(t, root, map[string]string{"Assets/A.cs": "class First { }"})
	index := nameIndex{}
	index.start(root, []CompileUnit{unit, unit}, []string{filepath.Join(root, "Missing"), ""}, []bool{true, false})

	index.wait()

	select {
	case <-index.done:
	default:
		t.Fatal("expected the read to be over once wait returned")
	}
	if _, err := index.lookup(0); err != nil {
		t.Errorf("expected the identifiers of the read unit, got %v", err)
	}
	if _, err := index.lookup(1); err == nil {
		t.Error("expected no identifiers for a unit that was not read")
	}
	if _, err := (&nameIndex{}).lookup(0); err == nil {
		t.Error("expected no identifiers from an index that never started")
	}
}
