package compilecheck

import (
	"errors"
	"os"
	"path/filepath"
	"reflect"
	"runtime"
	"strings"
	"testing"
)

// requireCaseInsensitiveFileSystem skips the test unless the temporary directory finds a file under
// a name that differs from it only in case.
func requireCaseInsensitiveFileSystem(t *testing.T) {
	t.Helper()
	probe := filepath.Join(t.TempDir(), "CaseProbe")
	writeFileAt(t, probe, "")
	if !fileExists(filepath.Join(filepath.Dir(probe), "caseprobe")) {
		t.Skip("the file system tells names apart by case")
	}
}

// rebuildLooseSources rebuilds the sources of an assembly without an .asmdef, through the listing
// one run takes of the project.
func rebuildLooseSources(t *testing.T, projectRoot string, sources []string) []string {
	t.Helper()
	got, err := RebuildSources(projectRoot, ResponseFile{Sources: sources}, nil, newOwnerIndexForProject(t, projectRoot))
	if err != nil {
		t.Fatalf("RebuildSources returned an error: %v", err)
	}

	return got
}

// Verifies the sources of a package-cache assembly keep the files that still exist and drop the
// ones deleted since the build, without a re-glob.
func TestRebuildSourcesKeepsExistingPackageCacheSources(t *testing.T) {
	projectRoot := t.TempDir()
	directory := filepath.Join(projectRoot, "Library", "PackageCache", "pkg")
	writeFileAt(t, filepath.Join(directory, "Pkg.asmdef"), `{"name":"Pkg"}`)
	writeFileAt(t, filepath.Join(directory, "Kept.cs"), "")
	writeFileAt(t, filepath.Join(directory, "Unrecorded.cs"), "")
	asmdef := AssemblyDefinition{Name: "Pkg", Path: filepath.Join(directory, "Pkg.asmdef"), Directory: directory}
	rsp := ResponseFile{Sources: []string{"Library/PackageCache/pkg/Kept.cs", "Library/PackageCache/pkg/Deleted.cs"}}

	sources, err := RebuildSources(projectRoot, rsp, &asmdef, newOwnerIndexForProject(t, projectRoot))
	if err != nil {
		t.Fatalf("RebuildSources returned an error: %v", err)
	}

	if want := []string{"Library/PackageCache/pkg/Kept.cs"}; !reflect.DeepEqual(sources, want) {
		t.Fatalf("sources = %v, want %v", sources, want)
	}
}

// Verifies a recorded source reached through a symbolic link to a directory still counts as there,
// although the walk does not descend through the link.
func TestRebuildSourcesKeepsASourceBehindALinkedDirectory(t *testing.T) {
	projectRoot := t.TempDir()
	outside := t.TempDir()
	writeFileAt(t, filepath.Join(outside, "A.cs"), "")
	symlinkOrSkip(t, outside, filepath.Join(projectRoot, "Assets", "Linked"))

	sources := rebuildLooseSources(t, projectRoot, []string{"Assets/Linked/A.cs"})

	if want := []string{"Assets/Linked/A.cs"}; !reflect.DeepEqual(sources, want) {
		t.Fatalf("sources = %v, want %v", sources, want)
	}
}

// Verifies a recorded source that is now a symbolic link to nothing counts as removed, although the
// walk lists a name there.
func TestRebuildSourcesDropsASourceThatIsADanglingLink(t *testing.T) {
	projectRoot := t.TempDir()
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "Kept.cs"), "")
	symlinkOrSkip(t, filepath.Join(projectRoot, "Missing.cs"), filepath.Join(projectRoot, "Assets", "Gone.cs"))

	sources := rebuildLooseSources(t, projectRoot, []string{"Assets/Gone.cs", "Assets/Kept.cs"})

	if want := []string{"Assets/Kept.cs"}; !reflect.DeepEqual(sources, want) {
		t.Fatalf("sources = %v, want %v", sources, want)
	}
}

// Verifies a recorded source whose name only changed case since the build still counts as there for
// an assembly without an .asmdef, as the file system finds it under the recorded spelling.
func TestRebuildSourcesKeepsASourceRenamedOnlyInCase(t *testing.T) {
	requireCaseInsensitiveFileSystem(t)
	projectRoot := t.TempDir()
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "Loose", "A.cs"), "")

	sources := rebuildLooseSources(t, projectRoot, []string{"Assets/Loose/a.cs"})

	if want := []string{"Assets/Loose/a.cs"}; !reflect.DeepEqual(sources, want) {
		t.Fatalf("sources = %v, want %v", sources, want)
	}
}

// Verifies a source renamed only in case inside an assembly with an .asmdef asks for a Unity build,
// because the re-glob finds a spelling the build never recorded.
func TestRebuildSourcesRefusesASourceRenamedOnlyInCaseUnderAnAsmdef(t *testing.T) {
	requireCaseInsensitiveFileSystem(t)
	projectRoot := t.TempDir()
	directory := filepath.Join(projectRoot, "Assets", "Foo")
	writeFileAt(t, filepath.Join(directory, "Foo.asmdef"), `{"name":"Foo"}`)
	writeFileAt(t, filepath.Join(directory, "A.cs"), "")
	asmdef := AssemblyDefinition{Name: "Foo", Path: filepath.Join(directory, "Foo.asmdef"), Directory: directory}

	_, err := RebuildSources(projectRoot, ResponseFile{Sources: []string{"Assets/Foo/a.cs"}}, &asmdef, newOwnerIndexForProject(t, projectRoot))

	var required UnityBuildRequiredError
	if !errors.As(err, &required) {
		t.Fatalf("err = %v, want a Unity build required", err)
	}
}

// Verifies a recorded source spelled in another Unicode normalization than the file on disk still
// counts as there, as macOS file systems find it.
func TestRebuildSourcesKeepsASourceInAnotherUnicodeNormalization(t *testing.T) {
	if runtime.GOOS != "darwin" {
		t.Skip("only macOS file systems ignore Unicode normalization")
	}
	projectRoot := t.TempDir()
	composed := "Café.cs"
	decomposed := "Café.cs"
	writeFileAt(t, filepath.Join(projectRoot, "Assets", composed), "")
	if !fileExists(filepath.Join(projectRoot, "Assets", decomposed)) {
		t.Skip("this file system tells Unicode normalizations apart")
	}

	sources := rebuildLooseSources(t, projectRoot, []string{"Assets/" + decomposed})

	if want := []string{"Assets/" + decomposed}; !reflect.DeepEqual(sources, want) {
		t.Fatalf("sources = %v, want %v", sources, want)
	}
}

// Verifies recorded sources outside every index root are checked on the disk: an existing one stays
// and a deleted one drops out.
func TestRebuildSourcesChecksSourcesOutsideTheIndexRoots(t *testing.T) {
	projectRoot := t.TempDir()
	outside := t.TempDir()
	kept := filepath.Join(outside, "Kept.cs")
	writeFileAt(t, kept, "")

	sources := rebuildLooseSources(t, projectRoot, []string{kept, filepath.Join(outside, "Deleted.cs")})

	if want := []string{kept}; !reflect.DeepEqual(sources, want) {
		t.Fatalf("sources = %v, want %v", sources, want)
	}
}

// Verifies files Unity ignores by name are left out of the glob, alongside ignored directories.
func TestRebuildSourcesLeavesOutIgnoredFiles(t *testing.T) {
	projectRoot := newAssemblyProject(t)
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "Foo", "._A.cs"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "Foo", ".Hidden.cs"), "")
	asmdef := AssemblyDefinition{
		Name: "Foo", Path: filepath.Join(projectRoot, "Assets", "Foo", "Foo.asmdef"),
		Directory: filepath.Join(projectRoot, "Assets", "Foo"),
	}

	sources, err := RebuildSources(projectRoot, ResponseFile{Sources: []string{"Assets/Foo/A.cs"}}, &asmdef, newOwnerIndexForProject(t, projectRoot))
	if err != nil {
		t.Fatalf("RebuildSources returned an error: %v", err)
	}

	if want := []string{"Assets/Foo/A.cs", "Assets/Foo/Sub/B.cs"}; !reflect.DeepEqual(sources, want) {
		t.Fatalf("sources = %v, want %v", sources, want)
	}
}

// Verifies the rebuild answers from the listing the run took rather than walking again: a source
// created after the listing is not globbed, a recorded one deleted after it is kept, and an .asmdef
// created after it does not stop the glob.
func TestRebuildSourcesUsesTheListingTakenAtTheStartOfTheRun(t *testing.T) {
	projectRoot := newAssemblyProject(t)
	owners := newOwnerIndexForProject(t, projectRoot)
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "Foo", "Late.cs"), "")
	if err := os.Remove(filepath.Join(projectRoot, "Assets", "Foo", "A.cs")); err != nil {
		t.Fatalf("failed to delete A.cs: %v", err)
	}
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "Foo", "Sub", "Late.asmdef"), `{"name":"Late"}`)
	asmdef := AssemblyDefinition{
		Name: "Foo", Path: filepath.Join(projectRoot, "Assets", "Foo", "Foo.asmdef"),
		Directory: filepath.Join(projectRoot, "Assets", "Foo"),
	}

	sources, err := RebuildSources(projectRoot, ResponseFile{Sources: []string{"Assets/Foo/A.cs"}}, &asmdef, owners)
	if err != nil {
		t.Fatalf("RebuildSources returned an error: %v", err)
	}

	if want := []string{"Assets/Foo/A.cs", "Assets/Foo/Sub/B.cs"}; !reflect.DeepEqual(sources, want) {
		t.Fatalf("sources = %v, want %v", sources, want)
	}
}

// Verifies a directory the listing does not hold is globbed from the disk, so a directory that does
// not exist is still reported with its path.
func TestGlobAssemblySourcesFallsBackToTheDiskOutsideTheListing(t *testing.T) {
	projectRoot := newAssemblyProject(t)
	owners := newOwnerIndexForProject(t, projectRoot)
	outside := t.TempDir()
	writeFileAt(t, filepath.Join(outside, "Out.cs"), "")

	sources, err := globAssemblySources(projectRoot, outside, owners)
	if err != nil || len(sources) != 1 || !strings.HasSuffix(sources[0], "Out.cs") {
		t.Fatalf("sources = %v, err = %v, want Out.cs from the disk", sources, err)
	}

	missing := filepath.Join(outside, "Gone")
	if _, missingErr := globAssemblySources(projectRoot, missing, owners); missingErr == nil ||
		!strings.HasPrefix(missingErr.Error(), "failed to list sources under "+missing) {
		t.Fatalf("err = %v, want the missing directory reported", missingErr)
	}
}
