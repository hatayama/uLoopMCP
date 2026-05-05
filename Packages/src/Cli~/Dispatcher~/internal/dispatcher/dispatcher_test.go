package dispatcher

import (
	"bytes"
	"context"
	"encoding/json"
	"os"
	"path/filepath"
	"strings"
	"testing"

	"github.com/hatayama/unity-cli-loop/Packages/src/Cli/Shared/version"
)

func TestRunVersionPrintsDispatcherVersion(t *testing.T) {
	// Verifies that the global command reports dispatcher compatibility version, not core version.
	var stdout bytes.Buffer
	var stderr bytes.Buffer

	code := Run(context.Background(), []string{"--version"}, &stdout, &stderr)

	if code != 0 {
		t.Fatalf("exit code mismatch: %d stderr=%s", code, stderr.String())
	}
	if strings.TrimSpace(stdout.String()) != version.Dispatcher {
		t.Fatalf("dispatcher version mismatch: %s", stdout.String())
	}
}

func TestRunVersionAfterProjectPathPrintsDispatcherVersion(t *testing.T) {
	// Verifies that --version remains a dispatcher-owned global flag after --project-path parsing.
	projectRoot := t.TempDir()
	createUnityProject(t, projectRoot)
	var stdout bytes.Buffer
	var stderr bytes.Buffer

	code := Run(context.Background(), []string{"--project-path", projectRoot, "--version"}, &stdout, &stderr)

	if code != 0 {
		t.Fatalf("exit code mismatch: %d stderr=%s", code, stderr.String())
	}
	if strings.TrimSpace(stdout.String()) != version.Dispatcher {
		t.Fatalf("dispatcher version mismatch: %s", stdout.String())
	}
}

func TestDispatcherEnvironmentOverridesExistingDispatcherVersion(t *testing.T) {
	// Verifies that nested dispatcher invocations cannot pass stale compatibility versions to core.
	environment := dispatcherEnvironment([]string{
		"PATH=/bin",
		version.DispatcherVersionEnv + "=0.0.0",
		"HOME=/tmp/home",
	})

	matches := 0
	actual := ""
	prefix := version.DispatcherVersionEnv + "="
	for _, entry := range environment {
		if !strings.HasPrefix(entry, prefix) {
			continue
		}
		matches++
		actual = strings.TrimPrefix(entry, prefix)
	}

	if matches != 1 {
		t.Fatalf("dispatcher version env count mismatch: %d in %#v", matches, environment)
	}
	if actual != version.Dispatcher {
		t.Fatalf("dispatcher version mismatch: %s", actual)
	}
}

func TestRunMissingProjectLocalCoreReportsStructuredError(t *testing.T) {
	// Verifies that the dispatcher finds a Unity project without importing the core CLI package.
	projectRoot := t.TempDir()
	createUnityProject(t, projectRoot)
	var stdout bytes.Buffer
	var stderr bytes.Buffer

	code := Run(context.Background(), []string{"--project-path", projectRoot, "list"}, &stdout, &stderr)

	if code != 1 {
		t.Fatalf("exit code mismatch: %d stdout=%s stderr=%s", code, stdout.String(), stderr.String())
	}
	var envelope cliErrorEnvelope
	if err := json.Unmarshal(stderr.Bytes(), &envelope); err != nil {
		t.Fatalf("stderr is not valid JSON: %v\n%s", err, stderr.String())
	}
	if envelope.Error.ErrorCode != errorCodeProjectLocalCLIMissing {
		t.Fatalf("error code mismatch: %#v", envelope.Error)
	}
}

func TestRunLaunchWithoutProjectLocalCoreUsesBootstrapLaunch(t *testing.T) {
	// Verifies that first-run launch does not fail at the project-local core existence check.
	projectRoot := t.TempDir()
	createUnityProject(t, projectRoot)
	var stdout bytes.Buffer
	var stderr bytes.Buffer

	code := Run(context.Background(), []string{"--project-path", projectRoot, "launch"}, &stdout, &stderr)

	if code != 1 {
		t.Fatalf("exit code mismatch: %d stdout=%s stderr=%s", code, stdout.String(), stderr.String())
	}
	var envelope cliErrorEnvelope
	if err := json.Unmarshal(stderr.Bytes(), &envelope); err != nil {
		t.Fatalf("stderr is not valid JSON: %v\n%s", err, stderr.String())
	}
	if envelope.Error.ErrorCode == errorCodeProjectLocalCLIMissing {
		t.Fatalf("launch should bootstrap before requiring project-local core: %#v", envelope.Error)
	}
}

func TestRunLaunchQuitWithoutProjectLocalCoreDoesNotOpenUnity(t *testing.T) {
	// Verifies that bootstrap launch rejects quit instead of silently starting Unity.
	projectRoot := t.TempDir()
	createUnityProject(t, projectRoot)
	var stdout bytes.Buffer
	var stderr bytes.Buffer

	code := Run(context.Background(), []string{"--project-path", projectRoot, "launch", "--quit"}, &stdout, &stderr)

	if code != 1 {
		t.Fatalf("exit code mismatch: %d stdout=%s stderr=%s", code, stdout.String(), stderr.String())
	}
	var envelope cliErrorEnvelope
	if err := json.Unmarshal(stderr.Bytes(), &envelope); err != nil {
		t.Fatalf("stderr is not valid JSON: %v\n%s", err, stderr.String())
	}
	if envelope.Error.ErrorCode != errorCodeInvalidArgument {
		t.Fatalf("error code mismatch: %#v", envelope.Error)
	}
}

func TestParseLaunchBootstrapOptionsPreservesQuitAndRestart(t *testing.T) {
	// Verifies that bootstrap launch does not discard lifecycle flags before deciding whether to run.
	options, err := parseLaunchBootstrapOptions([]string{"--quit", "--restart"})
	if err != nil {
		t.Fatalf("parseLaunchBootstrapOptions failed: %v", err)
	}
	if !options.quit {
		t.Fatal("quit flag was not preserved")
	}
	if !options.restart {
		t.Fatal("restart flag was not preserved")
	}
}

func TestParseLaunchBootstrapOptionsAcceptsNegativeMaxDepth(t *testing.T) {
	// Verifies that bootstrap launch accepts the same unlimited search depth value as core launch.
	_, err := parseLaunchBootstrapOptions([]string{"--max-depth", "-1"})
	if err != nil {
		t.Fatalf("parseLaunchBootstrapOptions failed: %v", err)
	}
}

func TestWaitForPathReturnsWhenPathExists(t *testing.T) {
	// Verifies that bootstrap launch can block on project-local core creation without timing out.
	targetPath := filepath.Join(t.TempDir(), "uloop-core")
	if err := os.WriteFile(targetPath, []byte("core"), 0o644); err != nil {
		t.Fatalf("failed to write target path: %v", err)
	}

	if err := waitForPath(context.Background(), targetPath, launchCoreReadyTimeout); err != nil {
		t.Fatalf("waitForPath failed: %v", err)
	}
}

func TestParseProjectPathAcceptsValueForm(t *testing.T) {
	// Verifies that dispatcher parsing preserves the global --project-path contract.
	remaining, projectPath, err := parseProjectPath([]string{"--project-path=/tmp/project", "list"})
	if err != nil {
		t.Fatalf("parseProjectPath failed: %v", err)
	}

	if projectPath != "/tmp/project" {
		t.Fatalf("project path mismatch: %s", projectPath)
	}
	if len(remaining) != 1 || remaining[0] != "list" {
		t.Fatalf("remaining args mismatch: %#v", remaining)
	}
}

func TestFindUnityProjectRootWithinFindsNestedLaunchProject(t *testing.T) {
	// Verifies that global launch can still discover a nested Unity project before core dispatch.
	workspaceRoot := t.TempDir()
	projectRoot := filepath.Join(workspaceRoot, "nested", "Game")
	createUnityProject(t, projectRoot)

	resolved, err := resolveProjectRoot(workspaceRoot, "", []string{"launch"})
	if err != nil {
		t.Fatalf("resolveProjectRoot failed: %v", err)
	}
	if resolved != projectRoot {
		t.Fatalf("project root mismatch: %s", resolved)
	}
}

func TestResolveProjectRootForLaunchUsesPositionalProjectPath(t *testing.T) {
	// Verifies that launch can target a project outside the current directory before core dispatch.
	workspaceRoot := t.TempDir()
	projectRoot := filepath.Join(t.TempDir(), "Game")
	createUnityProject(t, projectRoot)

	resolved, err := resolveProjectRoot(workspaceRoot, "", []string{"launch", projectRoot})
	if err != nil {
		t.Fatalf("resolveProjectRoot failed: %v", err)
	}
	if resolved != projectRoot {
		t.Fatalf("project root mismatch: %s", resolved)
	}
}

func TestResolveProjectRootForLaunchSkipsOptionValuesBeforePositionalProjectPath(t *testing.T) {
	// Verifies that launch option values are not mistaken for the project path used for core dispatch.
	workspaceRoot := t.TempDir()
	projectRoot := filepath.Join(t.TempDir(), "Game")
	createUnityProject(t, projectRoot)

	resolved, err := resolveProjectRoot(workspaceRoot, "", []string{"launch", "--platform", "iOS", projectRoot})
	if err != nil {
		t.Fatalf("resolveProjectRoot failed: %v", err)
	}
	if resolved != projectRoot {
		t.Fatalf("project root mismatch: %s", resolved)
	}
}

func TestForwardedProjectLocalArgsForLaunchUsesResolvedPositionalProjectPath(t *testing.T) {
	// Verifies that Windows core dispatch does not re-resolve relative launch paths from the project root.
	projectRoot := filepath.Join(t.TempDir(), "Game")
	args := []string{"launch", "--platform", "iOS", "relative/Game"}

	forwarded := forwardedProjectLocalArgs(args, "", projectRoot)

	if len(forwarded) != len(args) {
		t.Fatalf("forwarded args length mismatch: %#v", forwarded)
	}
	if forwarded[3] != projectRoot {
		t.Fatalf("forwarded project path mismatch: %#v", forwarded)
	}
	if args[3] != "relative/Game" {
		t.Fatalf("input args were mutated: %#v", args)
	}
}

func TestRunCompletionScriptDoesNotRequireUnityProject(t *testing.T) {
	// Verifies that shell completion stays a dispatcher-owned global command.
	changeDirectory(t, t.TempDir())
	var stdout bytes.Buffer
	var stderr bytes.Buffer

	code := Run(context.Background(), []string{"completion", "--shell", "bash"}, &stdout, &stderr)

	if code != 0 {
		t.Fatalf("exit code mismatch: %d stderr=%s", code, stderr.String())
	}
	if !strings.Contains(stdout.String(), "complete -F _uloop_completions uloop") {
		t.Fatalf("bash completion script mismatch: %s", stdout.String())
	}
}

func TestRunCompletionInstallDoesNotRequireUnityProject(t *testing.T) {
	// Verifies that installing shell completion does not depend on a project-local core binary.
	temporaryHome := t.TempDir()
	t.Setenv("HOME", temporaryHome)
	changeDirectory(t, t.TempDir())
	var stdout bytes.Buffer
	var stderr bytes.Buffer

	code := Run(context.Background(), []string{"completion", "--shell", "zsh", "--install"}, &stdout, &stderr)

	if code != 0 {
		t.Fatalf("exit code mismatch: %d stderr=%s", code, stderr.String())
	}
	configPath := filepath.Join(temporaryHome, ".zshrc")
	content, err := os.ReadFile(configPath)
	if err != nil {
		t.Fatalf("failed to read completion config: %v", err)
	}
	if !strings.Contains(string(content), `eval "$(uloop completion --shell zsh)"`) {
		t.Fatalf("completion config mismatch: %s", string(content))
	}
	if !strings.Contains(stdout.String(), "Completion installed") {
		t.Fatalf("install output mismatch: %s", stdout.String())
	}
}

func TestRunCompletionListsCachedToolOptions(t *testing.T) {
	// Verifies that dispatcher completion probes can use project-local tool cache without importing core packages.
	projectRoot := t.TempDir()
	createUnityProject(t, projectRoot)
	createToolCache(t, projectRoot)
	changeDirectory(t, projectRoot)
	var stdout bytes.Buffer
	var stderr bytes.Buffer

	code := Run(context.Background(), []string{"--list-options", "compile"}, &stdout, &stderr)

	if code != 0 {
		t.Fatalf("exit code mismatch: %d stderr=%s", code, stderr.String())
	}
	output := stdout.String()
	for _, option := range []string{"--force-recompile", "--no-wait-for-domain-reload"} {
		if !strings.Contains(output, option) {
			t.Fatalf("option %s was not listed: %s", option, output)
		}
	}
}

func TestRunCompletionListsCompletionCommandOptions(t *testing.T) {
	// Verifies that shell completion suggests dispatcher-owned completion flags.
	changeDirectory(t, t.TempDir())
	var stdout bytes.Buffer
	var stderr bytes.Buffer

	code := Run(context.Background(), []string{"completion", "--list-options", "completion"}, &stdout, &stderr)

	if code != 0 {
		t.Fatalf("exit code mismatch: %d stderr=%s", code, stderr.String())
	}
	output := stdout.String()
	for _, option := range []string{"--install", "--shell"} {
		if !strings.Contains(output, option) {
			t.Fatalf("option %s was not listed: %s", option, output)
		}
	}
}

func TestRunLauncherPrintsHelpAfterProjectPathOption(t *testing.T) {
	// Verifies that dispatcher help handles --project-path before dispatching to project-local core.
	var stdout bytes.Buffer
	var stderr bytes.Buffer

	code := Run(
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

func TestRunLauncherPrintsProjectCachedToolsAfterProjectPathOption(t *testing.T) {
	// Verifies that dispatcher help shows cached tool commands for an explicit Unity project path.
	projectRoot := t.TempDir()
	createUnityProject(t, projectRoot)
	createProjectHelpToolCache(t, projectRoot)
	var stdout bytes.Buffer
	var stderr bytes.Buffer

	code := Run(
		context.Background(),
		[]string{"--project-path", projectRoot, "-h"},
		&stdout,
		&stderr)

	assertProjectToolHelp(t, code, stdout.String(), stderr.String())
}

func TestRunLauncherPrintsProjectCachedToolsFromCurrentDirectory(t *testing.T) {
	// Verifies that dispatcher help shows cached tool commands when run inside a Unity project.
	projectRoot := t.TempDir()
	createUnityProject(t, projectRoot)
	createProjectHelpToolCache(t, projectRoot)
	changeDirectory(t, projectRoot)
	var stdout bytes.Buffer
	var stderr bytes.Buffer

	code := Run(
		context.Background(),
		[]string{"-h"},
		&stdout,
		&stderr)

	assertProjectToolHelp(t, code, stdout.String(), stderr.String())
}

func TestLoadCachedToolsFiltersInternalSkillTools(t *testing.T) {
	// Verifies that dispatcher help and completion do not advertise tools the core will reject.
	projectRoot := t.TempDir()
	createUnityProject(t, projectRoot)
	createSkill(t, projectRoot, "Assets/Editor/InternalTool/Skill", `---
name: uloop-internal-tool
internal: true
---

# internal
`)
	createToolCache(t, projectRoot)

	cache, ok := loadCachedTools(projectRoot)
	if !ok {
		t.Fatal("expected cached tools")
	}
	if containsCachedTool(cache, "internal-tool") {
		t.Fatalf("internal tool was not filtered: %#v", cache.Tools)
	}
	if !containsCachedTool(cache, "compile") {
		t.Fatalf("public tool was filtered: %#v", cache.Tools)
	}
}

func createProjectHelpToolCache(t *testing.T, projectRoot string) {
	t.Helper()

	cachePath := filepath.Join(projectRoot, ".uloop", "tools.json")
	if err := os.MkdirAll(filepath.Dir(cachePath), 0o755); err != nil {
		t.Fatalf("failed to create tool cache directory: %v", err)
	}
	content := `{
  "version": "test",
  "tools": [
    {
      "name": "project-tool",
      "description": "Project specific tool",
      "inputSchema": {"type": "object", "properties": {}}
    },
    {
      "name": "focus-window",
      "description": "Cached focus-window should not be listed because native command wins",
      "inputSchema": {"type": "object", "properties": {}}
    }
  ]
}`
	if err := os.WriteFile(cachePath, []byte(content), 0o644); err != nil {
		t.Fatalf("failed to write tool cache: %v", err)
	}
}

func assertProjectToolHelp(t *testing.T, code int, output string, stderr string) {
	t.Helper()
	if code != 0 {
		t.Fatalf("exit code mismatch: %d stderr=%s", code, stderr)
	}
	for _, expected := range []string{
		"Unity tool commands from this project's cache:",
		"project-tool",
		"Project specific tool",
	} {
		if !strings.Contains(output, expected) {
			t.Fatalf("help output missing %q:\n%s", expected, output)
		}
	}
	if strings.Contains(output, "Cached focus-window should not be listed") {
		t.Fatalf("help output should not list cached tools shadowed by native commands:\n%s", output)
	}
}

func createUnityProject(t *testing.T, projectRoot string) {
	t.Helper()

	if err := os.MkdirAll(filepath.Join(projectRoot, assetsDirectoryName), 0o755); err != nil {
		t.Fatalf("failed to create Assets: %v", err)
	}
	if err := os.MkdirAll(filepath.Join(projectRoot, projectSettingsDirectory), 0o755); err != nil {
		t.Fatalf("failed to create ProjectSettings: %v", err)
	}
	if err := os.WriteFile(
		filepath.Join(projectRoot, projectSettingsDirectory, "ProjectVersion.txt"),
		[]byte("m_EditorVersion: 9999.9.9f9\n"),
		0o644,
	); err != nil {
		t.Fatalf("failed to write ProjectVersion.txt: %v", err)
	}
}

func createToolCache(t *testing.T, projectRoot string) {
	t.Helper()

	cachePath := filepath.Join(projectRoot, ".uloop", "tools.json")
	if err := os.MkdirAll(filepath.Dir(cachePath), 0o755); err != nil {
		t.Fatalf("failed to create tool cache directory: %v", err)
	}
	content := `{
  "tools": [
    {
      "name": "internal-tool",
      "description": "Internal",
      "inputSchema": {
        "properties": {}
      }
    },
    {
      "name": "compile",
      "description": "Compile Unity scripts",
      "inputSchema": {
        "properties": {
          "forceRecompile": { "type": "boolean" },
          "waitForDomainReload": { "type": "boolean", "default": true }
        }
      }
    }
  ]
}`
	if err := os.WriteFile(cachePath, []byte(content), 0o644); err != nil {
		t.Fatalf("failed to write tool cache: %v", err)
	}
}

func createSkill(t *testing.T, projectRoot string, relativePath string, content string) {
	t.Helper()

	skillPath := filepath.Join(projectRoot, relativePath, skillFileName)
	if err := os.MkdirAll(filepath.Dir(skillPath), 0o755); err != nil {
		t.Fatalf("failed to create skill directory: %v", err)
	}
	if err := os.WriteFile(skillPath, []byte(content), 0o644); err != nil {
		t.Fatalf("failed to write skill: %v", err)
	}
}

func containsCachedTool(cache cachedTools, name string) bool {
	for _, tool := range cache.Tools {
		if tool.Name == name {
			return true
		}
	}
	return false
}

func changeDirectory(t *testing.T, path string) {
	t.Helper()

	currentDirectory, err := os.Getwd()
	if err != nil {
		t.Fatalf("failed to read current directory: %v", err)
	}
	if err := os.Chdir(path); err != nil {
		t.Fatalf("failed to change directory: %v", err)
	}
	t.Cleanup(func() {
		if err := os.Chdir(currentDirectory); err != nil {
			t.Fatalf("failed to restore directory: %v", err)
		}
	})
}
