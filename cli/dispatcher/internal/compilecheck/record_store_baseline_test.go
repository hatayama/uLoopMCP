package compilecheck

import (
	"os"
	"path/filepath"
	"testing"
)

// Verifies that only generated C# files are recorded, keyed relative to the generator output directory.
func TestHashGeneratedFilesRecordsOnlyCSharpFiles(t *testing.T) {
	directory := t.TempDir()
	if err := os.MkdirAll(filepath.Join(directory, "Gen"), outputDirPermissions); err != nil {
		t.Fatalf("failed to create the generator folder: %v", err)
	}
	writeOutputFile(t, filepath.Join(directory, "Gen"), "A.g.cs", "class A { }")
	writeOutputFile(t, filepath.Join(directory, "Gen"), "notes.txt", "not code")

	hashes, err := hashGeneratedFiles(directory)

	if err != nil || len(hashes) != 1 || hashes["Gen/A.g.cs"] == "" {
		t.Errorf("expected only Gen/A.g.cs, got %v (%v)", hashes, err)
	}
	if missing, err := hashGeneratedFiles(filepath.Join(directory, "missing")); err != nil || len(missing) != 0 {
		t.Errorf("expected a missing directory to hold no files, got %v (%v)", missing, err)
	}
}

// Verifies that a baseline another version of compile-check wrote is not used, and says so rather
// than reporting that no full compile ran, even when the compile it describes is still on disk.
func TestLoadBaselineReportsABaselineOfAnotherFormat(t *testing.T) {
	directoryPath := newRecordedOutputs(t)
	store := directoryStore(directoryPath)
	if err := store.recordCompile("Alpha", "input", recordedCompile{Succeeded: true}, alphaOutputs()); err != nil {
		t.Fatalf("failed to write the manifest: %v", err)
	}
	if err := writeBaselineRecord(directoryPath, "Alpha", baselineRecord{
		FormatVersion: baselineRecordFormatVersion - 1,
		InputKey:      "input",
	}); err != nil {
		t.Fatalf("failed to write the baseline: %v", err)
	}

	_, reason := store.loadBaseline("Alpha")

	if reason != "no baseline: it was recorded by another version of compile-check" {
		t.Errorf("reason = %q", reason)
	}
}
