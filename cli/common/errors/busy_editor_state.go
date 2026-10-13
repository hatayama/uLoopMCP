package clierrors

const unityServerBusyResponsivenessStallThresholdSeconds = 5.0

func unityServerBusyNextActions(data serverBusyErrorData) []string {
	if isWaitingForStalledMainThread(data) {
		return mainThreadWaitBusyNextActions(data.RunningToolName)
	}

	actions := []string{
		"Wait for the running Unity command to complete.",
		"Retry the command after Unity reports it is no longer busy.",
	}

	if optionalTrueBool(data.IsPaused) && optionalTrueBool(data.IsPlaying) {
		actions = append(pausedInPlayModeNextActions(data.RunningToolName), actions...)
	}

	if optionalTrueBool(data.IsCompiling) {
		actions = append(
			[]string{"Unity is compiling scripts; wait for compilation to finish before retrying."},
			actions...)
	} else if optionalTrueBool(data.IsUpdating) {
		actions = append(
			[]string{"Unity is importing assets; wait for the asset database update to finish before retrying."},
			actions...)
	}

	// Why only runningToolName == "compile": attach recovery requires a local pending
	// record from this client's own COMPILE_WAIT_TIMEOUT. Editor-state "unity-compile"
	// busy and other clients' compiles must not promise reattach.
	if data.RunningToolName == "compile" {
		actions = append(
			actions,
			"A compile can take several minutes on large projects. Wait for it to finish, then retry. If your own `uloop compile` previously failed with COMPILE_WAIT_TIMEOUT, re-running `uloop compile` reattaches to that compile instead of starting a new one.",
		)
	}

	if data.SecondsSinceLastMainThreadTick != nil &&
		*data.SecondsSinceLastMainThreadTick >= unityServerBusyResponsivenessStallThresholdSeconds {
		actions = append(
			actions,
			"Run a light command such as `uloop get-logs --max-count 1` to check whether Unity is still responsive before treating this as a freeze.")
	}

	return actions
}

// Why a paused Play Mode needs its own guidance: the default wait/retry pair is wrong
// advice there. A running command that waits for a frame or a physics step never reaches
// one while the Editor is paused, so it never completes and never releases the
// single-flight gate; the caller can wait forever. Why uloop cancel: while the gate is held,
// clear-pause-point and control-play-mode are themselves rejected as busy, and
// await-pause-point reaches Unity but only reports the hit. uloop cancel answers while the gate
// is held, cancels the running command, and releases the Editor pause.
func pausedInPlayModeNextActions(runningToolName string) []string {
	actions := []string{
		"Unity is paused in Play Mode. A running command that waits for a frame or a physics step cannot finish until play resumes, so this busy state does not clear on its own.",
		"Run `uloop pause-point-status` (it answers while Unity is busy) to see whether a pause-point hit is holding the pause.",
	}

	if ClientDisconnectCancelsRunningTool(runningToolName) {
		actions = append(
			actions,
			"Run `uloop cancel` (it answers while Unity is busy): the running command is cancelled and the Editor pause is released.")
	}

	return append(
		actions,
		"Release the pause in the Editor (Edit > Play Mode > Pause). Frames resume, so the running command finishes and returns its result.")
}

// ClientDisconnectCancelsRunningTool reports whether the running tool is one a client
// disconnect, and so uloop cancel, can stop. Why these two tool names are excluded: the Editor
// keeps a run-tests run that respects Enter Play Mode settings, and a compile that waits for a
// domain reload, alive across a client disconnect and uloop cancel alike. The busy payload
// carries only the tool name, not the options that decide it, so both names drop the step
// rather than promise a recovery that may not happen.
func ClientDisconnectCancelsRunningTool(runningToolName string) bool {
	return runningToolName != "run-tests" && runningToolName != "compile"
}

func unityServerBusyEditorActivitySummary(data serverBusyErrorData) map[string]any {
	summary := map[string]any{}
	copyOptionalTrueBool(summary, "isCompiling", data.IsCompiling)
	copyOptionalTrueBool(summary, "isUpdating", data.IsUpdating)
	copyOptionalTrueBool(summary, "isPlaying", data.IsPlaying)
	copyOptionalTrueBool(summary, "isPaused", data.IsPaused)
	if data.SecondsSinceLastMainThreadTick != nil {
		summary["secondsSinceLastMainThreadTick"] = *data.SecondsSinceLastMainThreadTick
	}
	if len(summary) == 0 {
		return nil
	}
	return summary
}

func optionalTrueBool(value *bool) bool {
	return value != nil && *value
}

func copyOptionalTrueBool(destination map[string]any, key string, value *bool) {
	if value == nil || !*value {
		return
	}
	destination[key] = true
}
