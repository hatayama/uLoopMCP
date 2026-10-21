package tooldocs

// record-video accepts flags that exist only in the CLI: the project runner parses them out of the
// argv and either drives start, the trigger, mark, the wait, and stop itself (one-shot), or cuts
// frames out of a finished video with ffmpeg without contacting Unity (extraction). The Unity-side
// RecordVideoSchema never describes them. Both option listings read this table so they cannot drift.
const (
	RecordVideoCommandName       = "record-video"
	RecordVideoSecondsFlagName   = "seconds"
	RecordVideoTriggerFlagName   = "trigger"
	RecordVideoVideoFlagName     = "video"
	RecordVideoFrameAtFlagName   = "frame-at"
	RecordVideoSheetFromFlagName = "sheet-from"
	RecordVideoSheetToFlagName   = "sheet-to"
)

// RecordVideoCLIOnlyOptions returns record-video's CLI-only flags. A fresh slice is built per call
// so a caller that sorts or appends cannot mutate the shared table.
func RecordVideoCLIOnlyOptions() []PausePointCLIOnlyOption {
	return []PausePointCLIOnlyOption{
		{
			FlagName: RecordVideoSecondsFlagName,
			Type:     "integer",
			Description: "One-shot: stop the recording this many seconds after --trigger returns " +
				"(or after start when there is no trigger). 1-600, at most --max-duration-seconds. " +
				"Only with --action start.",
		},
		{
			FlagName: RecordVideoTriggerFlagName,
			Type:     "string",
			Description: "One-shot: a uloop command to run right after recording starts, for example " +
				"\"simulate-keyboard --action Press --key C\" or " +
				"\"enable-pause-point --file <path> --line <n> --mode trace --await\". " +
				"The burst sheet is built around the moment it returns. " +
				"Implies --seconds 3 when --seconds is absent.",
		},
		{
			FlagName: RecordVideoVideoFlagName,
			Type:     "string",
			Description: "Extraction: the recorded video to cut frames from (the OutputPath of a stop or one-shot response). " +
				"Requires ffmpeg; does not contact Unity. Use with --frame-at or --sheet-from/--sheet-to.",
		},
		{
			FlagName:    RecordVideoFrameAtFlagName,
			Type:        "number",
			Description: "Extraction: write the full-size frame at this many seconds into the video as a PNG beside it.",
		},
		{
			FlagName:    RecordVideoSheetFromFlagName,
			Type:        "number",
			Description: "Extraction: start of a range, in seconds, to write as a sheet of consecutive frames (up to 40). Requires --sheet-to.",
		},
		{
			FlagName:    RecordVideoSheetToFlagName,
			Type:        "number",
			Description: "Extraction: end of the --sheet-from range, in seconds.",
		},
	}
}

func appendRecordVideoCLIOnlyOptionHelpEntries(
	toolName string,
	entries []OptionHelpEntry,
) []OptionHelpEntry {
	if toolName != RecordVideoCommandName {
		return entries
	}

	for _, entry := range PausePointCLIOnlyHelpEntries(RecordVideoCLIOnlyOptions()) {
		if hasOptionHelpEntry(entries, entry.Name) {
			continue
		}
		entries = append(entries, entry)
	}
	return entries
}
