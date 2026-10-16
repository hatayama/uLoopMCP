package resultstore

import (
	"os"
	"path/filepath"
	"reflect"
	"testing"
)

// comparedInputs names the comparison the tests record a verdict for.
func comparedInputs() SurfaceInputs {
	return SurfaceInputs{ProducedSha256: "produced", UnityBuildSha256: "unity", WorkerKey: "worker"}
}

// recordSurface records a "same" verdict with a detail for Alpha under comparedInputs.
func recordSurface(t *testing.T, directoryPath string) {
	t.Helper()
	err := directoryStore(directoryPath).RecordSurfaceVerdict("Alpha", comparedInputs(), SurfaceVerdict{Same: true, Detail: "detail"})
	if err != nil {
		t.Fatalf("failed to record the surface verdict: %v", err)
	}
}

// Verifies a recorded verdict is read back, with its detail, for the very comparison it answered.
func TestReusableSurfaceVerdictReadsBackTheRecordedVerdict(t *testing.T) {
	directoryPath := t.TempDir()
	recordSurface(t, directoryPath)

	got, found := directoryStore(directoryPath).ReusableSurfaceVerdict("Alpha", comparedInputs())

	if !found || !reflect.DeepEqual(got, SurfaceVerdict{Same: true, Detail: "detail"}) {
		t.Errorf("expected the recorded verdict, got %+v (found %v)", got, found)
	}
}

// Verifies a recorded verdict answers no other comparison: each input on its own keeps it unused.
func TestReusableSurfaceVerdictRejectsAnotherComparison(t *testing.T) {
	directoryPath := t.TempDir()
	recordSurface(t, directoryPath)
	cases := map[string]func(inputs *SurfaceInputs){
		"produced":    func(inputs *SurfaceInputs) { inputs.ProducedSha256 = "other" },
		"unity":       func(inputs *SurfaceInputs) { inputs.UnityBuildSha256 = "other" },
		"worker":      func(inputs *SurfaceInputs) { inputs.WorkerKey = "other" },
		"no produced": func(inputs *SurfaceInputs) { inputs.ProducedSha256 = "" },
	}
	for name, change := range cases {
		inputs := comparedInputs()
		change(&inputs)
		if _, found := directoryStore(directoryPath).ReusableSurfaceVerdict("Alpha", inputs); found {
			t.Errorf("%s: expected the record not to be used", name)
		}
	}
}

// Verifies a changed verdict is read back with its changed names and its unnamed reason.
func TestReusableSurfaceVerdictReadsBackChangedNames(t *testing.T) {
	directoryPath := t.TempDir()
	recorded := []SurfaceVerdict{
		{Detail: "named", ChangedNames: []string{"C", "M"}},
		{Detail: "unnamed", UnnamedReason: "operator op_Addition"},
	}
	for _, verdict := range recorded {
		if err := directoryStore(directoryPath).RecordSurfaceVerdict("Alpha", comparedInputs(), verdict); err != nil {
			t.Fatalf("failed to record the surface verdict: %v", err)
		}

		got, found := directoryStore(directoryPath).ReusableSurfaceVerdict("Alpha", comparedInputs())

		if !found || !reflect.DeepEqual(got, verdict) {
			t.Errorf("expected %+v back, got %+v (found %v)", verdict, got, found)
		}
	}
}

// Verifies a record written before verdicts carried changed names is not used, so a change it
// recorded is asked again rather than read as naming nothing.
func TestReusableSurfaceVerdictRejectsAVersionOneRecord(t *testing.T) {
	directoryPath := t.TempDir()
	content := `{"FormatVersion":1,"ProducedSha256":"produced","UnityBuildSha256":"unity","WorkerKey":"worker","Same":false,"Detail":"d"}`
	if err := os.WriteFile(filepath.Join(directoryPath, "Alpha"+surfaceRecordExtension), []byte(content), 0o600); err != nil {
		t.Fatalf("failed to write the record: %v", err)
	}

	if _, found := directoryStore(directoryPath).ReusableSurfaceVerdict("Alpha", comparedInputs()); found {
		t.Error("expected a version 1 record not to be used")
	}
}

// Verifies a record that is not one this build writes is not used.
func TestReusableSurfaceVerdictRejectsAnUnreadableRecord(t *testing.T) {
	directoryPath := t.TempDir()
	for name, content := range map[string]string{
		"not json":      "not json",
		"other version": `{"FormatVersion":3,"ProducedSha256":"produced","UnityBuildSha256":"unity","WorkerKey":"worker","Same":true}`,
	} {
		if err := os.WriteFile(filepath.Join(directoryPath, "Alpha"+surfaceRecordExtension), []byte(content), 0o600); err != nil {
			t.Fatalf("failed to write the record: %v", err)
		}
		if _, found := directoryStore(directoryPath).ReusableSurfaceVerdict("Alpha", comparedInputs()); found {
			t.Errorf("%s: expected the record not to be used", name)
		}
	}
	if _, found := directoryStore(t.TempDir()).ReusableSurfaceVerdict("Alpha", comparedInputs()); found {
		t.Error("expected no verdict without a record")
	}
}

// Verifies the surface verdict is kept apart from the records Forget removes with an assembly's
// outputs: it is keyed by content, so it can only answer the comparison it was made for.
func TestForgetKeepsTheSurfaceVerdict(t *testing.T) {
	directoryPath := t.TempDir()
	recordSurface(t, directoryPath)

	if err := directoryStore(directoryPath).Forget("Alpha"); err != nil {
		t.Fatalf("failed to forget Alpha: %v", err)
	}

	if _, found := directoryStore(directoryPath).ReusableSurfaceVerdict("Alpha", comparedInputs()); !found {
		t.Error("expected the surface verdict to survive Forget")
	}
}
