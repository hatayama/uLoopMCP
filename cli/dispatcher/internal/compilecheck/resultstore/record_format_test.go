package resultstore

import (
	"os"
	"testing"
)

// Verifies a baseline of another format is reported as such even when its manifest is gone too, so
// the format is checked before the manifest.
func TestLoadBaselineReportsAnotherFormatBeforeAMissingManifest(t *testing.T) {
	directoryPath := newRecordedOutputs(t)
	if err := writeBaselineRecord(directoryPath, "Alpha", baselineRecord{
		FormatVersion: baselineRecordFormatVersion - 1,
		InputKey:      "input",
	}); err != nil {
		t.Fatalf("failed to write the baseline: %v", err)
	}

	_, reason := directoryStore(directoryPath).LoadBaseline("Alpha")

	if reason != "no baseline: it was recorded by another version of compile-check" {
		t.Errorf("reason = %q", reason)
	}
}

// Verifies the bytes a compile result is recorded as, so moving the record keeps its format.
func TestWriteUnitResultManifestWritesTheRecordedFormat(t *testing.T) {
	directoryPath := newRecordedOutputs(t)
	result := CompileResult[testDiagnostic]{
		Succeeded: false,
		Diagnostics: []testDiagnostic{{
			Severity: "error",
			Code:     "CS0103",
			Message:  "The name 'Missing' does not exist in the current context",
			File:     "Assets/Alpha/Alpha.cs",
			Line:     3,
			Column:   9,
		}},
	}

	if err := directoryStore(directoryPath).RecordCompile("Alpha", "key", result, alphaOutputs()); err != nil {
		t.Fatalf("failed to write the manifest: %v", err)
	}

	assertFileContent(t, manifestPath(directoryPath, "Alpha"), `{"FormatVersion":2,"InputKey":"key","Succeeded":false,`+
		`"Diagnostics":[{"Severity":"error","Code":"CS0103","Message":"The name 'Missing' does not exist in the current context",`+
		`"File":"Assets/Alpha/Alpha.cs","Line":3,"Column":9}],`+
		`"Outputs":{"Alpha.dll":"e020bdf568793dee747a220086c5e53c8b3af5abd959383a31297f303577efe6",`+
		`"Alpha.ref.dll":"763cdc62a869262b6ff432a40eae29a00bb96f96f7a3320845abc8cd144d12e2"}}`)
}

// Verifies the bytes a baseline is recorded as, so moving the record keeps its format.
func TestWriteBaselineRecordWritesTheRecordedFormat(t *testing.T) {
	directoryPath := t.TempDir()
	record := baselineRecord{
		FormatVersion: baselineRecordFormatVersion,
		InputKey:      "input",
		NonSourceKey: KeyDigest{
			Key:   "non-source",
			Parts: map[string]string{"compiler": "c", "source list": "s"},
		},
		WorkerKey:          "worker",
		GeneratedDirectory: "Library/out/Alpha.generated",
		GeneratedFiles:     map[string]string{"Gen/G.g.cs": "g"},
		Files:              map[string]BaselineFile{"Assets/A.cs": {ContentHash: "a", Fingerprint: "f"}},
	}

	if err := writeBaselineRecord(directoryPath, "Alpha", record); err != nil {
		t.Fatalf("failed to write the baseline: %v", err)
	}

	assertFileContent(t, baselineRecordPath(directoryPath, "Alpha"), `{"FormatVersion":3,"InputKey":"input",`+
		`"NonSourceKey":{"Key":"non-source","Parts":{"compiler":"c","source list":"s"}},"WorkerKey":"worker",`+
		`"GeneratedDirectory":"Library/out/Alpha.generated","GeneratedFiles":{"Gen/G.g.cs":"g"},`+
		`"Files":{"Assets/A.cs":{"ContentHash":"a","Fingerprint":"f"}}}`)
}

// assertFileContent fails the test unless the file holds exactly want.
func assertFileContent(t *testing.T, path string, want string) {
	t.Helper()
	content, err := os.ReadFile(path)
	if err != nil {
		t.Fatalf("failed to read %s: %v", path, err)
	}
	if string(content) != want {
		t.Errorf("content of %s =\n%s\nwant\n%s", path, content, want)
	}
}
