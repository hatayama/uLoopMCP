package projectrunner

import (
	"math"
	"strconv"

	clierrors "github.com/hatayama/unity-cli-loop/common/errors"

	"github.com/hatayama/unity-cli-loop/common/clicore"
	"github.com/hatayama/unity-cli-loop/common/tooldocs"
)

// recordVideoExtractOptions is a request to cut frames out of a finished video. requested is false
// when none of the extraction flags was given, and record-video then runs as before.
type recordVideoExtractOptions struct {
	requested bool
	video     string
	hasFrame  bool
	frameAt   float64
	sheetFrom float64
	sheetTo   float64
}

// recordVideoExtractRawValues holds each extraction flag's value as written; present tells an
// absent flag from one given an empty value.
type recordVideoExtractRawValues struct {
	values  map[string]string
	present map[string]bool
	others  []string
}

var recordVideoExtractFlagNames = []string{
	tooldocs.RecordVideoVideoFlagName,
	tooldocs.RecordVideoFrameAtFlagName,
	tooldocs.RecordVideoSheetFromFlagName,
	tooldocs.RecordVideoSheetToFlagName,
}

// parseRecordVideoExtractOptions reads the extraction flags. Extraction never reaches Unity, so any
// other option would be silently ignored; it is rejected instead.
func parseRecordVideoExtractOptions(args []string) (recordVideoExtractOptions, error) {
	raw, err := collectRecordVideoExtractValues(args)
	if err != nil {
		return recordVideoExtractOptions{}, err
	}
	if len(raw.present) == 0 {
		return recordVideoExtractOptions{}, nil
	}
	if len(raw.others) > 0 {
		return recordVideoExtractOptions{}, recordVideoArgumentError(
			raw.others[0],
			"--video with --frame-at or --sheet-from/--sheet-to cuts frames from a finished video and takes no other options; got "+raw.others[0]+".",
			"Run the extraction on its own, after the recording has stopped.",
		)
	}
	if err := validateRecordVideoExtractShape(raw.present); err != nil {
		return recordVideoExtractOptions{}, err
	}
	return parseRecordVideoExtractValues(raw)
}

func collectRecordVideoExtractValues(args []string) (recordVideoExtractRawValues, error) {
	raw := recordVideoExtractRawValues{values: map[string]string{}, present: map[string]bool{}}
	for index := 0; index < len(args); index++ {
		arg := args[index]
		if !isRecordVideoExtractFlag(arg) {
			raw.others = append(raw.others, arg)
			continue
		}
		name, value, consumedNext, err := clicore.ParseFlagValue(arg, args, index)
		if err != nil {
			return recordVideoExtractRawValues{}, err
		}
		// An empty value usually comes from an unset shell variable; it is not an absent flag.
		if value == "" {
			return recordVideoExtractRawValues{}, clierrors.MissingValueArgumentError("--" + name)
		}
		raw.values[name] = value
		raw.present[name] = true
		if consumedNext {
			index++
		}
	}
	return raw, nil
}

func isRecordVideoExtractFlag(arg string) bool {
	for _, name := range recordVideoExtractFlagNames {
		if isPausePointFlag(arg, name) {
			return true
		}
	}
	return false
}

func validateRecordVideoExtractShape(present map[string]bool) error {
	hasFrame := present[tooldocs.RecordVideoFrameAtFlagName]
	hasFrom := present[tooldocs.RecordVideoSheetFromFlagName]
	hasTo := present[tooldocs.RecordVideoSheetToFlagName]
	switch {
	case !present[tooldocs.RecordVideoVideoFlagName]:
		return recordVideoArgumentError("--"+tooldocs.RecordVideoVideoFlagName,
			"--frame-at and --sheet-from/--sheet-to need --video, the recorded video to cut frames from.",
			"Pass the OutputPath of the stop or one-shot response as --video.")
	case !hasFrame && !hasFrom && !hasTo:
		return recordVideoArgumentError("--"+tooldocs.RecordVideoVideoFlagName,
			"--video needs --frame-at or --sheet-from/--sheet-to.",
			"Add --frame-at <seconds> for one frame, or --sheet-from and --sheet-to for a range.")
	case hasFrame && (hasFrom || hasTo):
		return recordVideoArgumentError("--"+tooldocs.RecordVideoFrameAtFlagName,
			"--frame-at cannot be combined with --sheet-from/--sheet-to.",
			"Run one extraction per command.")
	case hasFrom && !hasTo:
		return recordVideoArgumentError("--"+tooldocs.RecordVideoSheetToFlagName,
			"--sheet-from needs --sheet-to.", "Add --sheet-to <seconds>.")
	case hasTo && !hasFrom:
		return recordVideoArgumentError("--"+tooldocs.RecordVideoSheetFromFlagName,
			"--sheet-to needs --sheet-from.", "Add --sheet-from <seconds>.")
	}
	return nil
}

func parseRecordVideoExtractValues(raw recordVideoExtractRawValues) (recordVideoExtractOptions, error) {
	options := recordVideoExtractOptions{requested: true, video: raw.values[tooldocs.RecordVideoVideoFlagName]}
	if raw.present[tooldocs.RecordVideoFrameAtFlagName] {
		frameAt, err := parseRecordVideoExtractSeconds(tooldocs.RecordVideoFrameAtFlagName, raw.values)
		if err != nil {
			return recordVideoExtractOptions{}, err
		}
		options.hasFrame = true
		options.frameAt = frameAt
		return options, nil
	}

	from, err := parseRecordVideoExtractSeconds(tooldocs.RecordVideoSheetFromFlagName, raw.values)
	if err != nil {
		return recordVideoExtractOptions{}, err
	}
	to, err := parseRecordVideoExtractSeconds(tooldocs.RecordVideoSheetToFlagName, raw.values)
	if err != nil {
		return recordVideoExtractOptions{}, err
	}
	if to <= from {
		return recordVideoExtractOptions{}, recordVideoArgumentError("--"+tooldocs.RecordVideoSheetToFlagName,
			"--sheet-to must be after --sheet-from.", "Pass a range such as --sheet-from 1.2 --sheet-to 1.8.")
	}
	options.sheetFrom = from
	options.sheetTo = to
	return options, nil
}

func parseRecordVideoExtractSeconds(name string, values map[string]string) (float64, error) {
	value := values[name]
	seconds, err := strconv.ParseFloat(value, 64)
	if err != nil || math.IsNaN(seconds) || math.IsInf(seconds, 0) || seconds < 0 {
		return 0, clierrors.InvalidValueArgumentError("--"+name, value, "non-negative number of seconds")
	}
	return seconds, nil
}
