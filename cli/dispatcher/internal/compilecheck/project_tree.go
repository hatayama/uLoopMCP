package compilecheck

import (
	"io/fs"
	"path/filepath"
)

// projectTree is a listing of the project's index roots taken by one walk at the start of a run.
// Why one walk: indexing the .asmdef files, indexing the .asmref files and re-globbing every
// assembly's sources each walked the same directories again, and on a project of tens of thousands
// of files those walks cost more than the rest of planning together.
// It answers only what the walk saw. A path it does not list is not proof of absence - a file system
// that ignores case or Unicode normalization finds a name spelled differently, and a symbolic link
// can point at nothing - so callers fall back to the disk for every negative answer.
type projectTree struct {
	roots       []projectTreeRoot
	directories map[string]projectTreeDirectory // cleaned absolute path -> its listing
}

// projectTreeRoot is what the walk found below one index root, in the order it met it.
type projectTreeRoot struct {
	path            string
	definitionFiles []string // .asmdef files, in walk order
	referenceFiles  []string // .asmref files, in walk order
	walkErr         error    // the error that stopped the walk of this root, if any
}

// projectTreeDirectory is one directory as the walk listed it, without the names Unity ignores.
type projectTreeDirectory struct {
	entries []projectTreeEntry // in walk order, which is name order
	byName  map[string]projectTreeEntry
}

// projectTreeEntry is one listed name.
type projectTreeEntry struct {
	name        string
	isDirectory bool
	isRegular   bool // a regular file, as opposed to a directory, a symbolic link or a device
}

// walkProjectTree walks each index root once, in root order.
// Why the walk stops at the first root that fails: indexing stopped there before, so a later root
// was never read, and an error found there must not take the place of the one found first.
// Why the roots are walked one after another rather than in parallel: the .asmdef index keeps the
// first of two assemblies declaring the same name, so the order across roots decides the winner.
func walkProjectTree(projectRoot string) projectTree {
	tree := projectTree{directories: map[string]projectTreeDirectory{}}
	for _, rootPath := range projectIndexRoots(projectRoot) {
		if !directoryExists(rootPath) {
			continue
		}
		root, directories := walkProjectTreeRoot(rootPath)
		tree.roots = append(tree.roots, root)
		if root.walkErr != nil {
			break
		}
		for path, directory := range directories {
			tree.directories[path] = directory
		}
	}

	return tree
}

// walkProjectTreeRoot lists one root with the same rules the indexes walked it with: names Unity
// ignores are left out, and symbolic links are listed but not followed.
func walkProjectTreeRoot(rootPath string) (projectTreeRoot, map[string]projectTreeDirectory) {
	root := projectTreeRoot{path: rootPath}
	directories := map[string]projectTreeDirectory{}
	root.walkErr = filepath.WalkDir(rootPath, func(path string, entry fs.DirEntry, err error) error {
		if err != nil {
			return err
		}
		if path == rootPath {
			if entry.IsDir() {
				directories[filepath.Clean(path)] = newProjectTreeDirectory()
			}

			return nil
		}
		if isUnityIgnoredName(entry.Name()) {
			if entry.IsDir() {
				return filepath.SkipDir
			}

			return nil
		}
		addProjectTreeEntry(directories, path, entry)
		if entry.IsDir() {
			directories[filepath.Clean(path)] = newProjectTreeDirectory()

			return nil
		}
		switch filepath.Ext(path) {
		case assemblyDefinitionExtension:
			root.definitionFiles = append(root.definitionFiles, path)
		case assemblyReferenceExtension:
			root.referenceFiles = append(root.referenceFiles, path)
		}

		return nil
	})

	return root, directories
}

// newProjectTreeDirectory starts the listing of one directory.
func newProjectTreeDirectory() projectTreeDirectory {
	return projectTreeDirectory{byName: map[string]projectTreeEntry{}}
}

// addProjectTreeEntry records one name in the listing of its parent, which the walk always lists
// before any of its children.
func addProjectTreeEntry(directories map[string]projectTreeDirectory, path string, entry fs.DirEntry) {
	parentPath := filepath.Dir(filepath.Clean(path))
	parent := directories[parentPath]
	listed := projectTreeEntry{
		name:        entry.Name(),
		isDirectory: entry.IsDir(),
		isRegular:   entry.Type().IsRegular(),
	}
	parent.entries = append(parent.entries, listed)
	parent.byName[listed.name] = listed
	directories[parentPath] = parent
}

// directory returns the listing of a directory the walk went through.
func (tree projectTree) directory(path string) (projectTreeDirectory, bool) {
	directory, listed := tree.directories[filepath.Clean(path)]

	return directory, listed
}

// hasRegularFile reports whether the walk listed a regular file at exactly this path. A false
// answer means only that the walk did not see one under this spelling.
func (tree projectTree) hasRegularFile(path string) bool {
	directory, listed := tree.directory(filepath.Dir(path))
	if !listed {
		return false
	}
	entry, found := directory.byName[filepath.Base(path)]

	return found && entry.isRegular
}

// fileExists answers what fileExists answers on the disk, from the listing when it holds a regular
// file under exactly this spelling. Every other case - a name the listing lacks, a path outside it,
// an entry that is a symbolic link - asks the disk, because only the disk knows whether a differently
// spelled or linked name resolves to a file.
func (tree projectTree) fileExists(path string) bool {
	return tree.hasRegularFile(path) || fileExists(path)
}

// directoryHoldsAssemblyDefinition reports whether a directory declares an assembly of its own, from
// the listing when the walk went through the directory and from the disk otherwise.
func (tree projectTree) directoryHoldsAssemblyDefinition(path string) bool {
	listing, listed := tree.directory(path)
	if !listed {
		return directoryHoldsAssemblyDefinition(path)
	}

	return listing.holdsAssemblyDefinition()
}

// holdsAssemblyDefinition reports whether a listed directory holds an .asmdef. Any entry that is not
// a directory counts, as it did when the directory was read from the disk.
func (directory projectTreeDirectory) holdsAssemblyDefinition() bool {
	for _, entry := range directory.entries {
		if !entry.isDirectory && filepath.Ext(entry.name) == assemblyDefinitionExtension {
			return true
		}
	}

	return false
}
