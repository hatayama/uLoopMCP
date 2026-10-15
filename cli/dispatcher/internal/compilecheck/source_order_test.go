package compilecheck

import (
	"context"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"testing"
	"time"
)

// fooAssemblyDefinition is the Foo assembly newAssemblyProject lays out.
func fooAssemblyDefinition(projectRoot string) AssemblyDefinition {
	return AssemblyDefinition{
		Name:      "Foo",
		Path:      filepath.Join(projectRoot, "Assets", "Foo", "Foo.asmdef"),
		Directory: filepath.Join(projectRoot, "Assets", "Foo"),
	}
}

// rebuildFooSources rebuilds Foo's sources from rsp, failing the test when the rebuild fails.
func rebuildFooSources(t *testing.T, projectRoot string, rsp ResponseFile) []string {
	t.Helper()
	asmdef := fooAssemblyDefinition(projectRoot)
	sources, err := RebuildSources(projectRoot, rsp, &asmdef, newOwnerIndexForProject(t, projectRoot))
	if err != nil {
		t.Fatalf("expected the rebuild to succeed, got error: %v", err)
	}

	return sources
}

// Verifies the recorded sources keep the order of the response file, which is neither ordinal nor
// case-insensitive here, and a source deleted from the middle of it drops out without moving the rest.
func TestRebuildSourcesKeepsTheResponseFileOrder(t *testing.T) {
	projectRoot := newAssemblyProject(t)
	rsp := ResponseFile{Sources: []string{
		"Assets/Foo/Sub/B.cs",
		"Assets/Foo/Deleted.cs",
		"Assets/Foo/A.cs",
	}}

	sources := rebuildFooSources(t, projectRoot, rsp)

	assertStrings(t, "sources", sources, []string{
		"Assets/Foo/Sub/B.cs",
		"Assets/Foo/A.cs",
	})
}

// Verifies sources added since the last build come after the recorded ones in ordinal order, whatever
// order the walk found them in: N/M.cs is walked before N.cs, and C.cs sorts before b.cs only when
// case counts.
func TestRebuildSourcesAppendsAddedSourcesInOrdinalOrderAfterTheRecordedOnes(t *testing.T) {
	projectRoot := newAssemblyProject(t)
	for _, added := range []string{"N.cs", filepath.Join("N", "M.cs"), "C.cs", "b.cs"} {
		writeFileAt(t, filepath.Join(projectRoot, "Assets", "Foo", added), "")
	}
	rsp := ResponseFile{Sources: []string{
		"Assets/Foo/Sub/B.cs",
		"Assets/Foo/A.cs",
	}}

	sources := rebuildFooSources(t, projectRoot, rsp)

	assertStrings(t, "sources", sources, []string{
		"Assets/Foo/Sub/B.cs",
		"Assets/Foo/A.cs",
		"Assets/Foo/C.cs",
		"Assets/Foo/N.cs",
		"Assets/Foo/N/M.cs",
		"Assets/Foo/b.cs",
	})
}

// Verifies a source of a folder an .asmref attaches keeps its place at the head of the response
// file, although the walk reaches that folder after the assembly's own and its name sorts after it.
func TestRebuildSourcesKeepsAnAttachedFolderSourceAtItsResponseFilePosition(t *testing.T) {
	projectRoot := newAssemblyProject(t)
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "Shared", "E.cs"), "")
	writeFileAt(t,
		filepath.Join(projectRoot, "Assets", "Shared", "Foo.Ref.asmref"), `{"reference":"Foo"}`)
	rsp := ResponseFile{Sources: []string{
		"Assets/Shared/E.cs",
		"Assets/Foo/A.cs",
	}}

	sources := rebuildFooSources(t, projectRoot, rsp)

	assertStrings(t, "sources", sources, []string{
		"Assets/Shared/E.cs",
		"Assets/Foo/A.cs",
		"Assets/Foo/Sub/B.cs",
	})
}

// Verifies a source only the rescue of recorded sources keeps - it sits under a stale duplicate
// .asmdef that stops the walk, inside a folder an .asmref attaches - keeps its place at the head of
// the response file. The same rebuild without it in the response file shows the walk never finds it.
func TestRebuildSourcesKeepsARescuedSourceAtItsResponseFilePosition(t *testing.T) {
	projectRoot := newAssemblyProject(t)
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "Bar", "Bar.asmdef"), `{"name":"Bar"}`)
	writeFileAt(t,
		filepath.Join(projectRoot, "Assets", "Shared", "Foo.Ref.asmref"), `{"reference":"Foo"}`)
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "Shared", "Dup", "Bar.asmdef"), `{"name":"Bar"}`)
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "Shared", "Dup", "R.cs"), "")

	walkedOnly := rebuildFooSources(t, projectRoot, ResponseFile{Sources: []string{"Assets/Foo/A.cs"}})
	assertStrings(t, "sources the walk finds", walkedOnly, []string{
		"Assets/Foo/A.cs",
		"Assets/Foo/Sub/B.cs",
	})

	sources := rebuildFooSources(t, projectRoot, ResponseFile{Sources: []string{
		"Assets/Shared/Dup/R.cs",
		"Assets/Foo/A.cs",
	}})

	assertStrings(t, "sources", sources, []string{
		"Assets/Shared/Dup/R.cs",
		"Assets/Foo/A.cs",
		"Assets/Foo/Sub/B.cs",
	})
}

// Verifies a package-cache assembly keeps the order of its response file.
func TestRebuildSourcesKeepsTheResponseFileOrderOfAPackageCacheAssembly(t *testing.T) {
	projectRoot := t.TempDir()
	packageDirectory := filepath.Join(projectRoot, "Library", "PackageCache", "com.example.pkg")
	writeFileAt(t, filepath.Join(packageDirectory, "Pkg.asmdef"), `{"name":"Pkg"}`)
	writeFileAt(t, filepath.Join(packageDirectory, "A.cs"), "")
	writeFileAt(t, filepath.Join(packageDirectory, "Sub", "B.cs"), "")
	rsp := ResponseFile{Sources: []string{
		"Library/PackageCache/com.example.pkg/Sub/B.cs",
		"Library/PackageCache/com.example.pkg/Deleted.cs",
		"Library/PackageCache/com.example.pkg/A.cs",
	}}
	asmdef := AssemblyDefinition{
		Name:      "Pkg",
		Path:      filepath.Join(packageDirectory, "Pkg.asmdef"),
		Directory: packageDirectory,
	}

	sources, err := RebuildSources(projectRoot, rsp, &asmdef, newOwnerIndexForProject(t, projectRoot))
	if err != nil {
		t.Fatalf("expected the rebuild to succeed, got error: %v", err)
	}

	assertStrings(t, "sources", sources, []string{
		"Library/PackageCache/com.example.pkg/Sub/B.cs",
		"Library/PackageCache/com.example.pkg/A.cs",
	})
}

// Verifies an assembly with no .asmdef keeps the order of its response file.
func TestRebuildSourcesKeepsTheResponseFileOrderWithoutAnAssemblyDefinition(t *testing.T) {
	projectRoot := newAssemblyProject(t)
	rsp := ResponseFile{Sources: []string{
		"Assets/Foo/Sub/B.cs",
		"Assets/Foo/Deleted.cs",
		"Assets/Foo/A.cs",
	}}

	sources, err := RebuildSources(projectRoot, rsp, nil, newOwnerIndexForProject(t, projectRoot))
	if err != nil {
		t.Fatalf("expected the rebuild to succeed, got error: %v", err)
	}

	assertStrings(t, "sources", sources, []string{
		"Assets/Foo/Sub/B.cs",
		"Assets/Foo/A.cs",
	})
}

// Verifies the response file a compile writes lists the sources in the order of Unity's response
// file, from the plan through to the file csc reads, so nothing on the way sorts them again.
func TestCompileWritesSourcesInTheResponseFileOrder(t *testing.T) {
	projectRoot := t.TempDir()
	writePlanAssembly(t, projectRoot, "A", nil)
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "A", "Sub", "Z.cs"), "")
	writeFileAt(t, filepath.Join(projectRoot, planDagDirectory, "A.rsp"), joinLines([]string{
		`-out:"` + planDagDirectory + `/A.dll"`,
		`-refout:"` + planDagDirectory + `/A.ref.dll"`,
		`/additionalfile:"` + planDagDirectory + `/A.UnityAdditionalFile.txt"`,
		`"Assets/A/Sub/Z.cs"`,
		`"Assets/A/A.cs"`,
	}))
	settlePlanProject(t, projectRoot, "A")
	plan, err := BuildCompilePlan(projectRoot, planDagDirectory, true)
	if err != nil {
		t.Fatalf("expected the plan to build, got error: %v", err)
	}
	makeOutputDirectory(t, projectRoot, plan)
	captured := exec.Cmd{}
	compiler := Compiler{
		Paths:       EditorCompilerPaths{DotnetHostPath: "/dotnet", CompilerDllPath: "/csc.dll"},
		ProjectRoot: projectRoot,
		Timeout:     time.Minute,
		Run:         stubRunner("", 0, &captured),
	}

	if _, err := compiler.CompileUnit(context.Background(), plan, plan.Units[0]); err != nil {
		t.Fatalf("expected the unit to compile, got error: %v", err)
	}

	written, err := os.ReadFile(filepath.Join(projectRoot, plan.OutputDir, "A.rsp"))
	if err != nil {
		t.Fatalf("failed to read the written response file: %v", err)
	}
	sourceLines := []string{}
	for _, line := range strings.Split(string(written), "\n") {
		if strings.HasPrefix(line, `"Assets/`) {
			sourceLines = append(sourceLines, line)
		}
	}
	assertStrings(t, "source lines", sourceLines, []string{`"Assets/A/Sub/Z.cs"`, `"Assets/A/A.cs"`})
}

// Verifies a source the response file lists twice is compiled once, at its first position, in the
// re-globbed branch and in the one without an .asmdef alike.
func TestRebuildSourcesKeepsADuplicateResponseFileSourceOnce(t *testing.T) {
	projectRoot := newAssemblyProject(t)
	rsp := ResponseFile{Sources: []string{
		"Assets/Foo/Sub/B.cs",
		"Assets/Foo/A.cs",
		"Assets/Foo/Sub/B.cs",
	}}
	want := []string{"Assets/Foo/Sub/B.cs", "Assets/Foo/A.cs"}

	assertStrings(t, "re-globbed sources", rebuildFooSources(t, projectRoot, rsp), want)

	sources, err := RebuildSources(projectRoot, rsp, nil, newOwnerIndexForProject(t, projectRoot))
	if err != nil {
		t.Fatalf("expected the rebuild to succeed, got error: %v", err)
	}
	assertStrings(t, "sources without an .asmdef", sources, want)
}
