package resultstore

import (
	"os"
	"path/filepath"
	"testing"
)

const (
	assemblyExtension          = ".dll"
	referenceAssemblyExtension = ".ref.dll"
	testDirectoryPermissions   = 0o755
)

// testDiagnostic stands in for the caller's diagnostic type. Its fields are in the same order as the
// caller's, so a recorded compile result keeps the bytes the caller's type gives it.
type testDiagnostic struct {
	Severity string
	Code     string
	Message  string
	File     string
	Line     int
	Column   int
}

// writeOutputFile writes one file into the check's own output directory.
func writeOutputFile(t *testing.T, outputDirectoryPath string, name string, content string) {
	t.Helper()
	if err := os.WriteFile(
		filepath.Join(outputDirectoryPath, name), []byte(content), 0o600); err != nil {
		t.Fatalf("failed to write %s: %v", name, err)
	}
}

// writeFileAt creates a file and every parent directory it needs.
func writeFileAt(t *testing.T, path string, content string) {
	t.Helper()
	if err := os.MkdirAll(filepath.Dir(path), testDirectoryPermissions); err != nil {
		t.Fatalf("failed to create directory for %s: %v", path, err)
	}
	if err := os.WriteFile(path, []byte(content), 0o644); err != nil {
		t.Fatalf("failed to write %s: %v", path, err)
	}
}

// makeDirAt creates a directory and every parent directory it needs.
func makeDirAt(t *testing.T, path string) {
	t.Helper()
	if err := os.MkdirAll(path, testDirectoryPermissions); err != nil {
		t.Fatalf("failed to create directory %s: %v", path, err)
	}
}
