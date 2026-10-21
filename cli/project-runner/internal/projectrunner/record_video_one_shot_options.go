package projectrunner

import (
	"fmt"
	"strconv"
	"strings"

	clierrors "github.com/hatayama/unity-cli-loop/common/errors"

	"github.com/hatayama/unity-cli-loop/common/clicore"
	"github.com/hatayama/unity-cli-loop/common/tooldocs"
)

const (
	recordVideoOneShotMinSeconds         = 1
	recordVideoOneShotMaxSeconds         = 600
	recordVideoTriggerDefaultSeconds     = 3
	recordVideoDefaultMaxDurationSeconds = 60
	recordVideoActionFlagName            = "action"
	recordVideoMaxDurationFlagName       = "max-duration-seconds"
	recordVideoStartAction               = "start"
	pausePointModeFlagName               = "mode"
	pausePointTraceMode                  = "trace"
	pausePointEnableAwaitFlag            = "--" + tooldocs.PausePointEnableAwaitFlagName
)

// recordVideoOneShotOptions is what the one-shot needs from the CLI-only flags. oneShot is false
// when neither --seconds nor --trigger was given, and the command then passes through untouched.
type recordVideoOneShotOptions struct {
	oneShot        bool
	seconds        int
	hasTrigger     bool
	triggerCommand string
	triggerArgs    []string
}

// parseRecordVideoOneShotOptions pulls --seconds and --trigger out of record-video's arguments and
// returns the rest for Unity. Without either flag it checks nothing, so a plain record-video call
// keeps reporting its own argument errors exactly as before.
func parseRecordVideoOneShotOptions(args []string) (recordVideoOneShotOptions, []string, error) {
	rest, secondsValue, triggerValue, err := extractRecordVideoOneShotFlags(args)
	if err != nil {
		return recordVideoOneShotOptions{}, nil, err
	}
	if secondsValue == "" && triggerValue == "" {
		return recordVideoOneShotOptions{}, args, nil
	}

	options := recordVideoOneShotOptions{oneShot: true, seconds: recordVideoTriggerDefaultSeconds}
	if secondsValue != "" {
		seconds, err := parseRecordVideoOneShotSeconds(secondsValue)
		if err != nil {
			return recordVideoOneShotOptions{}, nil, err
		}
		options.seconds = seconds
	}
	if triggerValue != "" {
		triggerCommand, triggerArgs, err := parseRecordVideoTriggerCommand(triggerValue)
		if err != nil {
			return recordVideoOneShotOptions{}, nil, err
		}
		options.hasTrigger = true
		options.triggerCommand = triggerCommand
		options.triggerArgs = triggerArgs
	}
	if err := validateRecordVideoOneShotAgainstRest(options.seconds, rest); err != nil {
		return recordVideoOneShotOptions{}, nil, err
	}
	return options, rest, nil
}

func extractRecordVideoOneShotFlags(args []string) ([]string, string, string, error) {
	rest := make([]string, 0, len(args))
	secondsValue := ""
	triggerValue := ""
	for index := 0; index < len(args); index++ {
		arg := args[index]
		if !isPausePointFlag(arg, tooldocs.RecordVideoSecondsFlagName) &&
			!isPausePointFlag(arg, tooldocs.RecordVideoTriggerFlagName) {
			rest = append(rest, arg)
			continue
		}
		name, value, consumedNext, err := clicore.ParseFlagValue(arg, args, index)
		if err != nil {
			return nil, "", "", err
		}
		if name == tooldocs.RecordVideoSecondsFlagName {
			secondsValue = value
		} else {
			triggerValue = value
		}
		if consumedNext {
			index++
		}
	}
	return rest, secondsValue, triggerValue, nil
}

func parseRecordVideoOneShotSeconds(value string) (int, error) {
	seconds, err := strconv.Atoi(value)
	if err != nil || seconds < recordVideoOneShotMinSeconds || seconds > recordVideoOneShotMaxSeconds {
		return 0, recordVideoArgumentError(
			"--"+tooldocs.RecordVideoSecondsFlagName,
			fmt.Sprintf("--seconds must be an integer from %d to %d, got %q.",
				recordVideoOneShotMinSeconds, recordVideoOneShotMaxSeconds, value),
			"Pass whole seconds, for example --seconds 3.",
		)
	}
	return seconds, nil
}

// validateRecordVideoOneShotAgainstRest checks the one-shot against the options Unity will see:
// only a start can be a one-shot, and the recording's own safety limit must outlast the wait.
func validateRecordVideoOneShotAgainstRest(seconds int, rest []string) error {
	action, maxDurationValue, err := findRecordVideoActionAndMaxDuration(rest)
	if err != nil {
		return err
	}
	if action != "" && !strings.EqualFold(action, recordVideoStartAction) {
		return recordVideoArgumentError(
			"--"+recordVideoActionFlagName,
			"--seconds/--trigger apply only to --action start.",
			"Drop --action, or run stop/status/mark without --seconds and --trigger.",
		)
	}

	maxDuration := recordVideoDefaultMaxDurationSeconds
	if maxDurationValue != "" {
		maxDuration, err = strconv.Atoi(maxDurationValue)
		if err != nil {
			return clierrors.InvalidValueArgumentError("--"+recordVideoMaxDurationFlagName, maxDurationValue, "integer")
		}
	}
	if maxDuration < seconds {
		return recordVideoArgumentError(
			"--"+tooldocs.RecordVideoSecondsFlagName,
			fmt.Sprintf("--seconds %d is longer than --max-duration-seconds %d; the recording would stop before the one-shot does.",
				seconds, maxDuration),
			"Raise --max-duration-seconds to at least --seconds.",
		)
	}
	return nil
}

func findRecordVideoActionAndMaxDuration(rest []string) (string, string, error) {
	action := ""
	maxDuration := ""
	for index := 0; index < len(rest); index++ {
		arg := rest[index]
		if !isPausePointFlag(arg, recordVideoActionFlagName) && !isPausePointFlag(arg, recordVideoMaxDurationFlagName) {
			continue
		}
		name, value, consumedNext, err := clicore.ParseFlagValue(arg, rest, index)
		if err != nil {
			return "", "", err
		}
		if name == recordVideoActionFlagName {
			action = value
		} else {
			maxDuration = value
		}
		if consumedNext {
			index++
		}
	}
	return action, maxDuration, nil
}

// parseRecordVideoTriggerCommand splits a one-shot --trigger value with the checks every trigger
// shares, then applies record-video's own rules.
func parseRecordVideoTriggerCommand(value string) (string, []string, error) {
	return parseTriggerCommandValue(tooldocs.RecordVideoCommandName, value, validateRecordVideoTrigger)
}

// validateRecordVideoTrigger rejects triggers that cannot produce anything worth recording. Unlike
// pause-point, a pause-point wait is a legitimate trigger here: the recording runs on Editor
// updates, independent of the wait.
func validateRecordVideoTrigger(command string, triggerCommand string, triggerArgs []string) error {
	if triggerCommand == tooldocs.RecordVideoCommandName {
		return recordVideoArgumentError(
			"--"+tooldocs.RecordVideoTriggerFlagName,
			"--trigger cannot run record-video inside a one-shot recording.",
			"Pass a command that performs an action, for example simulate-keyboard.",
		)
	}
	if triggerCommand != pausePointEnableCommandName {
		return nil
	}
	if !containsToken(triggerArgs, pausePointEnableAwaitFlag) {
		return recordVideoArgumentError(
			"--"+tooldocs.RecordVideoTriggerFlagName,
			"--trigger with enable-pause-point must include --await; without it the command returns before anything happens.",
			"Add --await to the enable-pause-point trigger.",
		)
	}
	if !strings.EqualFold(findTokenFlagValue(triggerArgs, pausePointModeFlagName), pausePointTraceMode) {
		return recordVideoArgumentError(
			"--"+tooldocs.RecordVideoTriggerFlagName,
			"--trigger with enable-pause-point --await must use --mode trace; other modes pause Play Mode and the recording keeps showing the same frame. Add --mode trace.",
			"Add --mode trace to the enable-pause-point trigger.",
		)
	}
	return nil
}

func containsToken(tokens []string, want string) bool {
	for _, token := range tokens {
		if token == want {
			return true
		}
	}
	return false
}

// findTokenFlagValue reads `--name value` or `--name=value` from trigger tokens, or "" when absent.
func findTokenFlagValue(tokens []string, name string) string {
	flag := "--" + name
	for index, token := range tokens {
		if strings.HasPrefix(token, flag+"=") {
			return strings.TrimPrefix(token, flag+"=")
		}
		if token == flag && index+1 < len(tokens) {
			return tokens[index+1]
		}
	}
	return ""
}

func recordVideoArgumentError(option string, message string, nextAction string) *clierrors.ArgumentError {
	return &clierrors.ArgumentError{
		Message:     message,
		Option:      option,
		Command:     tooldocs.RecordVideoCommandName,
		NextActions: []string{nextAction},
	}
}
