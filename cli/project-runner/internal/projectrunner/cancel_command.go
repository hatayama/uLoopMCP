package projectrunner

import (
	"context"
	"encoding/json"
	"io"
	"time"

	clierrors "github.com/hatayama/unity-cli-loop/common/errors"

	"github.com/hatayama/unity-cli-loop/common/clicontract"
	"github.com/hatayama/unity-cli-loop/common/clicore"
	"github.com/hatayama/unity-cli-loop/common/unityipc"
)

const cancelRunningToolBridgeCommandName = "cancel-running-tool"

// Long enough for an Editor that answers off its main thread, which cancel-running-tool always
// does, and short enough that a caller stuck behind BUSY learns quickly that cancel did not land.
const cancelCommandTimeout = 5 * time.Second

func runCancelCommand(
	ctx context.Context,
	connection unityipc.Connection,
	args []string,
	stdout io.Writer,
	stderr io.Writer,
) int {
	return runCancelCommandWithTimeout(ctx, connection, args, stdout, stderr, cancelCommandTimeout)
}

// runCancelCommandWithTimeout sends cancel-running-tool once and prints the Editor's answer.
// It exits 0 for Cancelled and NothingRunning and 1 for NotCancellable, following the answer's
// Success. It never retries: a second send could cancel a tool that started after the first.
func runCancelCommandWithTimeout(
	ctx context.Context,
	connection unityipc.Connection,
	args []string,
	stdout io.Writer,
	stderr io.Writer,
	timeout time.Duration,
) int {
	errorContext := clierrors.ErrorContext{ProjectRoot: connection.ProjectRoot, Command: clicore.CancelCommandName}
	if len(args) > 0 {
		clierrors.WriteClassifiedError(stderr, &clierrors.ArgumentError{
			Message:     "cancel takes no options: " + args[0],
			Option:      args[0],
			NextActions: []string{"Run `uloop cancel` with no options other than --project-path."},
		}, errorContext)
		return 1
	}

	result, err := sendCancelRunningTool(ctx, connection, timeout)
	if err != nil {
		clierrors.WriteClassifiedError(stderr, err, errorContext)
		return 1
	}
	clicore.WriteJSON(stdout, result)
	return toolEnvelopeExitCode(result)
}

func sendCancelRunningTool(
	ctx context.Context,
	connection unityipc.Connection,
	timeout time.Duration,
) (json.RawMessage, error) {
	sendCtx, cancel := context.WithTimeout(ctx, timeout)
	defer cancel()
	return unityipc.NewClient(connection, clicontract.ProjectRunnerVersion()).Send(
		sendCtx,
		cancelRunningToolBridgeCommandName,
		map[string]any{})
}
