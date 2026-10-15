package compilecheck

import (
	"io/fs"
	"path/filepath"
)

// oracleWalkProjectTree is the single-walk listing as it was before the walk read directories in
// parallel. The parallel walk must produce the same tree, so the differential tests compare the two.
func oracleWalkProjectTree(projectRoot string) projectTree {
	tree := projectTree{directories: map[string]projectTreeDirectory{}}
	for _, rootPath := range projectIndexRoots(projectRoot) {
		if !directoryExists(rootPath) {
			continue
		}
		root, directories := oracleWalkProjectTreeRoot(rootPath)
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

// oracleWalkProjectTreeRoot lists one root with filepath.WalkDir, as the walk did before.
func oracleWalkProjectTreeRoot(rootPath string) (projectTreeRoot, map[string]projectTreeDirectory) {
	root := projectTreeRoot{path: rootPath}
	directories := map[string]projectTreeDirectory{}
	root.walkErr = filepath.WalkDir(rootPath, func(path string, entry fs.DirEntry, err error) error {
		if err != nil {
			return err
		}
		if path == rootPath {
			if entry.IsDir() {
				directories[filepath.Clean(path)] = oracleNewProjectTreeDirectory()
			}

			return nil
		}
		if isUnityIgnoredName(entry.Name()) {
			if entry.IsDir() {
				return filepath.SkipDir
			}

			return nil
		}
		oracleAddProjectTreeEntry(directories, path, entry)
		if entry.IsDir() {
			directories[filepath.Clean(path)] = oracleNewProjectTreeDirectory()

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

// oracleNewProjectTreeDirectory starts the listing of one directory.
func oracleNewProjectTreeDirectory() projectTreeDirectory {
	return projectTreeDirectory{byName: map[string]projectTreeEntry{}}
}

// oracleAddProjectTreeEntry records one name in the listing of its parent.
func oracleAddProjectTreeEntry(directories map[string]projectTreeDirectory, path string, entry fs.DirEntry) {
	parentPath := filepath.Dir(filepath.Clean(path))
	parent, started := directories[parentPath]
	if !started {
		parent = oracleNewProjectTreeDirectory()
	}
	listed := projectTreeEntry{
		name:        entry.Name(),
		isDirectory: entry.IsDir(),
		isRegular:   entry.Type().IsRegular(),
	}
	parent.entries = append(parent.entries, listed)
	parent.byName[listed.name] = listed
	directories[parentPath] = parent
}
