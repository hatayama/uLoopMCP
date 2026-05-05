package architecture

import (
	"encoding/json"
	"fmt"
	"io"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"testing"
)

const (
	modulePath             = "github.com/hatayama/unity-cli-loop/Packages/src/GoCli"
	maxProductionFileLines = 500
)

type goPackage struct {
	ImportPath string
	Imports    []string
}

// Tests that onion layers only import packages from allowed inner or outer boundaries.
func TestOnionLayerDependencies(t *testing.T) {
	moduleRoot := findModuleRoot(t)
	packages := listPackages(t, moduleRoot)
	for _, goPackage := range packages {
		if strings.Contains(goPackage.ImportPath, "/internal/cli") {
			t.Fatalf("legacy aggregate cli package must not be reintroduced: %s", goPackage.ImportPath)
		}
		sourceLayer := layerOf(goPackage.ImportPath)
		if sourceLayer == "" {
			continue
		}
		for _, importedPath := range goPackage.Imports {
			if !strings.HasPrefix(importedPath, modulePath) {
				continue
			}
			targetLayer := layerOf(importedPath)
			if targetLayer == "" {
				continue
			}
			if !isAllowedDependency(sourceLayer, targetLayer, importedPath) {
				t.Fatalf("%s package %s must not import %s package %s", sourceLayer, goPackage.ImportPath, targetLayer, importedPath)
			}
		}
	}
}

// Tests that production packages make the core, dispatcher, and shared boundaries visible in the tree.
func TestInternalPackagesStayInsideExplicitRuntimeBoundaries(t *testing.T) {
	moduleRoot := findModuleRoot(t)
	packages := listPackages(t, moduleRoot)
	for _, goPackage := range packages {
		if !strings.HasPrefix(goPackage.ImportPath, modulePath+"/internal/") {
			continue
		}
		if goPackage.ImportPath == modulePath+"/internal/architecture" {
			continue
		}
		if strings.HasPrefix(goPackage.ImportPath, modulePath+"/internal/core/") {
			continue
		}
		if strings.HasPrefix(goPackage.ImportPath, modulePath+"/internal/shared/") {
			continue
		}
		if strings.HasPrefix(goPackage.ImportPath, modulePath+"/internal/dispatcher") {
			continue
		}
		t.Fatalf("internal package must live under core, dispatcher, or shared: %s", goPackage.ImportPath)
	}
}

// Tests that production files stay small enough to keep each file focused on one responsibility.
func TestProductionGoFilesStayFocused(t *testing.T) {
	moduleRoot := findModuleRoot(t)
	err := filepath.WalkDir(moduleRoot, func(path string, entry os.DirEntry, walkErr error) error {
		if walkErr != nil {
			return walkErr
		}
		if entry.IsDir() {
			if entry.Name() == "dist" {
				return filepath.SkipDir
			}
			return nil
		}
		if !strings.HasSuffix(entry.Name(), ".go") || strings.HasSuffix(entry.Name(), "_test.go") {
			return nil
		}
		lineCount, err := countLines(path)
		if err != nil {
			return err
		}
		if lineCount > maxProductionFileLines {
			relativePath, err := filepath.Rel(moduleRoot, path)
			if err != nil {
				return err
			}
			return fmt.Errorf("%s has %d lines; split files above %d lines", relativePath, lineCount, maxProductionFileLines)
		}
		return nil
	})
	if err != nil {
		t.Fatal(err)
	}
}

// Tests that the global dispatcher binary stays independent from all project-local core packages.
func TestDispatcherCommandDoesNotDependOnCorePackages(t *testing.T) {
	moduleRoot := findModuleRoot(t)
	command := exec.Command("go", "list", "-deps", "./cmd/uloop-dispatcher")
	command.Dir = moduleRoot
	output, err := command.Output()
	if err != nil {
		t.Fatalf("go list failed: %v", err)
	}

	for _, dependency := range strings.Split(strings.TrimSpace(string(output)), "\n") {
		if strings.HasPrefix(dependency, modulePath+"/internal/core/") {
			t.Fatalf("dispatcher command must not depend on core package %s", dependency)
		}
	}
}

func listPackages(t *testing.T, moduleRoot string) []goPackage {
	t.Helper()
	command := exec.Command("go", "list", "-json", "./...")
	command.Dir = moduleRoot
	output, err := command.Output()
	if err != nil {
		t.Fatalf("go list failed: %v", err)
	}

	decoder := json.NewDecoder(strings.NewReader(string(output)))
	packages := []goPackage{}
	for {
		var goPackage goPackage
		err := decoder.Decode(&goPackage)
		if err == io.EOF {
			break
		}
		if err != nil {
			t.Fatalf("failed to decode go list output: %v", err)
		}
		packages = append(packages, goPackage)
	}
	return packages
}

func layerOf(importPath string) string {
	switch {
	case strings.Contains(importPath, "/internal/shared/domain"):
		return "domain"
	case strings.Contains(importPath, "/internal/shared/version"):
		return "version"
	case strings.Contains(importPath, "/internal/shared/adapters"):
		return "shared-adapters"
	case strings.Contains(importPath, "/internal/dispatcher"):
		return "dispatcher"
	case strings.Contains(importPath, "/internal/core/application"):
		return "application"
	case strings.Contains(importPath, "/internal/core/ports"):
		return "ports"
	case strings.Contains(importPath, "/internal/core/adapters"):
		return "core-adapters"
	case strings.Contains(importPath, "/internal/core/presentation"):
		return "presentation"
	case strings.Contains(importPath, "/internal/core/app"):
		return "app"
	case strings.Contains(importPath, "/cmd/"):
		return "cmd"
	default:
		return ""
	}
}

func isAllowedDependency(sourceLayer string, targetLayer string, importedPath string) bool {
	switch sourceLayer {
	case "domain":
		return targetLayer == "domain"
	case "version":
		return targetLayer == "version"
	case "shared-adapters":
		return targetLayer == "domain" || targetLayer == "shared-adapters"
	case "dispatcher":
		return targetLayer == "domain" || targetLayer == "version" || targetLayer == "shared-adapters" || targetLayer == "dispatcher"
	case "application":
		return targetLayer == "domain" || targetLayer == "ports" || targetLayer == "application"
	case "ports":
		return targetLayer == "domain" || targetLayer == "ports"
	case "core-adapters":
		return targetLayer == "domain" || targetLayer == "ports" || targetLayer == "application" || targetLayer == "shared-adapters" || targetLayer == "core-adapters"
	case "presentation":
		return targetLayer == "domain" || targetLayer == "version" || targetLayer == "ports" || targetLayer == "application" || targetLayer == "shared-adapters" || targetLayer == "core-adapters" || targetLayer == "presentation"
	case "app":
		return targetLayer == "domain" || targetLayer == "version" || targetLayer == "ports" || targetLayer == "application" || targetLayer == "shared-adapters" || targetLayer == "core-adapters" || targetLayer == "presentation"
	case "cmd":
		return targetLayer == "app" || targetLayer == "dispatcher" || importedPath == modulePath+"/internal/core/app"
	default:
		return true
	}
}

func findModuleRoot(t *testing.T) string {
	t.Helper()
	currentPath, err := os.Getwd()
	if err != nil {
		t.Fatalf("failed to get working directory: %v", err)
	}
	for {
		if _, err := os.Stat(filepath.Join(currentPath, "go.mod")); err == nil {
			return currentPath
		}
		parentPath := filepath.Dir(currentPath)
		if parentPath == currentPath {
			t.Fatal("go.mod not found")
		}
		currentPath = parentPath
	}
}

func countLines(path string) (int, error) {
	content, err := os.ReadFile(path)
	if err != nil {
		return 0, err
	}
	if len(content) == 0 {
		return 0, nil
	}
	lineCount := strings.Count(string(content), "\n")
	if !strings.HasSuffix(string(content), "\n") {
		lineCount++
	}
	return lineCount, nil
}
