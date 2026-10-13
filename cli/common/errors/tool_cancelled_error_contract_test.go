package clierrors

import (
	"encoding/json"
	"os"
	"reflect"
	"testing"

	"github.com/hatayama/unity-cli-loop/common/unityipc"
)

const toolCancelledErrorContractPath = "tests/contracts/tool_cancelled_error_contract.json"

type toolCancelledErrorContractFile struct {
	Comment   string          `json:"comment"`
	ErrorData json.RawMessage `json:"errorData"`
}

// Verifies the shared tool_cancelled error.data fixture decodes into every typed struct field.
func TestDecodeToolCancelledErrorData_WhenContractFixture_ReadsEveryField(t *testing.T) {
	contract := readToolCancelledErrorContract(t)

	decoded := decodeToolCancelledErrorData(contract.ErrorData)

	expected := toolCancelledErrorData{
		Type:            "tool_cancelled",
		Message:         "'run-tests' was cancelled by uloop cancel.",
		RunningToolName: "run-tests",
	}
	if decoded != expected {
		t.Fatalf("decoded contract mismatch:\n got: %#v\nwant: %#v", decoded, expected)
	}
}

// Verifies a tool_cancelled RPC error classifies as TOOL_CANCELLED in the execution phase, is
// not retryable, names the tool, and points at uloop status.
func TestClassifyToolCancelledRPCError_UsesCancelEnvelope(t *testing.T) {
	contract := readToolCancelledErrorContract(t)
	err := &unityipc.RPCError{
		Code:    -32603,
		Message: "'run-tests' was cancelled by uloop cancel.",
		Data:    contract.ErrorData,
	}

	cliErr := ClassifyError(err, ErrorContext{ProjectRoot: "/project", Command: "run-tests"})

	if cliErr.ErrorCode != "TOOL_CANCELLED" {
		t.Fatalf("error code mismatch: %#v", cliErr)
	}
	if cliErr.Phase != ErrorPhaseExecution {
		t.Fatalf("phase mismatch: %#v", cliErr)
	}
	if cliErr.Retryable || cliErr.SafeToRetry {
		t.Fatalf("a cancel must not be retryable: %#v", cliErr)
	}
	if cliErr.Message != "'run-tests' was cancelled by uloop cancel." {
		t.Fatalf("message mismatch: %s", cliErr.Message)
	}
	expectedActions := []string{"Run `uloop status` to confirm the Editor is Ready before the next command."}
	if !reflect.DeepEqual(cliErr.NextActions, expectedActions) {
		t.Fatalf("next actions mismatch: %#v", cliErr.NextActions)
	}
	if cliErr.Command != "run-tests" || cliErr.ProjectRoot != "/project" {
		t.Fatalf("context mismatch: %#v", cliErr)
	}
}

func readToolCancelledErrorContract(t *testing.T) toolCancelledErrorContractFile {
	t.Helper()

	path := findRepoRelativeFile(t, toolCancelledErrorContractPath)
	data, err := os.ReadFile(path)
	if err != nil {
		t.Fatalf("failed to read tool_cancelled error contract: %v", err)
	}

	var contract toolCancelledErrorContractFile
	if err := json.Unmarshal(data, &contract); err != nil {
		t.Fatalf("failed to unmarshal tool_cancelled error contract: %v", err)
	}
	if len(contract.ErrorData) == 0 {
		t.Fatal("tool_cancelled error contract must include errorData")
	}
	return contract
}
