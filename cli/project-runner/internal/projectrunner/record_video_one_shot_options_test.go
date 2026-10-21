package projectrunner

import (
	"errors"
	"strings"
	"testing"

	clierrors "github.com/hatayama/unity-cli-loop/common/errors"
)

func requireRecordVideoOneShotParseError(t *testing.T, args []string, wantText string) {
	t.Helper()
	_, _, err := parseRecordVideoOneShotOptions(args)
	if err == nil {
		t.Fatalf("args %q: expected an error containing %q", args, wantText)
	}
	var argumentError *clierrors.ArgumentError
	if !errors.As(err, &argumentError) {
		t.Fatalf("args %q: error = %v (%T), want *ArgumentError", args, err, err)
	}
	if !strings.Contains(err.Error(), wantText) {
		t.Fatalf("args %q: error = %q, want it to contain %q", args, err.Error(), wantText)
	}
}

func requireRecordVideoOneShotParsed(t *testing.T, args []string) (recordVideoOneShotOptions, []string) {
	t.Helper()
	options, rest, err := parseRecordVideoOneShotOptions(args)
	if err != nil {
		t.Fatalf("args %q: unexpected error: %v", args, err)
	}
	return options, rest
}

// Verifies record-video without --seconds or --trigger is not a one-shot and keeps every argument,
// including invalid values that Unity's own parameter checks must still report.
func TestParseRecordVideoOneShot_WithoutFlags_ReturnsArgsUntouched(t *testing.T) {
	args := []string{"--action", "bogus", "--max-duration-seconds", "abc"}

	options, rest := requireRecordVideoOneShotParsed(t, args)

	if options.oneShot {
		t.Fatal("oneShot = true without --seconds or --trigger")
	}
	if strings.Join(rest, " ") != strings.Join(args, " ") {
		t.Fatalf("rest = %q, want %q", rest, args)
	}
}

// Verifies --seconds must be a whole number from 1 to 600.
func TestParseRecordVideoOneShot_RejectsInvalidSeconds(t *testing.T) {
	for _, value := range []string{"abc", "0", "601", "1.5"} {
		requireRecordVideoOneShotParseError(t, []string{"--seconds", value}, "--seconds must be an integer from 1 to 600")
	}
}

// Verifies --seconds in both flag forms is pulled out of the arguments Unity receives.
func TestParseRecordVideoOneShot_ExtractsSecondsInBothForms(t *testing.T) {
	for _, args := range [][]string{{"--seconds", "5", "--quality", "low"}, {"--seconds=5", "--quality", "low"}} {
		options, rest := requireRecordVideoOneShotParsed(t, args)

		if !options.oneShot || options.seconds != 5 || options.hasTrigger {
			t.Fatalf("args %q: options = %+v", args, options)
		}
		if strings.Join(rest, " ") != "--quality low" {
			t.Fatalf("args %q: rest = %q", args, rest)
		}
	}
}

// Verifies a one-shot longer than the recording's safety limit is rejected, against the explicit
// limit and against the default of 60 seconds.
func TestParseRecordVideoOneShot_RejectsSecondsBeyondMaxDuration(t *testing.T) {
	requireRecordVideoOneShotParseError(t, []string{"--seconds", "10", "--max-duration-seconds", "5"}, "longer than --max-duration-seconds 5")
	requireRecordVideoOneShotParseError(t, []string{"--seconds", "61"}, "longer than --max-duration-seconds 60")
}

// Verifies a seconds value equal to the limit is accepted.
func TestParseRecordVideoOneShot_AcceptsSecondsEqualToMaxDuration(t *testing.T) {
	options, _ := requireRecordVideoOneShotParsed(t, []string{"--seconds", "5", "--max-duration-seconds=5"})

	if options.seconds != 5 {
		t.Fatalf("seconds = %d, want 5", options.seconds)
	}
}

// Verifies a one-shot with a --max-duration-seconds that is not an integer is rejected before Unity.
func TestParseRecordVideoOneShot_RejectsNonIntegerMaxDuration(t *testing.T) {
	requireRecordVideoOneShotParseError(t, []string{"--seconds", "3", "--max-duration-seconds", "abc"}, "--max-duration-seconds")
}

// Verifies the one-shot flags are rejected with any action other than start, including the = form
// and a differently cased action name.
func TestParseRecordVideoOneShot_RejectsOneShotWithNonStartAction(t *testing.T) {
	for _, actionArgs := range [][]string{
		{"--action", "stop"},
		{"--action", "status"},
		{"--action", "mark"},
		{"--action=stop"},
		{"--action", "Stop"},
	} {
		args := append([]string{"--seconds", "3"}, actionArgs...)
		requireRecordVideoOneShotParseError(t, args, "apply only to --action start")
	}
}

// Verifies an explicit start action, in any case, is accepted for a one-shot.
func TestParseRecordVideoOneShot_AcceptsExplicitStartAction(t *testing.T) {
	for _, args := range [][]string{{"--seconds", "3", "--action", "start"}, {"--seconds", "3", "--action=Start"}} {
		options, _ := requireRecordVideoOneShotParsed(t, args)
		if !options.oneShot {
			t.Fatalf("args %q: oneShot = false", args)
		}
	}
}

// Verifies --trigger alone implies a three-second wait.
func TestParseRecordVideoOneShot_TriggerImpliesThreeSeconds(t *testing.T) {
	options, rest := requireRecordVideoOneShotParsed(t, []string{"--trigger", "simulate-keyboard --action Press --key C"})

	if !options.oneShot || options.seconds != 3 || !options.hasTrigger {
		t.Fatalf("options = %+v", options)
	}
	if options.triggerCommand != "simulate-keyboard" || strings.Join(options.triggerArgs, " ") != "--action Press --key C" {
		t.Fatalf("trigger = %q %q", options.triggerCommand, options.triggerArgs)
	}
	if len(rest) != 0 {
		t.Fatalf("rest = %q, want empty", rest)
	}
}

// Verifies the shared trigger check rejects --project-path inside a one-shot trigger.
func TestParseRecordVideoOneShot_RejectsProjectPathInTrigger(t *testing.T) {
	requireRecordVideoOneShotParseError(t,
		[]string{"--trigger", "simulate-keyboard --key C --project-path /other"}, "cannot include --project-path")
}

// Verifies a leading "uloop" is rejected and its correction is judged by record-video's rules: an
// enable-pause-point trace await is offered back, while a pausing one falls back to the example.
func TestParseRecordVideoOneShot_LeadingUloopCorrectionUsesRecordVideoRules(t *testing.T) {
	_, _, err := parseRecordVideoOneShotOptions(
		[]string{"--trigger", "uloop enable-pause-point --file A.cs --line 3 --mode trace --await"})
	assertArgumentErrorEqual(t, err, &clierrors.ArgumentError{
		Message:     pausePointTriggerLeadingUloopMessage,
		Option:      "--trigger",
		Command:     "record-video",
		NextActions: []string{`Re-run with --trigger "enable-pause-point --file A.cs --line 3 --mode trace --await"`},
	})

	_, _, err = parseRecordVideoOneShotOptions(
		[]string{"--trigger", "uloop enable-pause-point --file A.cs --line 3 --await"})
	assertArgumentErrorEqual(t, err, &clierrors.ArgumentError{
		Message:     pausePointTriggerLeadingUloopMessage,
		Option:      "--trigger",
		Command:     "record-video",
		NextActions: []string{`Re-run with --trigger "simulate-keyboard --action Press --key Space"`},
	})
}

// Verifies record-video cannot be its own trigger.
func TestParseRecordVideoOneShot_RejectsNestedRecordVideoTrigger(t *testing.T) {
	requireRecordVideoOneShotParseError(t,
		[]string{"--trigger", "record-video --action stop"}, "--trigger cannot run record-video inside a one-shot recording.")
}

// Verifies an enable-pause-point --await trigger whose mode would pause Play Mode is rejected:
// no mode (single-shot by default), single-shot, and continuous.
func TestParseRecordVideoOneShot_RejectsPausingAwaitTrigger(t *testing.T) {
	for _, trigger := range []string{
		"enable-pause-point --file A.cs --line 3 --await",
		"enable-pause-point --file A.cs --line 3 --mode single-shot --await",
		"enable-pause-point --file A.cs --line 3 --mode continuous --await",
	} {
		requireRecordVideoOneShotParseError(t, []string{"--trigger", trigger}, "Add --mode trace.")
	}
}

// Verifies the trace mode is accepted in the = form and in any case, as the CLI and Unity accept it.
func TestParseRecordVideoOneShot_AcceptsTraceModeInAnyForm(t *testing.T) {
	for _, trigger := range []string{
		"enable-pause-point --file A.cs --line 3 --mode=trace --await",
		"enable-pause-point --file A.cs --line 3 --mode Trace --await",
	} {
		options, _ := requireRecordVideoOneShotParsed(t, []string{"--trigger", trigger})
		if options.triggerCommand != pausePointEnableCommandName {
			t.Fatalf("trigger %q: command = %q", trigger, options.triggerCommand)
		}
	}
}

// Verifies an enable-pause-point trigger without --await is rejected: it would return at once and
// the mark would land on the moment of enabling, not of the hit.
func TestParseRecordVideoOneShot_RejectsEnableWithoutAwait(t *testing.T) {
	requireRecordVideoOneShotParseError(t,
		[]string{"--trigger", "enable-pause-point --file A.cs --line 3 --mode trace"}, "must include --await")
}

// Verifies a pause-point wait is an accepted one-shot trigger, unlike inside a pause-point wait.
func TestParseRecordVideoOneShot_AcceptsAwaitPausePointTrigger(t *testing.T) {
	options, _ := requireRecordVideoOneShotParsed(t, []string{"--trigger", "await-pause-point --id marker"})

	if options.triggerCommand != "await-pause-point" {
		t.Fatalf("command = %q", options.triggerCommand)
	}
}

// Verifies a nested trigger written with outer single and inner double quotes, as it arrives after
// the shell, keeps the inner command as one argument.
func TestParseRecordVideoOneShot_NestedTriggerKeepsInnerQuotes(t *testing.T) {
	trigger := `enable-pause-point --file A.cs --line 3 --mode trace --await --trigger "simulate-keyboard --action Press --key C"`

	options, _ := requireRecordVideoOneShotParsed(t, []string{"--trigger", trigger})

	args := options.triggerArgs
	if args[len(args)-2] != "--trigger" || args[len(args)-1] != "simulate-keyboard --action Press --key C" {
		t.Fatalf("trigger args = %q", args)
	}
}
