package dispatcher

import (
	"bufio"
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"os"
	"path/filepath"
	"testing"
	"time"

	clierrors "github.com/hatayama/unity-cli-loop/common/errors"
	"github.com/hatayama/unity-cli-loop/common/vibelog"
	"github.com/hatayama/unity-cli-loop/dispatcher/internal/compilecheck"
)

// readCompileCheckVibeLog reads every entry today's CLI VibeLog of the project holds, in order.
func readCompileCheckVibeLog(t *testing.T, projectRoot string) []vibelog.CLIVibeLogEntry {
	t.Helper()
	path := filepath.Join(projectRoot, vibelog.CLIVibeLogDirectory,
		vibelog.CLIVibeLogPrefix+"_"+time.Now().UTC().Format("20060102")+".json")
	content, err := os.ReadFile(path)
	if err != nil {
		t.Fatalf("failed to read the CLI VibeLog: %v", err)
	}

	entries := []vibelog.CLIVibeLogEntry{}
	scanner := bufio.NewScanner(bytes.NewReader(content))
	scanner.Buffer(make([]byte, 0, 64*1024), 1024*1024)
	for scanner.Scan() {
		var entry vibelog.CLIVibeLogEntry
		if err := json.Unmarshal(scanner.Bytes(), &entry); err != nil {
			t.Fatalf("failed to parse a CLI VibeLog line %q: %v", scanner.Text(), err)
		}
		entries = append(entries, entry)
	}

	return entries
}

// assertCompileCheckVibeRun checks the log holds exactly one start and one complete of the same run,
// and returns the complete entry.
func assertCompileCheckVibeRun(t *testing.T, entries []vibelog.CLIVibeLogEntry) vibelog.CLIVibeLogEntry {
	t.Helper()
	if len(entries) != 2 {
		t.Fatalf("expected a start and a complete entry, got %+v", entries)
	}
	start, complete := entries[0], entries[1]
	if start.Operation != "cli_compile_check_start" || complete.Operation != "cli_compile_check_complete" {
		t.Fatalf("operations = %q, %q", start.Operation, complete.Operation)
	}
	if start.CorrelationID == "" || start.CorrelationID != complete.CorrelationID {
		t.Errorf("correlation ids = %q, %q, want one shared id", start.CorrelationID, complete.CorrelationID)
	}

	return complete
}

// assertVibeContextJSON checks one context field serializes to the given JSON.
func assertVibeContextJSON(t *testing.T, entry vibelog.CLIVibeLogEntry, key string, want string) {
	t.Helper()
	encoded, err := json.Marshal(entry.Context[key])
	if err != nil {
		t.Fatalf("failed to encode %s: %v", key, err)
	}
	if string(encoded) != want {
		t.Errorf("%s = %s, want %s", key, encoded, want)
	}
}

// loggedCompileCheckResult is a run that compiled, fast-checked, replayed a fast check of, reused,
// and blocked one assembly each, so a field the log drops or mixes up shows.
func loggedCompileCheckResult() compilecheck.Result {
	return compilecheck.Result{
		DagDir:  "Library/Bee/artifacts/1300b0aE.dag",
		Skipped: 3,
		Blocked: []string{"Delta"},
		Units: []compilecheck.UnitResult{
			{
				Assembly: "Alpha", Succeeded: true, Duration: 1500 * time.Millisecond,
				FastPathFallbackReason: "no baseline: no full compile of this assembly is on record",
				FastPathNote:           "baseline not recorded: source changed during the compile: Assets/A.cs",
			},
			{
				Assembly: "Beta", Succeeded: true, Duration: 250 * time.Millisecond, FastChecked: true,
				FastCheckChangedFiles: []string{"Assets/B1.cs", "Assets/B2.cs"},
			},
			{
				Assembly: "Epsilon", Succeeded: true, FastChecked: true, FastCheckReused: true,
				FastCheckChangedFiles: []string{"Assets/E.cs"},
			},
			{Assembly: "Gamma", Succeeded: true, Reused: true},
		},
	}
}

// Verifies a run writes a start and a complete entry with one correlation id, and the complete
// entry carries what the response reported and how long each compiled or checked unit took.
func TestCompileCheckVibeLogRecordsASucceededRun(t *testing.T) {
	t.Setenv(vibelog.CLIVibeLogEnvName, "1")
	projectRoot := t.TempDir()
	result := loggedCompileCheckResult()

	logRun := startCompileCheckVibeLog(projectRoot, compileCheckOptions{all: true, jobs: 4})
	logRun.completed(buildCompileCheckResponse(result, projectRoot), result)

	entries := readCompileCheckVibeLog(t, projectRoot)
	complete := assertCompileCheckVibeRun(t, entries)
	assertVibeContextJSON(t, entries[0], "all", "true")
	assertVibeContextJSON(t, entries[0], "full", "false")
	assertVibeContextJSON(t, entries[0], "jobs", "4")
	assertVibeContextJSON(t, entries[0], "project_identity", `"`+vibelog.ProjectIdentity(projectRoot)+`"`)
	if complete.Level != "INFO" {
		t.Errorf("level = %q, want INFO", complete.Level)
	}
	assertVibeContextJSON(t, complete, "outcome", `"succeeded"`)
	assertVibeContextJSON(t, complete, "error_count", "0")
	assertVibeContextJSON(t, complete, "compiled", `["Alpha"]`)
	assertVibeContextJSON(t, complete, "fast_checked", `["Beta","Epsilon"]`)
	assertVibeContextJSON(t, complete, "reused", `["Gamma"]`)
	assertVibeContextJSON(t, complete, "blocked", `["Delta"]`)
	assertVibeContextJSON(t, complete, "skipped_count", "3")
	assertVibeContextJSON(t, complete, "fast_checks", `[{"assembly":"Beta","changed_file_count":2,"reused":false},`+
		`{"assembly":"Epsilon","changed_file_count":1,"reused":true}]`)
	assertVibeContextJSON(t, complete, "fallbacks",
		`[{"assembly":"Alpha","reason":"no baseline: no full compile of this assembly is on record"}]`)
	assertVibeContextJSON(t, complete, "baseline_notes",
		`[{"assembly":"Alpha","reason":"baseline not recorded: source changed during the compile: Assets/A.cs"}]`)
	assertVibeContextJSON(t, complete, "units",
		`[{"assembly":"Alpha","duration_ms":1500,"succeeded":true},{"assembly":"Beta","duration_ms":250,"succeeded":true}]`)
	if _, found := complete.Context["duration_ms"]; !found {
		t.Error("expected the run's duration to be logged")
	}
}

// Verifies a run that found compile errors is logged as a warning with the errors counted.
func TestCompileCheckVibeLogRecordsARunWithErrors(t *testing.T) {
	t.Setenv(vibelog.CLIVibeLogEnvName, "1")
	projectRoot := t.TempDir()
	result := compilecheck.Result{Units: []compilecheck.UnitResult{{
		Assembly: "Alpha", Diagnostics: []compilecheck.Diagnostic{
			{Severity: "error", Code: "CS0103", Message: "missing", File: "Assets/A.cs", Line: 1, Column: 1},
			{Severity: "error", Code: "CS0029", Message: "cannot convert", File: "Assets/A.cs", Line: 2, Column: 1},
		},
	}}}

	logRun := startCompileCheckVibeLog(projectRoot, compileCheckOptions{})
	logRun.completed(buildCompileCheckResponse(result, projectRoot), result)

	complete := assertCompileCheckVibeRun(t, readCompileCheckVibeLog(t, projectRoot))
	if complete.Level != "WARNING" {
		t.Errorf("level = %q, want WARNING", complete.Level)
	}
	assertVibeContextJSON(t, complete, "outcome", `"errors"`)
	assertVibeContextJSON(t, complete, "error_count", "2")
}

// Verifies a run that could not finish is logged as an error with what stopped it.
func TestCompileCheckVibeLogRecordsAFailedRun(t *testing.T) {
	t.Setenv(vibelog.CLIVibeLogEnvName, "1")
	projectRoot := t.TempDir()

	logRun := startCompileCheckVibeLog(projectRoot, compileCheckOptions{})
	logRun.failed(errors.New("no Bee build found"))

	complete := assertCompileCheckVibeRun(t, readCompileCheckVibeLog(t, projectRoot))
	if complete.Level != "ERROR" {
		t.Errorf("level = %q, want ERROR", complete.Level)
	}
	assertVibeContextJSON(t, complete, "outcome", `"failed"`)
	assertVibeContextJSON(t, complete, "error", `"no Bee build found"`)
	wantCode := clierrors.ClassifyError(errors.New("no Bee build found"), clierrors.ErrorContext{}).ErrorCode
	assertVibeContextJSON(t, complete, "error_code", `"`+wantCode+`"`)
}

// Verifies runCompileCheck logs the run that stopped because the project's Editor is not
// installed, as a start and a failed complete of the same run.
func TestCompileCheckVibeLogRecordsAnEditorResolutionFailure(t *testing.T) {
	t.Setenv(vibelog.CLIVibeLogEnvName, "1")
	isolateCompileCheckEditorLookup(t)
	projectRoot := createDispatcherUnityProject(t)
	writeDispatcherTestFile(t, filepath.Join(projectRoot, projectVersionFilePath),
		"m_EditorVersion: "+bogusCompileCheckEditorVersion+"\n")
	var stdout bytes.Buffer
	var stderr bytes.Buffer

	code := runCompileCheck(context.Background(), compileCheckOptions{projectPath: projectRoot}, projectRoot, &stdout, &stderr)

	if code != 1 {
		t.Fatalf("code = %d, want 1", code)
	}
	complete := assertCompileCheckVibeRun(t, readCompileCheckVibeLog(t, projectRoot))
	assertVibeContextJSON(t, complete, "outcome", `"failed"`)
}
