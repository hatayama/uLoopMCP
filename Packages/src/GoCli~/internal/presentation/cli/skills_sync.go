package cli

import (
	"bytes"
	"os"
	"path/filepath"
)

func syncSkillDirectory(sourceDir string, destinationDir string) error {
	parentDir := filepath.Dir(destinationDir)
	if err := os.MkdirAll(parentDir, 0o755); err != nil {
		return err
	}
	tempDir, err := os.MkdirTemp(parentDir, filepath.Base(destinationDir)+".tmp-")
	if err != nil {
		return err
	}

	replaced := false
	defer func() {
		if !replaced {
			_ = os.RemoveAll(tempDir)
		}
	}()
	if err := copySkillDirectory(sourceDir, tempDir); err != nil {
		return err
	}
	if err := os.RemoveAll(destinationDir); err != nil {
		return err
	}
	if err := os.Rename(tempDir, destinationDir); err != nil {
		return err
	}
	replaced = true
	return nil
}

func copySkillDirectory(sourceDir string, destinationDir string) error {
	return filepath.WalkDir(sourceDir, func(path string, entry os.DirEntry, walkErr error) error {
		if walkErr != nil {
			return walkErr
		}
		relativePath, err := filepath.Rel(sourceDir, path)
		if err != nil {
			return err
		}
		if relativePath == "." {
			return os.MkdirAll(destinationDir, 0o755)
		}
		if shouldSkipSkillFile(entry.Name()) {
			if entry.IsDir() {
				return filepath.SkipDir
			}
			return nil
		}

		destinationPath := filepath.Join(destinationDir, relativePath)
		if entry.IsDir() {
			return os.MkdirAll(destinationPath, 0o755)
		}

		content, err := os.ReadFile(path)
		if err != nil {
			return err
		}
		content = normalizeSkillFileContent(relativePath, content)
		return os.WriteFile(destinationPath, content, 0o644)
	})
}

func getSkillStatus(baseDir string, skill skillDefinition, grouped bool) string {
	skillDir := getPreferredSkillDir(baseDir, skill.name, grouped)
	if _, err := os.Stat(filepath.Join(skillDir, skillFileName)); err != nil {
		return "not_installed"
	}
	if isInstalledSkillOutdated(skillDir, skill) {
		return "outdated"
	}
	return "installed"
}

func isInstalledSkillOutdated(installedDir string, skill skillDefinition) bool {
	installedContent, err := os.ReadFile(filepath.Join(installedDir, skillFileName))
	if err != nil {
		return true
	}
	installedContent = normalizeSkillFileContent(skillFileName, installedContent)
	expectedContent := normalizeSkillFileContent(skillFileName, skill.content)
	if !bytes.Equal(installedContent, expectedContent) {
		return true
	}

	expectedFiles := collectComparableSkillFiles(skill.sourceDirectory)
	installedFiles := collectComparableSkillFiles(installedDir)
	if len(expectedFiles) != len(installedFiles) {
		return true
	}
	for relativePath, expectedContent := range expectedFiles {
		installedContent, ok := installedFiles[relativePath]
		if !ok || !bytes.Equal(expectedContent, installedContent) {
			return true
		}
	}
	return false
}

func collectComparableSkillFiles(root string) map[string][]byte {
	files := map[string][]byte{}
	_ = filepath.WalkDir(root, func(path string, entry os.DirEntry, walkErr error) error {
		if walkErr != nil || entry.IsDir() || shouldSkipSkillFile(entry.Name()) {
			return nil
		}
		relativePath, err := filepath.Rel(root, path)
		if err != nil || relativePath == skillFileName {
			return nil
		}
		content, err := os.ReadFile(path)
		if err != nil {
			return nil
		}
		files[relativePath] = normalizeSkillFileContent(relativePath, content)
		return nil
	})
	return files
}
