package cli

import (
	"bytes"
	"strings"
	"testing"
)

func TestPrintLauncherHelpListsNativeAndUnityToolCommands(t *testing.T) {
	var stdout bytes.Buffer

	printLauncherHelp(&stdout)

	output := stdout.String()
	for _, expected := range []string{
		"Native commands:",
		"  launch",
		"  focus-window",
		"  list",
		"  skills",
		"Unity tool commands:",
		"  compile",
		"  get-logs",
		"uloop completion --list-options <command>",
	} {
		if !strings.Contains(output, expected) {
			t.Fatalf("help output missing %q:\n%s", expected, output)
		}
	}
}

func TestPrintProjectLocalHelpListsNativeAndUnityToolCommands(t *testing.T) {
	var stdout bytes.Buffer

	printHelp(&stdout)

	output := stdout.String()
	for _, expected := range []string{
		"Native commands:",
		"  launch",
		"  focus-window",
		"  list",
		"  sync",
		"Unity tool commands:",
		"  compile",
		"  run-tests",
		"uloop list",
	} {
		if !strings.Contains(output, expected) {
			t.Fatalf("help output missing %q:\n%s", expected, output)
		}
	}
}
