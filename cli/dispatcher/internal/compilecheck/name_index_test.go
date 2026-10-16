package compilecheck

import (
	"os"
	"path/filepath"
	"strings"
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

// Verifies identifiers are cut by the characters C# allows in them: a spacing mark, a combining mark
// and a connector stay inside one identifier, a letter number starts one, and a formatting character
// is left out of the name, as the compiler leaves it out.
func TestScanIdentifiersCutsIdentifiersByTheCharactersCSharpAllows(t *testing.T) {
	set := identifiersOf("नाम cafe\u0301 a\u203fb \u216bth Fo\u200do")

	assertHasIdentifiers(t, set,
		[]string{"नाम", "cafe\u0301", "a\u203fb", "\u216bth", "Foo"},
		[]string{"न", "म", "cafe", "a", "b", "th", "Fo", "o"})
}

// Verifies a target-typed new is found with any white space before its parenthesis, and only then.
func TestScanIdentifiersFindsATargetTypedNewOnlyBeforeAParenthesis(t *testing.T) {
	cases := map[string]bool{
		"new(1)": true, "new   (1)": true, "new\n(1)": true, "new x(1)": false, "x = new": false, "new  ": false,
	}
	for content, want := range cases {
		if _, found := identifiersOf(content)[targetTypedNewToken]; found != want {
			t.Errorf("%q: target-typed new found = %v, want %v", content, found, want)
		}
	}
}

// BenchmarkScanIdentifiers reads a file holding many "new" expressions.
func BenchmarkScanIdentifiers(b *testing.B) {
	content := []byte(strings.Repeat("var x = new Thing(); ", 40_000))
	for b.Loop() {
		scanIdentifiers(content, identifierSet{})
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

// Verifies a unit's identifiers come from every source, and its generator output's from every C# file
// under the directory, nested directories included, with reading the latter reported.
func TestReadIdentifiersReadsSourcesAndGeneratedCode(t *testing.T) {
	root := t.TempDir()
	unit := writeUnitSources(t, root, map[string]string{"Assets/A.cs": "class First { }", "Assets/B.cs": "class Second { }"})
	writeUnitSources(t, root, map[string]string{"Generated/sub/a.CS": "class Third { }", "Generated/notes.txt": "Fourth"})

	set, withGenerated, err := readGeneratedIdentifiers(filepath.Join(root, "Generated"))
	if err != nil {
		t.Fatalf("expected the generated identifiers to be read, got %v", err)
	}
	if err := readSourceIdentifiers(root, unit, set); err != nil {
		t.Fatalf("expected the source identifiers to be read, got %v", err)
	}

	assertHasIdentifiers(t, set, []string{"First", "Second", "Third"}, []string{"Fourth"})
	if !withGenerated {
		t.Error("expected the generated code to be reported as read")
	}
}

// Verifies a missing generator output directory reads as no generated code rather than an error.
func TestReadGeneratedIdentifiersWithoutGeneratedCode(t *testing.T) {
	set, withGenerated, err := readGeneratedIdentifiers(filepath.Join(t.TempDir(), "Missing"))
	if err != nil {
		t.Fatalf("expected no error, got %v", err)
	}
	if withGenerated || len(set) != 0 {
		t.Errorf("expected no generated code, got %v (reported read: %v)", set, withGenerated)
	}
}

// Verifies a source given by absolute path, as a local package's is, is read from that path.
func TestReadSourceIdentifiersReadsAnAbsoluteSource(t *testing.T) {
	elsewhere := t.TempDir()
	writeUnitSources(t, elsewhere, map[string]string{"Package/P.cs": "class Packaged { }"})
	unit := CompileUnit{
		Assembly: ResponseFile{AssemblyName: "Unit"},
		Sources:  []string{filepath.ToSlash(filepath.Join(elsewhere, "Package", "P.cs"))},
	}
	set := identifierSet{}

	if err := readSourceIdentifiers(t.TempDir(), unit, set); err != nil {
		t.Fatalf("expected the identifiers to be read, got %v", err)
	}

	assertHasIdentifiers(t, set, []string{"Packaged"}, nil)
}

// Verifies a unit without sources, or with one that cannot be read, is refused rather than read as
// an empty set, so it is never read as writing no changed name.
func TestReadSourceIdentifiersRefusesAUnitItCannotRead(t *testing.T) {
	root := t.TempDir()
	missing := CompileUnit{Assembly: ResponseFile{AssemblyName: "Unit"}, Sources: []string{"Assets/Missing.cs"}}
	for label, unit := range map[string]CompileUnit{"no sources": {Assembly: ResponseFile{AssemblyName: "Unit"}}, "missing": missing} {
		if err := readSourceIdentifiers(root, unit, identifierSet{}); err == nil {
			t.Errorf("%s: expected an error", label)
		}
	}
}

// Verifies the background read of the sources is over once wait returns after it was asked for, and
// that lookup answers only for the units it was asked to read.
func TestNameIndexIsOverOnceWaitReturns(t *testing.T) {
	root := t.TempDir()
	unit := writeUnitSources(t, root, map[string]string{"Assets/A.cs": "class First { }"})
	index := nameIndex{}
	index.start(root, []CompileUnit{unit, unit}, []string{filepath.Join(root, "Missing"), ""}, []bool{true, false})
	index.readSources()

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
