package compilecheck

import (
	"path/filepath"
	"reflect"
	"testing"
)

// comparableProjectTree is a projectTree with its errors turned into strings and every empty list
// spelled the same way, so two trees that differ only in nil against empty slices compare equal.
type comparableProjectTree struct {
	roots       []comparableProjectTreeRoot
	directories map[string]comparableProjectTreeDirectory
}

// comparableProjectTreeRoot is one root of a comparableProjectTree.
type comparableProjectTreeRoot struct {
	path            string
	definitionFiles []string
	referenceFiles  []string
	walkErr         string
}

// comparableProjectTreeDirectory is one directory of a comparableProjectTree.
type comparableProjectTreeDirectory struct {
	entries []projectTreeEntry
	byName  map[string]projectTreeEntry
}

// toComparableProjectTree normalizes a tree for comparison.
func toComparableProjectTree(tree projectTree) comparableProjectTree {
	comparable := comparableProjectTree{
		roots:       []comparableProjectTreeRoot{},
		directories: map[string]comparableProjectTreeDirectory{},
	}
	for _, root := range tree.roots {
		walkErr := ""
		if root.walkErr != nil {
			walkErr = root.walkErr.Error()
		}
		comparable.roots = append(comparable.roots, comparableProjectTreeRoot{
			path:            root.path,
			definitionFiles: append([]string{}, root.definitionFiles...),
			referenceFiles:  append([]string{}, root.referenceFiles...),
			walkErr:         walkErr,
		})
	}
	for path, directory := range tree.directories {
		byName := map[string]projectTreeEntry{}
		for name, entry := range directory.byName {
			byName[name] = entry
		}
		comparable.directories[path] = comparableProjectTreeDirectory{
			entries: append([]projectTreeEntry{}, directory.entries...),
			byName:  byName,
		}
	}

	return comparable
}

// assertWalkMatchesOracle walks a project with the production walk and with the WalkDir oracle and
// fails when the two trees differ in anything a caller can observe.
func assertWalkMatchesOracle(t *testing.T, projectRoot string) {
	t.Helper()
	want := toComparableProjectTree(oracleWalkProjectTree(projectRoot))
	got := toComparableProjectTree(walkProjectTree(projectRoot))
	if !reflect.DeepEqual(got, want) {
		t.Fatalf("walk differs from the WalkDir oracle\n got: %+v\nwant: %+v", got, want)
	}
}

// Verifies the walk lists the same tree as filepath.WalkDir over every kind of project layout it
// has to agree on: order, ignored names, links, roots that are missing, linked or unreadable, the
// first error of a root, and roots that overlap.
func TestWalkProjectTreeMatchesTheWalkDirOracle(t *testing.T) {
	cases := []struct {
		name  string
		setUp func(t *testing.T, projectRoot string)
	}{
		{"nested directories and files in walk order", setUpNestedProjectTree},
		{"ignored directories and files", setUpIgnoredNamesProjectTree},
		{"symbolic links inside a root", setUpSymbolicLinksProjectTree},
		{"a directory named like an asmdef", setUpAsmdefNamedDirectoryProjectTree},
		{"a root that is a symbolic link", setUpLinkedRootProjectTree},
		{"a missing root", setUpMissingRootProjectTree},
		{"an unreadable directory in a later root", setUpUnreadableChildProjectTree},
		{"an unreadable root", setUpUnreadableRootProjectTree},
		{"siblings after an unreadable directory", setUpSiblingsAfterUnreadableProjectTree},
		{"another subtree after an unreadable directory", setUpSubtreeAfterUnreadableProjectTree},
		{"definitions found before an unreadable directory", setUpDefinitionsBeforeUnreadableProjectTree},
		{"a local package inside Packages", setUpOverlappingRootsProjectTree},
	}
	for _, testCase := range cases {
		t.Run(testCase.name, func(t *testing.T) {
			projectRoot := t.TempDir()
			testCase.setUp(t, projectRoot)
			assertWalkMatchesOracle(t, projectRoot)
		})
	}
}

// setUpNestedProjectTree lays out nested directories whose walk order differs from path order.
func setUpNestedProjectTree(t *testing.T, projectRoot string) {
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "a", "X.asmdef"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "a", "b", "c", "C.cs"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "a.b", "Y.asmdef"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "Z", "A", "X.asmdef"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "Z", "A.asmdef"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "R.asmref"), "")
	makeDirAt(t, filepath.Join(projectRoot, "Assets", "Empty"))
	writeFileAt(t, filepath.Join(projectRoot, "Packages", "P", "P.asmdef"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Packages", "P", "Q.asmref"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Library", "PackageCache", "pkg", "Q.asmdef"), "")
}

// setUpIgnoredNamesProjectTree lays out directories and files Unity ignores, next to ones it keeps.
func setUpIgnoredNamesProjectTree(t *testing.T, projectRoot string) {
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "Tmp~", "T.asmdef"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "Tmp~", "Deep", "D.asmdef"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Assets", ".hidden", "H.asmdef"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "a", "._X.asmdef"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "a", "Old.asmref~"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "a", "Kept.asmdef"), "")
}

// setUpSymbolicLinksProjectTree lays out a link to a directory, a dangling link and a link named
// like an .asmdef.
func setUpSymbolicLinksProjectTree(t *testing.T, projectRoot string) {
	outside := t.TempDir()
	writeFileAt(t, filepath.Join(outside, "Inner", "Inner.asmdef"), "")
	writeFileAt(t, filepath.Join(outside, "Real.asmdef"), "")
	symlinkOrSkip(t, filepath.Join(outside, "Inner"), filepath.Join(projectRoot, "Assets", "Linked"))
	symlinkOrSkip(t, filepath.Join(outside, "Missing.cs"), filepath.Join(projectRoot, "Assets", "Dangling.cs"))
	symlinkOrSkip(t, filepath.Join(outside, "Real.asmdef"), filepath.Join(projectRoot, "Assets", "Link.asmdef"))
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "Plain", "Plain.asmdef"), "")
}

// setUpAsmdefNamedDirectoryProjectTree lays out a directory whose name ends in .asmdef.
func setUpAsmdefNamedDirectoryProjectTree(t *testing.T, projectRoot string) {
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "Folder.asmdef", "Inside.cs"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "Folder.asmdef", "Nested", "Nested.asmdef"), "")
}

// setUpLinkedRootProjectTree makes Assets a symbolic link to a directory holding an .asmdef.
func setUpLinkedRootProjectTree(t *testing.T, projectRoot string) {
	outside := t.TempDir()
	writeFileAt(t, filepath.Join(outside, "A", "A.asmdef"), "")
	symlinkOrSkip(t, outside, filepath.Join(projectRoot, "Assets"))
	writeFileAt(t, filepath.Join(projectRoot, "Packages", "P", "P.asmdef"), "")
}

// setUpMissingRootProjectTree leaves out Packages and the package cache.
func setUpMissingRootProjectTree(t *testing.T, projectRoot string) {
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "A", "A.asmdef"), "")
}

// setUpUnreadableChildProjectTree locks a directory of Packages after a clean Assets, with a package
// cache behind it that must not be walked.
func setUpUnreadableChildProjectTree(t *testing.T, projectRoot string) {
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "A", "A.asmdef"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Packages", "Before", "B.asmdef"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Library", "PackageCache", "pkg", "C.asmdef"), "")
	lockDirectory(t, filepath.Join(projectRoot, "Packages", "Locked"))
}

// setUpUnreadableRootProjectTree locks Assets itself, with Packages behind it.
func setUpUnreadableRootProjectTree(t *testing.T, projectRoot string) {
	writeFileAt(t, filepath.Join(projectRoot, "Packages", "P", "P.asmdef"), "")
	lockDirectory(t, filepath.Join(projectRoot, "Assets"))
}

// setUpSiblingsAfterUnreadableProjectTree puts an .asmdef and an .asmref in a sibling the walk
// meets after the unreadable directory.
func setUpSiblingsAfterUnreadableProjectTree(t *testing.T, projectRoot string) {
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "M", "M.asmdef"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "M", "M.asmref"), "")
	lockDirectory(t, filepath.Join(projectRoot, "Assets", "Locked"))
}

// setUpSubtreeAfterUnreadableProjectTree puts an .asmdef in another top-level subtree the walk
// meets after an unreadable directory nested in an earlier one.
func setUpSubtreeAfterUnreadableProjectTree(t *testing.T, projectRoot string) {
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "A", "A.asmdef"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "B", "B.asmdef"), "")
	lockDirectory(t, filepath.Join(projectRoot, "Assets", "A", "Locked"))
}

// setUpDefinitionsBeforeUnreadableProjectTree puts several .asmdef files ahead of an unreadable
// directory in the same root.
func setUpDefinitionsBeforeUnreadableProjectTree(t *testing.T, projectRoot string) {
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "A", "A1.asmdef"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "A", "A2.asmdef"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "B", "B.asmref"), "")
	lockDirectory(t, filepath.Join(projectRoot, "Assets", "Z_Locked"))
}

// setUpOverlappingRootsProjectTree adds a local package that lives inside Packages, so two roots
// list the same directories.
// Why only one `file:` dependency: the manifest's dependencies are a map, and with two of them the
// order of the local roots would change between the oracle's walk and the production one.
func setUpOverlappingRootsProjectTree(t *testing.T, projectRoot string) {
	writeFileAt(t, filepath.Join(projectRoot, "Packages", "manifest.json"),
		`{"dependencies":{"com.example.local":"file:Local"}}`)
	writeFileAt(t, filepath.Join(projectRoot, "Packages", "Local", "L.asmdef"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Packages", "Local", "Sub", "S.asmref"), "")
	writeFileAt(t, filepath.Join(projectRoot, "Assets", "A", "A.asmdef"), "")
}
