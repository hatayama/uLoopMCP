package dispatcher

import (
	"context"
	"encoding/json"
	"fmt"
	"io"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"sort"
	"strings"
	"syscall"

	"github.com/hatayama/unity-cli-loop/Packages/src/GoCli/internal/version"
)

const (
	projectPathFlagName      = "project-path"
	projectLocalUnixPath     = ".uloop/bin/uloop-core"
	projectLocalWindowsPath  = ".uloop/bin/uloop-core.exe"
	assetsDirectoryName      = "Assets"
	projectSettingsDirectory = "ProjectSettings"
	defaultLaunchMaxDepth    = 3
)

var excludedProjectSearchDirs = map[string]bool{
	".git":         true,
	"Build":        true,
	"Builds":       true,
	"Library":      true,
	"Logs":         true,
	"Temp":         true,
	"node_modules": true,
	"obj":          true,
}

func Run(ctx context.Context, args []string, stdout io.Writer, stderr io.Writer) int {
	if isVersionRequest(args) {
		writeLine(stdout, version.Dispatcher)
		return 0
	}
	if handled, code := tryHandleCompletionRequest(args, stdout, stderr); handled {
		return code
	}
	if handled, code := tryHandleUpdateRequest(ctx, args, stdout, stderr); handled {
		return code
	}

	startPath, err := os.Getwd()
	if err != nil {
		writeError(stderr, internalError(err.Error(), ""))
		return 1
	}
	if len(args) == 0 || isHelpRequest(args) {
		printHelpForResolvedProject(stdout, startPath, "")
		return 0
	}

	remainingArgs, explicitProjectPath, err := parseProjectPath(args)
	if err != nil {
		writeError(stderr, argumentError(err.Error(), ""))
		return 1
	}
	if len(remainingArgs) == 0 || isHelpRequest(remainingArgs) {
		printHelpForResolvedProject(stdout, startPath, explicitProjectPath)
		return 0
	}
	if isLaunchHelpRequest(remainingArgs) {
		printLaunchHelp(stdout)
		return 0
	}

	projectRoot, err := resolveProjectRoot(startPath, explicitProjectPath, remainingArgs)
	if err != nil {
		writeError(stderr, projectNotFoundError(err.Error(), commandName(remainingArgs)))
		return 1
	}

	localPath := projectLocalPath(projectRoot)
	if _, err := os.Stat(localPath); err != nil {
		if isLaunchCommand(remainingArgs) {
			return runLaunchBootstrap(ctx, remainingArgs[1:], projectRoot, stdout, stderr)
		}
		writeError(stderr, projectLocalCLIMissingError(localPath, projectRoot, commandName(remainingArgs)))
		return 1
	}

	forwardedArgs := append([]string{}, remainingArgs...)
	if explicitProjectPath != "" {
		forwardedArgs = append(forwardedArgs, "--project-path", projectRoot)
	}
	return execProjectLocal(ctx, localPath, forwardedArgs, projectRoot, stderr)
}

func commandName(args []string) string {
	if len(args) == 0 {
		return ""
	}
	return args[0]
}

func isVersionRequest(args []string) bool {
	return len(args) == 1 && (args[0] == "--version" || args[0] == "-v")
}

func isHelpRequest(args []string) bool {
	return len(args) == 1 && (args[0] == "--help" || args[0] == "-h")
}

func parseProjectPath(args []string) ([]string, string, error) {
	remaining := make([]string, 0, len(args))
	projectPath := ""

	for index := 0; index < len(args); index++ {
		arg := args[index]
		if arg == "--"+projectPathFlagName {
			if index+1 >= len(args) || strings.HasPrefix(args[index+1], "-") {
				return nil, "", fmt.Errorf("--%s requires a value", projectPathFlagName)
			}
			projectPath = args[index+1]
			index++
			continue
		}
		prefix := "--" + projectPathFlagName + "="
		if strings.HasPrefix(arg, prefix) {
			value := strings.TrimPrefix(arg, prefix)
			if value == "" {
				return nil, "", fmt.Errorf("--%s requires a value", projectPathFlagName)
			}
			projectPath = value
			continue
		}
		remaining = append(remaining, arg)
	}

	return remaining, projectPath, nil
}

func resolveProjectRoot(startPath string, explicitProjectPath string, args []string) (string, error) {
	if explicitProjectPath != "" {
		projectRoot, err := filepath.Abs(explicitProjectPath)
		if err != nil {
			return "", err
		}
		if !isUnityProject(projectRoot) {
			return "", fmt.Errorf("--project-path does not point to a Unity project: %s", projectRoot)
		}
		return projectRoot, nil
	}
	if len(args) > 0 && args[0] == "launch" {
		projectPath := launchPositionalProjectPath(args[1:])
		if projectPath != "" {
			projectRoot, err := filepath.Abs(projectPath)
			if err != nil {
				return "", err
			}
			if !isUnityProject(projectRoot) {
				return "", fmt.Errorf("not a Unity project: %s", projectRoot)
			}
			return projectRoot, nil
		}
		return findUnityProjectRootWithin(startPath, launchMaxDepth(args[1:]))
	}
	return findProjectRoot(startPath)
}

func launchPositionalProjectPath(args []string) string {
	for index := 0; index < len(args); index++ {
		arg := args[index]
		if arg == "-p" || arg == "--platform" || arg == "--max-depth" {
			if index+1 < len(args) {
				index++
			}
			continue
		}
		if strings.HasPrefix(arg, "-") {
			continue
		}
		return arg
	}
	return ""
}

func findProjectRoot(startPath string) (string, error) {
	currentPath, err := filepath.Abs(startPath)
	if err != nil {
		return "", err
	}

	for {
		if isUnityProject(currentPath) {
			return currentPath, nil
		}
		if pathExists(filepath.Join(currentPath, ".git")) {
			return "", fmt.Errorf("unity project not found. Use --project-path option to specify the target")
		}

		parentPath := filepath.Dir(currentPath)
		if parentPath == currentPath {
			return "", fmt.Errorf("unity project not found. Use --project-path option to specify the target")
		}
		currentPath = parentPath
	}
}

func findUnityProjectRootWithin(startPath string, maxDepth int) (string, error) {
	currentPath, err := filepath.Abs(startPath)
	if err != nil {
		return "", err
	}
	if isUnityProject(currentPath) {
		return currentPath, nil
	}

	childProjects := findUnityProjectsInChildren(currentPath, maxDepth)
	if len(childProjects) > 0 {
		return childProjects[0], nil
	}
	return findProjectRoot(currentPath)
}

func findUnityProjectsInChildren(startPath string, maxDepth int) []string {
	projects := []string{}

	var scan func(string, int)
	scan = func(currentPath string, depth int) {
		if maxDepth >= 0 && depth > maxDepth {
			return
		}
		if isUnityProject(currentPath) {
			projects = append(projects, currentPath)
			return
		}

		entries, err := os.ReadDir(currentPath)
		if err != nil {
			return
		}

		for _, entry := range entries {
			if !entry.IsDir() || excludedProjectSearchDirs[entry.Name()] {
				continue
			}
			scan(filepath.Join(currentPath, entry.Name()), depth+1)
		}
	}

	scan(startPath, 0)
	sort.Strings(projects)
	return projects
}

func isUnityProject(projectPath string) bool {
	return pathExists(filepath.Join(projectPath, assetsDirectoryName)) &&
		pathExists(filepath.Join(projectPath, projectSettingsDirectory))
}

func pathExists(path string) bool {
	_, err := os.Stat(path)
	return err == nil
}

func projectLocalPath(projectRoot string) string {
	if runtime.GOOS == "windows" {
		return filepath.Join(projectRoot, projectLocalWindowsPath)
	}
	return filepath.Join(projectRoot, projectLocalUnixPath)
}

func execProjectLocal(ctx context.Context, localPath string, args []string, projectRoot string, stderr io.Writer) int {
	environment := append(os.Environ(), version.DispatcherVersionEnv+"="+version.Dispatcher)
	if runtime.GOOS != "windows" {
		err := syscall.Exec(localPath, append([]string{localPath}, args...), environment)
		if err != nil {
			writeError(stderr, internalError(err.Error(), projectRoot))
			return 1
		}
		return 0
	}

	command := exec.CommandContext(ctx, localPath, args...)
	command.Dir = projectRoot
	command.Stdout = os.Stdout
	command.Stderr = os.Stderr
	command.Stdin = os.Stdin
	command.Env = environment
	if err := command.Run(); err != nil {
		return 1
	}
	return 0
}

func writeLine(writer io.Writer, value string) {
	_, _ = fmt.Fprintln(writer, value)
}

func writeFormat(writer io.Writer, format string, args ...any) {
	_, _ = fmt.Fprintf(writer, format, args...)
}

func writeError(writer io.Writer, err cliError) {
	encoder := json.NewEncoder(writer)
	encoder.SetIndent("", "  ")
	_ = encoder.Encode(cliErrorEnvelope{
		Success: false,
		Error:   err,
	})
}
