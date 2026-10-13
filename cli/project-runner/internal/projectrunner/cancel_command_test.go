package projectrunner

import (
	"bytes"
	"context"
	"strings"
	"testing"
	"time"

	"github.com/hatayama/unity-cli-loop/common/unityipc"
)

type cancelRun struct {
	code   int
	stdout string
	stderr string
}

func runCancelForTest(connection unityipc.Connection, args []string) cancelRun {
	var stdout bytes.Buffer
	var stderr bytes.Buffer
	code := runCancelCommandWithTimeout(context.Background(), connection, args, &stdout, &stderr, 2*time.Second)
	return cancelRun{code: code, stdout: stdout.String(), stderr: stderr.String()}
}

// Verifies each answer the Editor gives to cancel-running-tool is printed as it came and exits
// with the code its Success implies: Cancelled and NothingRunning exit 0, NotCancellable exits 1.
func TestRunCancelCommandPrintsOutcomeAndExitsBySuccess(t *testing.T) {
	cases := []struct {
		name     string
		result   string
		wantCode int
	}{
		{
			name:     "Cancelled",
			result:   `{"Outcome":"Cancelled","RunningToolName":"execute-dynamic-code","RunningToolElapsedSeconds":12,"Success":true}`,
			wantCode: 0,
		},
		{
			name:     "NothingRunning",
			result:   `{"Outcome":"NothingRunning","RunningToolName":null,"RunningToolElapsedSeconds":null,"Success":true}`,
			wantCode: 0,
		},
		{
			name:     "NotCancellable",
			result:   `{"Outcome":"NotCancellable","RunningToolName":"compile","RunningToolElapsedSeconds":3,"Success":false}`,
			wantCode: 1,
		},
	}
	for _, testCase := range cases {
		t.Run(testCase.name, func(t *testing.T) {
			projectRoot := writeFakeUnityProject(t)
			server := startFakeUnityResultServer(t, projectRoot, cancelRunningToolBridgeCommandName, testCase.result)

			run := runCancelForTest(server.connection, nil)

			if run.code != testCase.wantCode {
				t.Fatalf("exit code = %d, want %d; stdout=%s stderr=%s", run.code, testCase.wantCode, run.stdout, run.stderr)
			}
			if !strings.Contains(run.stdout, `"Outcome": "`+testCase.name+`"`) {
				t.Fatalf("stdout must carry the Editor's outcome:\n%s", run.stdout)
			}
			if run.stderr != "" {
				t.Fatalf("stderr must stay empty when the Editor answered: %s", run.stderr)
			}
			server.receivedRequest(t)
			assertServerDidNotFail(t, server.serverErr)
		})
	}
}

// Verifies a cancel that cannot reach the Editor prints a classified connection error on stderr
// and exits 1 without printing an answer.
func TestRunCancelCommandReportsUnreachableEditor(t *testing.T) {
	projectRoot := writeFakeUnityProject(t)

	run := runCancelForTest(unreachableConnection(projectRoot), nil)

	if run.code != 1 {
		t.Fatalf("exit code = %d, want 1", run.code)
	}
	if run.stdout != "" {
		t.Fatalf("stdout must stay empty on a send error: %s", run.stdout)
	}
	if !strings.Contains(run.stderr, `"ErrorCode": "UNITY_NOT_REACHABLE"`) || !strings.Contains(run.stderr, `"Command": "cancel"`) {
		t.Fatalf("stderr must be a classified connection error for cancel:\n%s", run.stderr)
	}
}

// Verifies an Editor whose package predates cancel-running-tool, which answers with its generic
// internal error, is reported through the ordinary RPC error classification and exits 1.
func TestRunCancelCommandReportsPackageWithoutCancelCommand(t *testing.T) {
	projectRoot := writeFakeUnityProject(t)
	server := startFakeUnityServer(t, projectRoot, cancelRunningToolBridgeCommandName,
		`{"jsonrpc":"2.0","id":1,"error":{"code":-32603,"message":"Internal error","data":{"type":"internal_error","message":"Unknown tool: cancel-running-tool"}}}`)

	run := runCancelForTest(server.connection, nil)

	if run.code != 1 {
		t.Fatalf("exit code = %d, want 1", run.code)
	}
	if run.stdout != "" {
		t.Fatalf("stdout must stay empty on an RPC error: %s", run.stdout)
	}
	if !strings.Contains(run.stderr, `"ErrorCode": "UNITY_RPC_ERROR"`) ||
		!strings.Contains(run.stderr, "Unknown tool: cancel-running-tool") {
		t.Fatalf("stderr must be the classified RPC error naming the unknown command:\n%s", run.stderr)
	}
	server.receivedRequest(t)
}
