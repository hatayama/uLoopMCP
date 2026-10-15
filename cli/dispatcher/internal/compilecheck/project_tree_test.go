package compilecheck

import (
	"os"
	"path/filepath"
	"reflect"
	"runtime"
	"strings"
	"testing"
)

// lockDirectory makes a directory unreadable for the rest of the test, restoring it so the
// temporary directory can still be removed.
func lockDirectory(t *testing.T, path string) {
	t.Helper()
	if runtime.GOOS == "windows" {
		t.Skip("directory permissions do not make a directory unreadable on Windows")
	}
	makeDirAt(t, path)
	if err := os.Chmod(path, 0o000); err != nil {
		t.Fatalf("failed to lock %s: %v", path, err)
	}
	t.Cleanup(func() { _ = os.Chmod(path, 0o755) })
	if _, err := os.ReadDir(path); err == nil {
		t.Skip("this user can read a directory without permissions")
	}
}

// symlinkOrSkip creates a symbolic link, skipping the test where the platform does not allow one.
func symlinkOrSkip(t *testing.T, target string, link string) {
	t.Helper()
	if err := os.MkdirAll(filepath.Dir(link), 0o755); err != nil {
		t.Fatalf("failed to create directory for %s: %v", link, err)
	}
	if err := os.Symlink(target, link); err != nil {
		t.Skipf("symbolic links are not available here: %v", err)
	}
}

// Verifies the walk leaves out the names Unity ignores, both as directories and as files, and lists
// the .asmdef and .asmref files in the order the walk met them.
func TestWalkProjectTreeSkipsIgnoredNamesAndKeepsWalkOrder(t *testing.T) {
	projectRoot := t.TempDir()
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "a", "X.asmdef"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "a.b", "Y.asmdef"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "a", "R.asmref"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "Tmp~", "T.asmdef"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Assets", ".hidden", "H.asmdef"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "a", "._X.asmdef"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "a", "Old.asmref~"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Packages", "P", "P.asmdef"), "")

	tree := walkProjectTree(projectRoot)

	gotDefinitions := []string{}
	gotReferences := []string{}
	for _, root := range tree.roots {
		if root.walkErr != nil {
			t.Fatalf("walk of %s failed: %v", root.path, root.walkErr)
		}
		gotDefinitions = append(gotDefinitions, root.definitionFiles...)
		gotReferences = append(gotReferences, root.referenceFiles...)
	}
	wantDefinitions := []string{
		filepath.Join(projectRoot, "Assets", "a", "X.asmdef"),
		filepath.Join(projectRoot, "Assets", "a.b", "Y.asmdef"),
		filepath.Join(projectRoot, "Packages", "P", "P.asmdef"),
	}
	if !reflect.DeepEqual(gotDefinitions, wantDefinitions) {
		t.Fatalf("definition files = %v, want %v", gotDefinitions, wantDefinitions)
	}
	if want := []string{filepath.Join(projectRoot, "Assets", "a", "R.asmref")}; !reflect.DeepEqual(gotReferences, want) {
		t.Fatalf("reference files = %v, want %v", gotReferences, want)
	}
	for _, ignored := range []string{"Tmp~", ".hidden"} {
		if _, listed := tree.directory(filepath.Join(projectRoot, "Assets", ignored)); listed {
			t.Fatalf("ignored directory %s was listed", ignored)
		}
	}
	if tree.hasRegularFile(filepath.Join(projectRoot, "Assets", "a", "._X.asmdef")) {
		t.Fatal("an ignored file was listed")
	}
	if !tree.hasRegularFile(filepath.Join(projectRoot, "Assets", "a", "X.asmdef")) {
		t.Fatal("a regular file the walk met was not listed")
	}
}

// Verifies a symbolic link is listed as an entry that is not a regular file, and the walk does not
// descend through a link to a directory.
func TestWalkProjectTreeListsSymbolicLinksAsNonRegularEntries(t *testing.T) {
	projectRoot := t.TempDir()
	outside := t.TempDir()
	writeFileAt(t, filepath.Join(outside, "Real.cs"), "")
	symlinkOrSkip(t, outside, filepath.Join(projectRoot, "Assets", "Linked"))
	symlinkOrSkip(t, filepath.Join(outside, "Real.cs"), filepath.Join(projectRoot, "Assets", "Link.cs"))

	tree := walkProjectTree(projectRoot)

	if tree.hasRegularFile(filepath.Join(projectRoot, "Assets", "Link.cs")) {
		t.Fatal("a symbolic link to a file was listed as a regular file")
	}
	if _, listed := tree.directory(filepath.Join(projectRoot, "Assets", "Linked")); listed {
		t.Fatal("the walk descended through a symbolic link to a directory")
	}
	assets, listed := tree.directory(filepath.Join(projectRoot, "Assets"))
	if !listed {
		t.Fatal("Assets was not listed")
	}
	names := []string{}
	for _, entry := range assets.entries {
		names = append(names, entry.name)
	}
	if want := []string{"Link.cs", "Linked"}; !reflect.DeepEqual(names, want) {
		t.Fatalf("Assets entries = %v, want %v", names, want)
	}
}

// Verifies a walk error keeps what the failing root listed before it, records the error on that
// root, and leaves every later root unwalked.
func TestWalkProjectTreeStopsAtTheFirstRootThatFails(t *testing.T) {
	projectRoot := t.TempDir()
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "A", "A.asmdef"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Packages", "Before", "B.asmdef"), "")
	lockDirectory(t, filepath.Join(projectRoot, "Packages", "Locked"))
	writeFileAt(t, filepath.Join(projectRoot, "Library", "PackageCache", "pkg", "C.asmdef"), "")

	tree := walkProjectTree(projectRoot)

	if len(tree.roots) != 2 {
		t.Fatalf("roots = %+v, want Assets and Packages only", tree.roots)
	}
	packages := tree.roots[1]
	if packages.path != filepath.Join(projectRoot, "Packages") || packages.walkErr == nil {
		t.Fatalf("Packages root = %+v, want a walk error", packages)
	}
	if want := []string{filepath.Join(projectRoot, "Packages", "Before", "B.asmdef")}; !reflect.DeepEqual(packages.definitionFiles, want) {
		t.Fatalf("Packages definition files = %v, want %v", packages.definitionFiles, want)
	}
	if !strings.Contains(packages.walkErr.Error(), "Locked") {
		t.Fatalf("walk error = %v, want the locked directory named", packages.walkErr)
	}
	// Why the failing root's directories are not listed: its walk stopped part way, so a listing of
	// it could miss files, and a lookup must then fall back to the disk rather than trust it.
	if _, listed := tree.directory(filepath.Join(projectRoot, "Packages", "Before")); listed {
		t.Fatal("a directory of the failing root was listed")
	}
	if _, listed := tree.directory(filepath.Join(projectRoot, "Assets", "A")); !listed {
		t.Fatal("a directory of a root that walked cleanly was not listed")
	}
}
