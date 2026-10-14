package fastcheck

import (
	"context"
	"encoding/json"
	"errors"
	"os"
	"os/exec"
	"path/filepath"
	"reflect"
	"strings"
	"testing"
	"time"
)

// newTestClient returns a client whose worker is the given stand-in, with its own temp directory.
func newTestClient(t *testing.T, run CommandRunner) Client {
	t.Helper()
	return Client{
		Worker:        WorkerPaths{Key: "key", DllPath: filepath.Join(t.TempDir(), workerDllFileName), DotnetHostPath: "dotnet"},
		TempDirectory: t.TempDir(),
		Run:           run,
	}
}

// assertNoRequestFilesLeft fails when a call left its request or response file behind.
func assertNoRequestFilesLeft(t *testing.T, directory string) {
	t.Helper()
	entries, err := os.ReadDir(directory)
	if err != nil {
		t.Fatalf("failed to list the temp directory: %v", err)
	}

	if len(entries) != 0 {
		t.Fatalf("expected the request and response files to be removed, found %d entries", len(entries))
	}
}

// Verifies that Check sends the request as the worker reads it and parses the worker's response.
func TestCheckParsesResponse(t *testing.T) {
	request := CheckRequest{
		ProjectRoot:          "/project",
		ResponseFilePath:     "/project/Sample.Game.fastcheck.rsp",
		ChangedFiles:         []string{"/project/Probe.cs"},
		BaselineFingerprints: map[string]string{"/project/Probe.cs": "abc"},
		GeneratedDirectory:   "/project/Sample.Game.generated",
	}
	want := CheckResponse{
		Outcome: CheckedOutcome,
		Diagnostics: []WorkerDiagnostic{
			{File: "/project/Probe.cs", Line: 7, Column: 25, Severity: "error", Code: "CS0029", Message: "message"},
		},
		UncheckedCodes:     []string{"CS0067"},
		GeneratorsNotRerun: true,
	}
	var sentArguments []string
	var sentRequest map[string]any
	client := newTestClient(t, func(_ context.Context, cmd *exec.Cmd) (string, string, int, error) {
		sentArguments = cmd.Args
		content, err := os.ReadFile(cmd.Args[len(cmd.Args)-2])
		if err != nil {
			return "", err.Error(), 1, err
		}

		if err := json.Unmarshal(content, &sentRequest); err != nil {
			return "", err.Error(), 1, err
		}

		// The worker's own spelling of the response, not the Go struct's encoding.
		response := `{"Outcome":"Checked","Reason":"","Diagnostics":[{"File":"/project/Probe.cs","Line":7,"Column":25,` +
			`"Severity":"error","Code":"CS0029","Message":"message"}],"UncheckedCodes":["CS0067"],"GeneratorsNotRerun":true}`
		return "", "", 0, os.WriteFile(cmd.Args[len(cmd.Args)-1], []byte(response), cacheFilePermissions)
	})

	got, err := client.Check(context.Background(), request)
	if err != nil {
		t.Fatalf("expected the check to succeed, got error: %v", err)
	}

	if !reflect.DeepEqual(got, want) {
		t.Fatalf("expected %+v, got %+v", want, got)
	}

	if sentArguments[1] != compilerExecArgument || sentArguments[2] != client.Worker.DllPath || sentArguments[3] != checkCommandName {
		t.Fatalf("expected dotnet exec <worker> check <in> <out>, got %v", sentArguments)
	}

	for _, field := range []string{"ProjectRoot", "ResponseFilePath", "ChangedFiles", "BaselineFingerprints", "GeneratedDirectory"} {
		if _, found := sentRequest[field]; !found {
			t.Fatalf("expected the request to carry %s, got %v", field, sentRequest)
		}
	}

	assertNoRequestFilesLeft(t, client.TempDirectory)
}

// Verifies that a worker that cannot start is an error carrying the start failure.
func TestWorkerStartFailureIsError(t *testing.T) {
	client := newTestClient(t, func(_ context.Context, _ *exec.Cmd) (string, string, int, error) {
		return "", "", 0, errors.New("dotnet not found")
	})

	_, err := client.Check(context.Background(), CheckRequest{})
	if err == nil || !strings.Contains(err.Error(), "dotnet not found") {
		t.Fatalf("expected the start failure in the error, got %v", err)
	}
}

// Verifies that a worker exiting with a non-zero code is an error carrying its standard error.
func TestNonZeroExitIsError(t *testing.T) {
	client := newTestClient(t, func(_ context.Context, _ *exec.Cmd) (string, string, int, error) {
		return "", "System.InvalidOperationException: broken", 3, &exec.ExitError{}
	})

	_, err := client.Fingerprint(context.Background(), FingerprintRequest{})
	if err == nil || !strings.Contains(err.Error(), "exit code 3") || !strings.Contains(err.Error(), "broken") {
		t.Fatalf("expected an error with the exit code and standard error, got %v", err)
	}

	assertNoRequestFilesLeft(t, client.TempDirectory)
}

// Verifies that a worker that does not finish within the timeout is an error.
func TestTimeoutIsError(t *testing.T) {
	client := newTestClient(t, func(ctx context.Context, _ *exec.Cmd) (string, string, int, error) {
		<-ctx.Done()
		return "", "", -1, ctx.Err()
	})
	client.Timeout = 20 * time.Millisecond

	_, err := client.Check(context.Background(), CheckRequest{})
	if err == nil || !strings.Contains(err.Error(), "did not finish") {
		t.Fatalf("expected a timeout error, got %v", err)
	}

	assertNoRequestFilesLeft(t, client.TempDirectory)
}

// Verifies that Fingerprint parses the worker's fingerprints and content hashes.
func TestFingerprintParsesResponse(t *testing.T) {
	client := newTestClient(t, func(_ context.Context, cmd *exec.Cmd) (string, string, int, error) {
		response := `{"Fingerprints":{"/project/A.cs":"f"},"ContentHashes":{"/project/A.cs":"h"}}`
		return "", "", 0, os.WriteFile(cmd.Args[len(cmd.Args)-1], []byte(response), cacheFilePermissions)
	})

	got, err := client.Fingerprint(context.Background(), FingerprintRequest{Sources: []string{"/project/A.cs"}})
	if err != nil {
		t.Fatalf("expected the fingerprint to succeed, got error: %v", err)
	}

	want := FingerprintResponse{
		Fingerprints:  map[string]string{"/project/A.cs": "f"},
		ContentHashes: map[string]string{"/project/A.cs": "h"},
	}
	if !reflect.DeepEqual(got, want) {
		t.Fatalf("expected %+v, got %+v", want, got)
	}
}

// Verifies that a worker that exits cleanly without writing a response is an error.
func TestMissingResponseIsError(t *testing.T) {
	client := newTestClient(t, func(_ context.Context, _ *exec.Cmd) (string, string, int, error) {
		return "", "", 0, nil
	})

	_, err := client.Check(context.Background(), CheckRequest{})
	if err == nil || !strings.Contains(err.Error(), "worker response") {
		t.Fatalf("expected a response read error, got %v", err)
	}

	assertNoRequestFilesLeft(t, client.TempDirectory)
}

// Verifies that a client without a built worker or a usable temp directory fails before running anything.
func TestClientWithoutWorkerOrTempDirectoryIsError(t *testing.T) {
	ran := false
	run := func(_ context.Context, _ *exec.Cmd) (string, string, int, error) {
		ran = true
		return "", "", 0, nil
	}

	withoutWorker := newTestClient(t, run)
	withoutWorker.Worker = WorkerPaths{}
	if _, err := withoutWorker.Check(context.Background(), CheckRequest{}); err == nil {
		t.Fatal("expected an error without a built worker")
	}

	withoutTempDirectory := newTestClient(t, run)
	withoutTempDirectory.TempDirectory = filepath.Join(t.TempDir(), "missing")
	if _, err := withoutTempDirectory.Check(context.Background(), CheckRequest{}); err == nil {
		t.Fatal("expected an error without a temp directory")
	}

	if ran {
		t.Fatal("expected the worker not to run")
	}
}

// Verifies that a response that is not JSON is an error.
func TestMalformedResponseIsError(t *testing.T) {
	client := newTestClient(t, func(_ context.Context, cmd *exec.Cmd) (string, string, int, error) {
		return "", "", 0, os.WriteFile(cmd.Args[len(cmd.Args)-1], []byte("not json"), cacheFilePermissions)
	})

	_, err := client.Check(context.Background(), CheckRequest{})
	if err == nil || !strings.Contains(err.Error(), "parse") {
		t.Fatalf("expected a response parse error, got %v", err)
	}

	assertNoRequestFilesLeft(t, client.TempDirectory)
}
