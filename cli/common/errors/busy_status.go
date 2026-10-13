package clierrors

import (
	"fmt"
)

func unityServerBusyMessage(fallback string, data serverBusyErrorData, requestedCommand string) string {
	runningToolName := data.RunningToolName
	requestedToolName := firstNonEmpty(data.RequestedToolName, requestedCommand)
	if runningToolName == "" || requestedToolName == "" {
		return fallback
	}
	if isWaitingForStalledMainThread(data) {
		return mainThreadWaitBusyMessage(requestedToolName, runningToolName, *data.SecondsSinceLastMainThreadTick)
	}
	// Both the retrying tool path and commands that send once without retrying reach this
	// message, so it must not claim that a retry already happened.
	if data.RunningToolElapsedSeconds != nil {
		return fmt.Sprintf(
			"'%s' was not executed because Unity is busy running '%s' (running for %ds). uloop is single-flight per Editor. Wait for '%s' to complete, then run the command once more; do not retry in a loop.%s",
			requestedToolName,
			runningToolName,
			*data.RunningToolElapsedSeconds,
			runningToolName,
			busyCancelHint(runningToolName))
	}
	return fmt.Sprintf(
		"'%s' was not executed because Unity is busy running '%s'. uloop is single-flight per Editor. Wait for '%s' to complete, then run the command once more; do not retry in a loop.",
		requestedToolName,
		runningToolName,
		runningToolName)
}

// busyCancelHint points at uloop cancel only for a busy that came from the execution slot, the
// only kind with elapsed seconds, so the caller already checked that. An Editor-state busy
// (unity-compile, unity-asset-database-update) holds no slot and nothing to cancel.
func busyCancelHint(runningToolName string) string {
	if !ClientDisconnectCancelsRunningTool(runningToolName) {
		return ""
	}
	return " If it does not finish, run `uloop cancel` to stop it."
}
