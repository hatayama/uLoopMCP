package tooldocs

import (
	"strings"
	"testing"

	"github.com/hatayama/unity-cli-loop/common/tools"
)

// Verifies record-video's CLI-only table declares --seconds as an integer and --trigger as a string.
func TestRecordVideoCLIOnlyOptionsDeclareSecondsAndTrigger(t *testing.T) {
	options := RecordVideoCLIOnlyOptions()
	if len(options) != 2 {
		t.Fatalf("got %d options, want 2", len(options))
	}
	if options[0].FlagName != RecordVideoSecondsFlagName || options[0].Type != "integer" {
		t.Errorf("first option = %+v, want --seconds integer", options[0])
	}
	if options[1].FlagName != RecordVideoTriggerFlagName || options[1].Type != "string" {
		t.Errorf("second option = %+v, want --trigger string", options[1])
	}
}

// Verifies record-video's --help lists the one-shot --seconds and --trigger flags, which are not in
// RecordVideoSchema, so the schema-driven loop cannot produce them.
func TestVisibleOptionHelpEntriesIncludeRecordVideoCLIOnlyOptions(t *testing.T) {
	tool, ok := tools.Find(tools.LoadDefault(), RecordVideoCommandName)
	if !ok {
		t.Fatalf("embedded catalog has no %q tool", RecordVideoCommandName)
	}

	entries := VisibleOptionHelpEntriesForTool(tool)
	for _, option := range RecordVideoCLIOnlyOptions() {
		optionName := "--" + option.FlagName
		entry, found := findOptionHelpEntry(entries, optionName)
		if !found {
			t.Fatalf("record-video --help is missing CLI-only option %s", optionName)
		}
		if !strings.HasSuffix(entry.Usage, " <value>") {
			t.Errorf("valued flag usage = %q, want a value placeholder", entry.Usage)
		}
	}
}

// Verifies the one-shot flags are not added to other tools' --help.
func TestVisibleOptionHelpEntriesOmitRecordVideoCLIOnlyOptionsForOtherTools(t *testing.T) {
	tool, ok := tools.Find(tools.LoadDefault(), "screenshot")
	if !ok {
		t.Fatal("embedded catalog has no screenshot tool")
	}

	if _, found := findOptionHelpEntry(VisibleOptionHelpEntriesForTool(tool), "--"+RecordVideoSecondsFlagName); found {
		t.Fatal("screenshot --help lists record-video's --seconds")
	}
}
