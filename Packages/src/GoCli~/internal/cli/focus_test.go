package cli

import "testing"

func TestParseMacUnityProcessesExtractsProjectPath(t *testing.T) {
	output := `123 /Applications/Unity/Hub/Editor/6000.0.0f1/Unity.app/Contents/MacOS/Unity -projectPath "/Users/me/My Project" -useHub -hubIPC
456 /Applications/Unity/Hub/Editor/6000.0.0f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/me/Batch"
789 /Applications/Unity/Hub/Editor/6000.0.0f1/Unity.app/Contents/MacOS/Unity -projectPath /Users/me/Other -logFile -
`

	processes := parseMacUnityProcesses(output)

	if len(processes) != 2 {
		t.Fatalf("process count mismatch: %#v", processes)
	}
	if processes[0].pid != 123 || processes[0].projectPath != "/Users/me/My Project" {
		t.Fatalf("first process mismatch: %#v", processes[0])
	}
	if processes[1].pid != 789 || processes[1].projectPath != "/Users/me/Other" {
		t.Fatalf("second process mismatch: %#v", processes[1])
	}
}

func TestParseWindowsUnityProcessesExtractsProjectPath(t *testing.T) {
	output := `123|C:\Program Files\Unity\Hub\Editor\6000.0.0f1\Editor\Unity.exe -projectPath "C:\Users\me\My Project" -useHub
456|C:\Program Files\Unity\Hub\Editor\6000.0.0f1\Editor\Unity.exe -batchmode -projectPath "C:\Users\me\Batch"
`

	processes := parseWindowsUnityProcesses(output)

	if len(processes) != 1 {
		t.Fatalf("process count mismatch: %#v", processes)
	}
	if processes[0].pid != 123 || processes[0].projectPath != `C:\Users\me\My Project` {
		t.Fatalf("process mismatch: %#v", processes[0])
	}
}

func TestExtractProjectPathSupportsEqualsAndSpaces(t *testing.T) {
	cases := map[string]string{
		`Unity -projectPath="/Users/me/My Project" -useHub`:  "/Users/me/My Project",
		`Unity -projectpath '/Users/me/Other Project' -flag`: "/Users/me/Other Project",
		`Unity -projectPath /Users/me/Plain -flag`:           "/Users/me/Plain",
	}

	for command, expected := range cases {
		actual := extractProjectPath(command)
		if actual != expected {
			t.Fatalf("project path mismatch for %q: %q", command, actual)
		}
	}
}
