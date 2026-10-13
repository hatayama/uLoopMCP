package clierrors

import "github.com/hatayama/unity-cli-loop/common/unityipc"

// toolCancelledError reports to the CLI that waited for a tool that uloop cancel stopped it.
// Why not retryable: the cancel was asked for on purpose, so running the command again is a new
// decision for the caller, not a recovery.
func toolCancelledError(
	rpcErr *unityipc.RPCError,
	details map[string]any,
	data toolCancelledErrorData,
	context ErrorContext,
) CLIError {
	message := rpcErr.Message
	if data.RunningToolName != "" {
		message = "'" + data.RunningToolName + "' was cancelled by uloop cancel."
	}

	return CLIError{
		ErrorCode:   errorCodeToolCancelled,
		Phase:       ErrorPhaseExecution,
		Message:     message,
		Retryable:   false,
		SafeToRetry: false,
		ProjectRoot: context.ProjectRoot,
		Command:     context.Command,
		NextActions: []string{
			"Run `uloop status` to confirm the Editor is Ready before the next command.",
		},
		Details: details,
	}
}
