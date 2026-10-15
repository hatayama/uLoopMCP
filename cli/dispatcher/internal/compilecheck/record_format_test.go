package compilecheck

import (
	"os"
	"testing"
)

// Verifies a baseline of another format is reported as such even when its manifest is gone too, so
// the format is checked before the manifest.
func TestLoadBaselineReportsAnotherFormatBeforeAMissingManifest(t *testing.T) {
	project := newFastPathProject(t, fastPathMinimumSources)
	project.recordBaseline(t)
	record := project.readBaseline(t)
	record.FormatVersion = baselineRecordFormatVersion - 1
	project.writeJSON(t, project.baselinePath(), record)
	if err := os.Remove(project.manifestPathOf("Alpha")); err != nil {
		t.Fatalf("failed to remove the manifest: %v", err)
	}

	_, reason := openRecordStore(project.root, project.plan.OutputDir).loadBaseline("Alpha")

	if reason != "no baseline: it was recorded by another version of compile-check" {
		t.Errorf("reason = %q", reason)
	}
}

// Verifies the bytes a compile result is recorded as, so moving the record keeps its format.
func TestWriteUnitResultManifestWritesTheRecordedFormat(t *testing.T) {
	outputDirectoryPath, unit := newManifestUnit(t)
	result := recordedCompile{
		Succeeded: false,
		Diagnostics: []Diagnostic{{
			Severity: severityError,
			Code:     "CS0103",
			Message:  "The name 'Missing' does not exist in the current context",
			File:     "Assets/Alpha/Alpha.cs",
			Line:     3,
			Column:   9,
		}},
	}

	if err := manifestStore(outputDirectoryPath).recordCompile(
		unit.Assembly.AssemblyName, "key", result, unitOutputNames(unit)); err != nil {
		t.Fatalf("failed to write the manifest: %v", err)
	}

	assertFileContent(t, manifestPath(outputDirectoryPath, unit.Assembly.AssemblyName), `{"FormatVersion":1,"InputKey":"key","Succeeded":false,`+
		`"Diagnostics":[{"Severity":"error","Code":"CS0103","Message":"The name 'Missing' does not exist in the current context",`+
		`"File":"Assets/Alpha/Alpha.cs","Line":3,"Column":9}],`+
		`"Outputs":{"Alpha.dll":"e020bdf568793dee747a220086c5e53c8b3af5abd959383a31297f303577efe6",`+
		`"Alpha.ref.dll":"763cdc62a869262b6ff432a40eae29a00bb96f96f7a3320845abc8cd144d12e2"}}`)
}

// Verifies the bytes a baseline is recorded as, so moving the record keeps its format.
func TestWriteBaselineRecordWritesTheRecordedFormat(t *testing.T) {
	outputDirectoryPath, unit := newManifestUnit(t)
	record := baselineRecord{
		FormatVersion: baselineRecordFormatVersion,
		InputKey:      "input",
		NonSourceKey: nonSourceIdentity{
			Key:   "non-source",
			Parts: map[string]string{"compiler": "c", "source list": "s"},
		},
		WorkerKey:          "worker",
		GeneratedDirectory: "Library/out/Alpha.generated",
		GeneratedFiles:     map[string]string{"Gen/G.g.cs": "g"},
		Files:              map[string]baselineFile{"Assets/A.cs": {ContentHash: "a", Fingerprint: "f"}},
	}

	if err := writeBaselineRecord(outputDirectoryPath, unit.Assembly.AssemblyName, record); err != nil {
		t.Fatalf("failed to write the baseline: %v", err)
	}

	assertFileContent(t, baselineRecordPath(outputDirectoryPath, unit.Assembly.AssemblyName), `{"FormatVersion":3,"InputKey":"input",`+
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
