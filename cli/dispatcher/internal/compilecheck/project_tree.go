package compilecheck

import (
	"os"
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

// walkProjectTree lists each index root once, as filepath.WalkDir would walk the roots in root order.
// Why the directories are read in parallel but put together in walk order: on a project of tens of
// thousands of files the reads are what take the time, while the order decides results - the
// .asmdef index keeps the first of two assemblies declaring the same name, and the first error a
// root meets is the one reported - so only the reads may run in any order.
// Why the walk stops at the first root that fails: indexing stopped there before, so a later root
// was never read, and an error found there must not take the place of the one found first.
func walkProjectTree(projectRoot string) projectTree {
	return walkProjectTreeReadingAhead(projectRoot, readProjectTreeListings)
}

// walkProjectTreeReadingAhead is walkProjectTree with the read-ahead given. The tree it returns does
// not depend on what readAhead read: a directory it left out is read when the walk reaches it.
func walkProjectTreeReadingAhead(
	projectRoot string, readAhead func(rootPaths []string) map[string]projectTreeListing,
) projectTree {
	starts := []projectTreeRootStart{}
	readFirst := []string{}
	for _, rootPath := range projectIndexRoots(projectRoot) {
		if !directoryExists(rootPath) {
			continue
		}
		// Why Lstat: a root that is a symbolic link is listed without being followed, as WalkDir does.
		info, err := os.Lstat(rootPath)
		start := projectTreeRootStart{path: rootPath, lstatErr: err}
		if err == nil && info.IsDir() {
			start.isDirectory = true
			readFirst = append(readFirst, rootPath)
		}
		starts = append(starts, start)
	}
	listings := readAhead(readFirst)

	tree := projectTree{directories: map[string]projectTreeDirectory{}}
	for _, start := range starts {
		root, directories := restoreProjectTreeRoot(start, listings)
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

// projectTreeRootStart is what the walk learned about one root before reading it.
type projectTreeRootStart struct {
	path        string
	isDirectory bool
	lstatErr    error
}

// restoreProjectTreeRoot puts one root together from the listings read ahead, in the order
// filepath.WalkDir would have met its entries, and stops at the first error the same walk would
// have stopped at.
func restoreProjectTreeRoot(
	start projectTreeRootStart, listings map[string]projectTreeListing,
) (projectTreeRoot, map[string]projectTreeDirectory) {
	root := projectTreeRoot{path: start.path}
	directories := map[string]projectTreeDirectory{}
	if start.lstatErr != nil {
		root.walkErr = start.lstatErr

		return root, directories
	}
	if start.isDirectory {
		root.walkErr = restoreProjectTreeDirectory(&root, directories, start.path, listings)
	}

	return root, directories
}

// restoreProjectTreeDirectory lists one directory and everything below it in walk order: each name in
// name order, with a directory walked to the end before the next name.
// Why a listing that failed is not walked: WalkDir stops at the error, so what a failed read did
// return was never seen, and nothing after it in the same root was either.
func restoreProjectTreeDirectory(
	root *projectTreeRoot,
	directories map[string]projectTreeDirectory,
	path string,
	listings map[string]projectTreeListing,
) error {
	listing, readAhead := listings[path]
	if !readAhead {
		// Why a directory missing from the read-ahead is read here: whatever rule decided what to
		// read ahead, the tree must come out as the walk would have listed it.
		listing = readProjectTreeListing(path)
	}
	if listing.err != nil {
		return listing.err
	}
	directories[filepath.Clean(path)] = listing.directory
	for _, entry := range listing.directory.entries {
		entryPath := filepath.Join(path, entry.name)
		if entry.isDirectory {
			if err := restoreProjectTreeDirectory(root, directories, entryPath, listings); err != nil {
				return err
			}

			continue
		}
		switch filepath.Ext(entryPath) {
		case assemblyDefinitionExtension:
			root.definitionFiles = append(root.definitionFiles, entryPath)
		case assemblyReferenceExtension:
			root.referenceFiles = append(root.referenceFiles, entryPath)
		}
	}

	return nil
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
