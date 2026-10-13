package clierrors

import (
	"encoding/json"
	"strings"
	"testing"

	"github.com/hatayama/unity-cli-loop/common/unityipc"
)

const busyCancelHintText = "Run `uloop cancel` to stop it."

func classifyBusyForCancelHint(t *testing.T, data string) CLIError {
	t.Helper()
	err := &unityipc.RPCError{
		Code:    -32603,
		Message: "Unity is busy.",
		Data:    json.RawMessage(data),
	}
	return ClassifyError(err, ErrorContext{ProjectRoot: "/project", Command: "get-logs"})
}

// Verifies a busy from the execution slot held by a tool uloop cancel can stop ends its message
// with the cancel hint.
func TestBusyMessage_WhenCancellableToolHoldsSlot_EndsWithCancelHint(t *testing.T) {
	cliErr := classifyBusyForCancelHint(t,
		`{"type":"server_busy","runningToolName":"execute-dynamic-code","requestedToolName":"get-logs","runningToolElapsedSeconds":160}`)

	expected := "'get-logs' was not executed because Unity is busy running 'execute-dynamic-code' (running for 160s). uloop is single-flight per Editor. Wait for 'execute-dynamic-code' to complete, then run the command once more; do not retry in a loop. " + busyCancelHintText
	if cliErr.Message != expected {
		t.Fatalf("message mismatch:\n got: %s\nwant: %s", cliErr.Message, expected)
	}
}

// Verifies a busy without elapsed seconds gets no cancel hint even for a tool uloop cancel could
// stop, because only the execution slot reports elapsed seconds and only it has something to cancel.
func TestBusyMessage_WhenElapsedSecondsMissing_OmitsCancelHint(t *testing.T) {
	cliErr := classifyBusyForCancelHint(t,
		`{"type":"server_busy","runningToolName":"execute-dynamic-code","requestedToolName":"get-logs"}`)

	if strings.Contains(cliErr.Message, busyCancelHintText) {
		t.Fatalf("busy without elapsed seconds must not suggest uloop cancel: %s", cliErr.Message)
	}
}

// Verifies a busy held by run-tests gets no cancel hint, because the payload cannot tell whether
// that run is one uloop cancel may stop.
func TestBusyMessage_WhenRunTestsHoldsSlot_OmitsCancelHint(t *testing.T) {
	cliErr := classifyBusyForCancelHint(t,
		`{"type":"server_busy","runningToolName":"run-tests","requestedToolName":"get-logs","runningToolElapsedSeconds":30}`)

	if !strings.Contains(cliErr.Message, "(running for 30s)") {
		t.Fatalf("message must be the elapsed wording: %s", cliErr.Message)
	}
	if strings.Contains(cliErr.Message, busyCancelHintText) {
		t.Fatalf("run-tests busy must not suggest uloop cancel: %s", cliErr.Message)
	}
}

// Verifies an Editor-state busy such as unity-compile, which holds no execution slot, gets no
// cancel hint.
func TestBusyMessage_WhenEditorStateIsCompiling_OmitsCancelHint(t *testing.T) {
	cliErr := classifyBusyForCancelHint(t,
		`{"type":"server_busy","runningToolName":"unity-compile","requestedToolName":"get-logs","isCompiling":true}`)

	if strings.Contains(cliErr.Message, busyCancelHintText) {
		t.Fatalf("Editor-state busy must not suggest uloop cancel: %s", cliErr.Message)
	}
}
