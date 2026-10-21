package projectrunner

import (
	"errors"
	"fmt"
	"testing"

	"github.com/hatayama/unity-cli-loop/common/clicore"
	clierrors "github.com/hatayama/unity-cli-loop/common/errors"
)

func nestedWaitTriggerError(command string, triggerCommand string) *clierrors.ArgumentError {
	return &clierrors.ArgumentError{
		Message: fmt.Sprintf(
			"--trigger cannot target %q: waiting for a pause point from inside another pause-point wait cannot make progress.",
			triggerCommand),
		Option:  "--trigger",
		Command: command,
		NextActions: []string{
			"Pass a command that performs an action (for example simulate-keyboard), not another pause-point wait.",
		},
	}
}

func assertArgumentErrorEqual(t *testing.T, err error, want *clierrors.ArgumentError) {
	t.Helper()
	var got *clierrors.ArgumentError
	if !errors.As(err, &got) {
		t.Fatalf("error = %v (%T), want *ArgumentError", err, err)
	}
	if got.Message != want.Message || got.Option != want.Option || got.Command != want.Command {
		t.Fatalf("error = %+v, want %+v", got, want)
	}
	if len(got.NextActions) != len(want.NextActions) {
		t.Fatalf("NextActions = %#v, want %#v", got.NextActions, want.NextActions)
	}
	for index, action := range want.NextActions {
		if got.NextActions[index] != action {
			t.Fatalf("NextActions[%d] = %q, want %q", index, got.NextActions[index], action)
		}
	}
}

// Verifies await-pause-point and enable-pause-point reject a nested pause-point wait as --trigger
// with the exact message, option, command, and next action, for both nested targets.
func TestParsePausePointTriggerCommand_NestedWaitMessageIsPinned(t *testing.T) {
	for _, command := range []string{clicore.PausePointAwaitCommandName, pausePointEnableCommandName} {
		for _, triggerCommand := range []string{clicore.PausePointAwaitCommandName, pausePointEnableCommandName} {
			_, _, err := parsePausePointTriggerCommand(command, triggerCommand+" --id other")

			assertArgumentErrorEqual(t, err, nestedWaitTriggerError(command, triggerCommand))
		}
	}
}

// Verifies enable-pause-point's --await --trigger path rejects a nested pause-point wait with the
// same pinned error before anything is sent to Unity.
func TestEnablePausePoint_RejectsNestedWaitTrigger(t *testing.T) {
	for _, triggerCommand := range []string{clicore.PausePointAwaitCommandName, pausePointEnableCommandName} {
		args := []string{"--file", "Assets/A.cs", "--line", "3", "--await", "--trigger", triggerCommand + " --id x"}

		_, _, _, _, _, _, _, _, err := extractPausePointEnableAwaitFlags(args)

		assertArgumentErrorEqual(t, err, nestedWaitTriggerError(pausePointEnableCommandName, triggerCommand))
	}
}

// Verifies a leading "uloop" before a nested pause-point wait is not offered back as a usable
// correction under the pause-point rules, so the next action falls back to the generic example.
func TestParsePausePointTriggerCommand_LeadingUloopNestedWaitFallsBackToExample(t *testing.T) {
	_, _, err := parsePausePointTriggerCommand(clicore.PausePointAwaitCommandName, "uloop enable-pause-point --id x")

	assertArgumentErrorEqual(t, err, &clierrors.ArgumentError{
		Message: pausePointTriggerLeadingUloopMessage,
		Option:  "--trigger",
		Command: clicore.PausePointAwaitCommandName,
		NextActions: []string{
			`Re-run with --trigger "simulate-keyboard --action Press --key Space"`,
		},
	})
}
