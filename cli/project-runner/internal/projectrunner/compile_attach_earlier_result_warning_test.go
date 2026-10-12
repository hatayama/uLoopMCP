package projectrunner

import (
	"bytes"
	"context"
	"encoding/json"
	"strings"
	"testing"
	"time"

	"github.com/hatayama/unity-cli-loop/common/unityipc"
)

// Why a literal and not the production constant: rewording the constant must fail these tests
// instead of silently changing what they expect.
const expectedEarlierCompileResultWarning = "This result comes from the earlier compile that timed out, so it does not include script changes saved after that compile started. If you changed scripts since then, run `uloop compile` again to compile the current scripts."

// Verifies that the note is put at the front of the result's Warning, and that a result it cannot
// safely rewrite is returned as is.
func TestAddEarlierCompileResultWarning(t *testing.T) {
	rewritten := []struct {
		name            string
		input           string
		expectedWarning string
		rawSubstrings   []string
	}{
		{name: "Warning absent", input: `{"Success":true,"ErrorCount":0}`, expectedWarning: expectedEarlierCompileResultWarning},
		{name: "Warning null", input: `{"Success":true,"Warning":null}`, expectedWarning: expectedEarlierCompileResultWarning},
		{name: "Warning empty", input: `{"Success":true,"Warning":""}`, expectedWarning: expectedEarlierCompileResultWarning},
		{
			name:            "Warning already set",
			input:           `{"Success":true,"Warning":"Play Mode was stopped."}`,
			expectedWarning: expectedEarlierCompileResultWarning + " Play Mode was stopped.",
		},
		{
			name:            "message with HTML-sensitive characters",
			input:           `{"Success":false,"Errors":[{"Message":"Cannot convert 'List<int>' & 'T>'"}]}`,
			expectedWarning: expectedEarlierCompileResultWarning,
			rawSubstrings:   []string{"List<int>", "& 'T>'"},
		},
	}
	for _, testCase := range rewritten {
		t.Run(testCase.name, func(t *testing.T) {
			output := addEarlierCompileResultWarning(json.RawMessage(testCase.input))

			assertResultWarning(t, output, testCase.expectedWarning)
			assertOtherFieldsKept(t, json.RawMessage(testCase.input), output)
			// Why the raw bytes: a decoded comparison would pass even if < > & were escaped as \u003c.
			for _, expected := range testCase.rawSubstrings {
				if !strings.Contains(string(output), expected) {
					t.Fatalf("expected %q in the raw output, got: %s", expected, output)
				}
			}
		})
	}

	unchanged := []struct {
		name  string
		input string
	}{
		{name: "not JSON", input: `not json`},
		{name: "JSON that is not an object", input: `[1,2]`},
		{name: "Warning that is not a string", input: `{"Success":true,"Warning":5}`},
	}
	for _, testCase := range unchanged {
		t.Run(testCase.name, func(t *testing.T) {
			output := addEarlierCompileResultWarning(json.RawMessage(testCase.input))

			if string(output) != testCase.input {
				t.Fatalf("expected the result unchanged, got: %s", output)
			}
		})
	}
}

// Verifies that a re-run returning the timed-out compile's stored result warns that it predates
// later script edits.
func TestRunCompileAttachStoredResultWarnsItPredatesLaterEdits(t *testing.T) {
	projectRoot := t.TempDir()
	writeEarlierResultWarningPendingRecord(t, projectRoot, "compile_attach_stored_warning")
	deps := compileWaitTestDeps(func(context.Context, unityipc.Connection, string) (compileStatusResponse, error) {
		return compileStatusResponse{
			Ready:     true,
			HasResult: true,
			Result:    json.RawMessage(`{"Success":true,"ErrorCount":0}`),
		}, nil
	})

	stdout, code := runEarlierResultWarningCompile(t, projectRoot, map[string]any{}, deps)

	if code != 0 {
		t.Fatalf("exit code = %d, want 0", code)
	}
	assertResultWarning(t, stdout, expectedEarlierCompileResultWarning)
}

// Verifies that a re-run that waits for the timed-out compile and returns its result warns that it
// predates later script edits, and that a failed result still fails the command.
func TestRunCompileAttachWaitedResultWarnsItPredatesLaterEdits(t *testing.T) {
	projectRoot := t.TempDir()
	writeEarlierResultWarningPendingRecord(t, projectRoot, "compile_attach_waited_warning")
	callCount := 0
	deps := compileWaitTestDeps(func(context.Context, unityipc.Connection, string) (compileStatusResponse, error) {
		callCount++
		if callCount == 1 {
			return compileStatusResponse{Ready: false, IsCompiling: true}, nil
		}
		return compileStatusResponse{
			Ready:     true,
			HasResult: true,
			Result:    json.RawMessage(`{"Success":false,"ErrorCount":2}`),
		}, nil
	})

	stdout, code := runEarlierResultWarningCompile(t, projectRoot, map[string]any{}, deps)

	if code != 1 {
		t.Fatalf("exit code = %d, want 1", code)
	}
	assertResultWarning(t, stdout, expectedEarlierCompileResultWarning)
}

func writeEarlierResultWarningPendingRecord(t *testing.T, projectRoot string, requestID string) {
	t.Helper()
	if err := writeCompilePendingRecord(projectRoot, compilePendingRecord{
		RequestID:     requestID,
		TimedOutAtUtc: time.Now().UTC().Add(-time.Minute),
	}); err != nil {
		t.Fatalf("write pending record failed: %v", err)
	}
}

func runEarlierResultWarningCompile(
	t *testing.T,
	projectRoot string,
	params map[string]any,
	deps compileWaitDeps,
) ([]byte, int) {
	t.Helper()
	connection := unityipc.Connection{
		Endpoint:    unityipc.Endpoint{Network: "tcp", Address: "127.0.0.1:1"},
		ProjectRoot: projectRoot,
	}
	var stdout, stderr bytes.Buffer

	code := runCompileWithDomainReloadWaitWithDeps(context.Background(), connection, params, &stdout, &stderr, deps)

	return stdout.Bytes(), code
}

func assertResultWarning(t *testing.T, result []byte, expectedWarning string) {
	t.Helper()
	fields := map[string]json.RawMessage{}
	if err := json.Unmarshal(bytes.TrimSpace(result), &fields); err != nil {
		t.Fatalf("result is not a JSON object: %v\n%s", err, result)
	}
	warning := ""
	if err := json.Unmarshal(fields["Warning"], &warning); err != nil {
		t.Fatalf("Warning is not a string: %v\n%s", err, result)
	}
	if warning != expectedWarning {
		t.Fatalf("Warning = %q\nwant %q", warning, expectedWarning)
	}
}

func assertOtherFieldsKept(t *testing.T, input json.RawMessage, output json.RawMessage) {
	t.Helper()
	inputFields := map[string]any{}
	if err := json.Unmarshal(input, &inputFields); err != nil {
		t.Fatalf("test input is not a JSON object: %v", err)
	}
	outputFields := map[string]any{}
	if err := json.Unmarshal(output, &outputFields); err != nil {
		t.Fatalf("result is not a JSON object: %v\n%s", err, output)
	}
	for key, inputValue := range inputFields {
		if key == "Warning" {
			continue
		}
		inputJSON, _ := json.Marshal(inputValue)
		outputJSON, _ := json.Marshal(outputFields[key])
		if !bytes.Equal(inputJSON, outputJSON) {
			t.Fatalf("field %s = %s, want %s", key, outputJSON, inputJSON)
		}
	}
}
