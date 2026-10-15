package resultstore

import (
	"encoding/json"
	"os"
	"path/filepath"
	"reflect"
	"strings"
	"testing"
)

// checkedBaseline is the baseline the tests' checks ran against.
func checkedBaseline() Baseline[testDiagnostic] {
	return Baseline[testDiagnostic]{InputKey: "baseline"}
}

// checkedResult is a check the tests record, with every field set so a field the store drops shows.
func checkedResult() FastCheckResult[testDiagnostic] {
	return FastCheckResult[testDiagnostic]{
		Succeeded: true,
		Diagnostics: []testDiagnostic{{
			Severity: "warning", Code: "CS0168", Message: "unused", File: "Assets/Alpha/Alpha.cs", Line: 3, Column: 9,
		}},
		ChangedFiles:       []string{"Assets/Alpha/Alpha.cs"},
		UncheckedCodes:     []string{"CS0169"},
		GeneratorsNotRerun: true,
	}
}

// recordCheck records checkedResult for Alpha under the input key "input", the checked baseline and
// the worker build "worker".
func recordCheck(t *testing.T, directoryPath string) {
	t.Helper()
	err := directoryStore(directoryPath).RecordFastCheck("Alpha", "input", checkedBaseline(), "worker", checkedResult())
	if err != nil {
		t.Fatalf("failed to record the check: %v", err)
	}
}

// rewriteCheckRecord reads Alpha's recorded check, lets change alter it, and writes it back.
func rewriteCheckRecord(t *testing.T, directoryPath string, change func(record *fastCheckRecord[testDiagnostic])) {
	t.Helper()
	path := fastResultPath(directoryPath, "Alpha")
	content, err := os.ReadFile(path)
	if err != nil {
		t.Fatalf("failed to read the recorded check: %v", err)
	}
	var record fastCheckRecord[testDiagnostic]
	if err := json.Unmarshal(content, &record); err != nil {
		t.Fatalf("failed to parse the recorded check: %v", err)
	}
	change(&record)
	rewritten, err := json.Marshal(record)
	if err != nil {
		t.Fatalf("failed to encode the recorded check: %v", err)
	}
	if err := os.WriteFile(path, rewritten, 0o600); err != nil {
		t.Fatalf("failed to write the recorded check back: %v", err)
	}
}

// Verifies a recorded check comes back exactly as it was recorded.
func TestReusableFastCheckReturnsTheRecordedCheck(t *testing.T) {
	directoryPath := t.TempDir()
	recordCheck(t, directoryPath)

	result, found := directoryStore(directoryPath).ReusableFastCheck("Alpha", "input", checkedBaseline(), "worker")

	if !found {
		t.Fatal("expected the recorded check to be found")
	}
	if !reflect.DeepEqual(result, checkedResult()) {
		t.Errorf("result = %+v, want %+v", result, checkedResult())
	}
}

// Verifies a check recorded for other inputs is not handed out.
func TestReusableFastCheckRejectsAnotherInputKey(t *testing.T) {
	directoryPath := t.TempDir()
	recordCheck(t, directoryPath)

	if _, found := directoryStore(directoryPath).ReusableFastCheck("Alpha", "other", checkedBaseline(), "worker"); found {
		t.Error("a check recorded for other inputs should not be found")
	}
}

// Verifies a check recorded against another baseline is not handed out.
func TestReusableFastCheckRejectsAnotherBaseline(t *testing.T) {
	directoryPath := t.TempDir()
	recordCheck(t, directoryPath)
	baseline := Baseline[testDiagnostic]{InputKey: "other baseline"}

	if _, found := directoryStore(directoryPath).ReusableFastCheck("Alpha", "input", baseline, "worker"); found {
		t.Error("a check recorded against another baseline should not be found")
	}
}

// Verifies a check recorded by another worker build is not handed out.
func TestReusableFastCheckRejectsAnotherWorkerBuild(t *testing.T) {
	directoryPath := t.TempDir()
	recordCheck(t, directoryPath)

	if _, found := directoryStore(directoryPath).ReusableFastCheck("Alpha", "input", checkedBaseline(), "other"); found {
		t.Error("a check recorded by another worker build should not be found")
	}
}

// Verifies a check recorded by another version of compile-check is not handed out.
func TestReusableFastCheckRejectsAnotherFormatVersion(t *testing.T) {
	directoryPath := t.TempDir()
	recordCheck(t, directoryPath)
	rewriteCheckRecord(t, directoryPath, func(record *fastCheckRecord[testDiagnostic]) {
		record.FormatVersion = fastResultFormatVersion - 1
	})

	if _, found := directoryStore(directoryPath).ReusableFastCheck("Alpha", "input", checkedBaseline(), "worker"); found {
		t.Error("a check of another format version should not be found")
	}
}

// Verifies a record that cannot be read is treated as no record: one that is not JSON, and one whose
// version and keys match but one field holds a value of the wrong type, which decoding would
// otherwise fill in only partly.
func TestReusableFastCheckRejectsAnUnreadableRecord(t *testing.T) {
	for name, write := range map[string]func(t *testing.T, directoryPath string){
		"not json": func(t *testing.T, directoryPath string) {
			writeOutputFile(t, directoryPath, "Alpha"+fastResultExtension, "{not json")
		},
		"wrong field type": func(t *testing.T, directoryPath string) {
			recordCheck(t, directoryPath)
			path := fastResultPath(directoryPath, "Alpha")
			content, err := os.ReadFile(path)
			if err != nil {
				t.Fatalf("failed to read the recorded check: %v", err)
			}
			rewritten := strings.Replace(string(content), `"Line":3`, `"Line":"three"`, 1)
			if err := os.WriteFile(path, []byte(rewritten), 0o600); err != nil {
				t.Fatalf("failed to write the recorded check back: %v", err)
			}
		},
	} {
		t.Run(name, func(t *testing.T) {
			directoryPath := t.TempDir()
			write(t, directoryPath)

			if _, found := directoryStore(directoryPath).ReusableFastCheck("Alpha", "input", checkedBaseline(), "worker"); found {
				t.Error("an unreadable record should not be found")
			}
		})
	}
}

// Verifies an empty input key never matches, even a record that holds an empty input key.
func TestReusableFastCheckRejectsAnEmptyInputKey(t *testing.T) {
	directoryPath := t.TempDir()
	recordCheck(t, directoryPath)
	rewriteCheckRecord(t, directoryPath, func(record *fastCheckRecord[testDiagnostic]) {
		record.InputKey = ""
	})

	if _, found := directoryStore(directoryPath).ReusableFastCheck("Alpha", "", checkedBaseline(), "worker"); found {
		t.Error("an empty input key should not find a record")
	}
}

// Verifies a check is not recorded without each of the three keys it is looked up by, and nothing is
// written in that case.
func TestRecordFastCheckRefusesAMissingKey(t *testing.T) {
	for name, testCase := range map[string]struct {
		key       string
		baseline  Baseline[testDiagnostic]
		workerKey string
		want      string
	}{
		"input key": {key: "", baseline: checkedBaseline(), workerKey: "worker", want: "an input key"},
		"baseline":  {key: "input", baseline: Baseline[testDiagnostic]{}, workerKey: "worker", want: "a baseline"},
		"worker":    {key: "input", baseline: checkedBaseline(), workerKey: "", want: "a worker build"},
	} {
		t.Run(name, func(t *testing.T) {
			directoryPath := t.TempDir()

			err := directoryStore(directoryPath).RecordFastCheck(
				"Alpha", testCase.key, testCase.baseline, testCase.workerKey, checkedResult())

			want := "refusing to record a changed-files check of Alpha without " + testCase.want
			if err == nil || err.Error() != want {
				t.Errorf("err = %v, want %q", err, want)
			}
			if _, statErr := os.Stat(fastResultPath(directoryPath, "Alpha")); !os.IsNotExist(statErr) {
				t.Errorf("expected no record to be written, got %v", statErr)
			}
		})
	}
}

// Verifies a check that cannot be renamed into place is reported as a write failure and leaves no
// temporary file behind.
func TestRecordFastCheckReportsAFailedRename(t *testing.T) {
	directoryPath := t.TempDir()
	blockManifestPath(t, fastResultPath(directoryPath, "Alpha"))

	err := directoryStore(directoryPath).RecordFastCheck("Alpha", "input", checkedBaseline(), "worker", checkedResult())

	if err == nil || !strings.HasPrefix(err.Error(), "failed to write "+fastResultPath(directoryPath, "Alpha")+":") {
		t.Fatalf("err = %v", err)
	}
	matches, globErr := filepath.Glob(filepath.Join(directoryPath, "*"+resultManifestTempSuffix))
	if globErr != nil || len(matches) != 0 {
		t.Fatalf("temporary records left behind: %v (glob error %v)", matches, globErr)
	}
}

// Verifies forgetting an assembly removes its recorded check too, so no check outlives the compile
// it was checked against.
func TestForgetRemovesTheRecordedCheck(t *testing.T) {
	directoryPath := t.TempDir()
	recordCheck(t, directoryPath)

	if err := directoryStore(directoryPath).Forget("Alpha"); err != nil {
		t.Fatalf("expected the records to be removed, got error: %v", err)
	}

	if _, err := os.Stat(fastResultPath(directoryPath, "Alpha")); !os.IsNotExist(err) {
		t.Errorf("expected the recorded check to be removed, got %v", err)
	}
}

// Verifies the bytes a check is recorded as, so the record keeps its format.
func TestRecordFastCheckWritesTheRecordedFormat(t *testing.T) {
	directoryPath := t.TempDir()
	result := FastCheckResult[testDiagnostic]{
		Succeeded: false,
		Diagnostics: []testDiagnostic{{
			Severity: "error",
			Code:     "CS0103",
			Message:  "The name 'Missing' does not exist in the current context",
			File:     "Assets/Alpha/Alpha.cs",
			Line:     3,
			Column:   9,
		}},
		ChangedFiles:       []string{"Assets/Alpha/Alpha.cs"},
		UncheckedCodes:     []string{"CS0169"},
		GeneratorsNotRerun: true,
	}

	if err := directoryStore(directoryPath).RecordFastCheck("Alpha", "input", checkedBaseline(), "worker", result); err != nil {
		t.Fatalf("failed to record the check: %v", err)
	}

	assertFileContent(t, fastResultPath(directoryPath, "Alpha"), `{"FormatVersion":1,"InputKey":"input",`+
		`"BaselineInputKey":"baseline","WorkerKey":"worker","Succeeded":false,`+
		`"Diagnostics":[{"Severity":"error","Code":"CS0103","Message":"The name 'Missing' does not exist in the current context",`+
		`"File":"Assets/Alpha/Alpha.cs","Line":3,"Column":9}],`+
		`"ChangedFiles":["Assets/Alpha/Alpha.cs"],"UncheckedCodes":["CS0169"],"GeneratorsNotRerun":true}`)
}
