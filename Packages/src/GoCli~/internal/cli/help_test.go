package cli

import (
	"bytes"
	"context"
	"strings"
	"testing"
)

func TestPrintLauncherHelpListsNativeCommandsAndLiveToolGuidance(t *testing.T) {
	var stdout bytes.Buffer

	printLauncherHelp(&stdout)

	output := stdout.String()
	for _, expected := range []string{
		"Native commands:",
		"  launch",
		"  focus-window",
		"  list",
		"  skills",
		"Unity tool commands are project-specific.",
		"uloop list",
		"--project-path <path>",
		"uloop --project-path /path/to/project list",
		"uloop completion --list-options <command>",
	} {
		if !strings.Contains(output, expected) {
			t.Fatalf("help output missing %q:\n%s", expected, output)
		}
	}
	for _, unexpected := range []string{
		"  compile",
		"  get-logs",
		"  run-tests",
	} {
		if strings.Contains(output, unexpected) {
			t.Fatalf("help output should not include baked-in Unity tool %q:\n%s", unexpected, output)
		}
	}
}

func TestPrintProjectLocalHelpListsNativeCommandsAndLiveToolGuidance(t *testing.T) {
	var stdout bytes.Buffer

	printHelp(&stdout)

	output := stdout.String()
	for _, expected := range []string{
		"Native commands:",
		"  launch",
		"  focus-window",
		"  list",
		"  sync",
		"Unity tool commands are project-specific.",
		"--project-path <path>",
		"uloop --project-path /path/to/project list",
		"uloop list",
	} {
		if !strings.Contains(output, expected) {
			t.Fatalf("help output missing %q:\n%s", expected, output)
		}
	}
	for _, unexpected := range []string{
		"  compile",
		"  get-logs",
		"  run-tests",
	} {
		if strings.Contains(output, unexpected) {
			t.Fatalf("help output should not include baked-in Unity tool %q:\n%s", unexpected, output)
		}
	}
}

func TestRunLauncherPrintsHelpAfterProjectPathOption(t *testing.T) {
	var stdout bytes.Buffer
	var stderr bytes.Buffer

	code := RunLauncher(
		context.Background(),
		[]string{"--project-path", "/does/not/need/to/exist", "-h"},
		&stdout,
		&stderr)

	if code != 0 {
		t.Fatalf("exit code mismatch: %d stderr=%s", code, stderr.String())
	}
	output := stdout.String()
	for _, expected := range []string{
		"Native commands:",
		"--project-path <path>",
		"Unity tool commands are project-specific.",
	} {
		if !strings.Contains(output, expected) {
			t.Fatalf("help output missing %q:\n%s", expected, output)
		}
	}
	if strings.Contains(output, "Native Go CLI preview") {
		t.Fatalf("launcher should not dispatch help to project-local core:\n%s", output)
	}
}
