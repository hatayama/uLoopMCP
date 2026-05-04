package dispatcher

import (
	"context"
	"fmt"
	"io"
	"os"
	"os/exec"
	"path/filepath"
	"regexp"
	"runtime"
	"strconv"
	"strings"
	"time"
)

const (
	launchCommandName        = "launch"
	launchPathPollInterval   = 500 * time.Millisecond
	launchCoreReadyTimeout   = 180 * time.Second
	launchLockfileTimeout    = 5 * time.Second
	projectVersionFilePath   = "ProjectSettings/ProjectVersion.txt"
	recoveryDirectoryPath    = "Assets/_Recovery"
	launchTempDirectoryName  = "Temp"
	unityLockfileName        = "UnityLockfile"
	unsupportedBootstrapFlag = "requires project-local uloop-core and cannot run before the core is generated"
)

var editorVersionPattern = regexp.MustCompile(`(?m)^m_EditorVersion:\s*(.+)$`)

type launchBootstrapOptions struct {
	deleteRecovery bool
	platform       string
	quit           bool
	restart        bool
}

func isLaunchCommand(args []string) bool {
	return len(args) > 0 && args[0] == launchCommandName
}

func isLaunchHelpRequest(args []string) bool {
	return len(args) == 2 && args[0] == launchCommandName && isHelpRequest(args[1:])
}

func runLaunchBootstrap(ctx context.Context, args []string, projectRoot string, stdout io.Writer, stderr io.Writer) int {
	options, err := parseLaunchBootstrapOptions(args)
	if err != nil {
		writeError(stderr, argumentError(err.Error(), launchCommandName))
		return 1
	}
	if options.quit {
		writeError(stderr, argumentError("--quit "+unsupportedBootstrapFlag, launchCommandName))
		return 1
	}
	if options.restart {
		writeError(stderr, argumentError("--restart "+unsupportedBootstrapFlag, launchCommandName))
		return 1
	}

	if options.deleteRecovery {
		if err := os.RemoveAll(filepath.Join(projectRoot, recoveryDirectoryPath)); err != nil {
			writeError(stderr, internalError(err.Error(), projectRoot))
			return 1
		}
	}

	unityPath, err := resolveUnityExecutablePath(projectRoot)
	if err != nil {
		writeError(stderr, internalError(err.Error(), projectRoot))
		return 1
	}

	launchArgs := []string{"-projectPath", projectRoot}
	if options.platform != "" {
		launchArgs = append(launchArgs, "-buildTarget", options.platform)
	}

	writeLine(stdout, "Opening Unity...")
	writeFormat(stdout, "Project Path: %s\n", projectRoot)
	writeFormat(stdout, "Detected Unity version: %s\n", readUnityVersionForLog(projectRoot))

	command := newUnityLaunchCommand(ctx, unityPath, launchArgs)
	if err := command.Start(); err != nil {
		writeError(stderr, internalError(err.Error(), projectRoot))
		return 1
	}
	if err := command.Process.Release(); err != nil {
		writeError(stderr, internalError(err.Error(), projectRoot))
		return 1
	}
	_ = waitForPath(ctx, unityLockfilePath(projectRoot), launchLockfileTimeout)
	if err := waitForPath(ctx, projectLocalPath(projectRoot), launchCoreReadyTimeout); err != nil {
		writeError(stderr, internalError(err.Error(), projectRoot))
		return 1
	}
	return 0
}

func parseLaunchBootstrapOptions(args []string) (launchBootstrapOptions, error) {
	options := launchBootstrapOptions{}
	for index := 0; index < len(args); index++ {
		arg := args[index]
		switch {
		case arg == "-r" || arg == "--restart":
			options.restart = true
		case arg == "-q" || arg == "--quit":
			options.quit = true
		case arg == "-d" || arg == "--delete-recovery":
			options.deleteRecovery = true
		case arg == "-a" || arg == "-f" || arg == "--add-unity-hub" || arg == "--favorite" || arg == "--unity-hub-entry":
			return launchBootstrapOptions{}, fmt.Errorf("native launch does not support Unity Hub registration option: %s", arg)
		case arg == "-p" || arg == "--platform":
			value, consumed, err := readLaunchOptionValue(arg, args, index)
			if err != nil {
				return launchBootstrapOptions{}, err
			}
			options.platform = value
			if consumed {
				index++
			}
		case strings.HasPrefix(arg, "--platform="):
			options.platform = strings.TrimPrefix(arg, "--platform=")
		case arg == "--max-depth":
			_, consumed, err := readLaunchOptionValue(arg, args, index)
			if err != nil {
				return launchBootstrapOptions{}, err
			}
			if consumed {
				index++
			}
		case strings.HasPrefix(arg, "--max-depth="):
			continue
		case strings.HasPrefix(arg, "-"):
			return launchBootstrapOptions{}, fmt.Errorf("unknown launch option: %s", arg)
		default:
			continue
		}
	}
	return options, nil
}

func readLaunchOptionValue(option string, args []string, index int) (string, bool, error) {
	if strings.Contains(option, "=") {
		parts := strings.SplitN(option, "=", 2)
		if parts[1] == "" {
			return "", false, fmt.Errorf("%s requires a value", parts[0])
		}
		return parts[1], false, nil
	}
	if index+1 >= len(args) || strings.HasPrefix(args[index+1], "-") {
		return "", false, fmt.Errorf("%s requires a value", option)
	}
	return args[index+1], true, nil
}

func resolveUnityExecutablePath(projectRoot string) (string, error) {
	unityVersion, err := readUnityEditorVersion(projectRoot)
	if err != nil {
		return "", err
	}

	candidates := unityExecutableCandidates(unityVersion)
	for _, candidate := range candidates {
		if _, err := os.Stat(candidate); err == nil {
			return candidate, nil
		}
	}
	if len(candidates) == 0 {
		return "", fmt.Errorf("unity launch is not supported on %s", runtime.GOOS)
	}
	return candidates[0], nil
}

func readUnityEditorVersion(projectRoot string) (string, error) {
	content, err := os.ReadFile(filepath.Join(projectRoot, projectVersionFilePath))
	if err != nil {
		return "", err
	}
	matches := editorVersionPattern.FindStringSubmatch(string(content))
	if len(matches) != 2 {
		return "", fmt.Errorf("unity editor version not found in %s", projectVersionFilePath)
	}
	version := strings.TrimSpace(matches[1])
	if version == "" {
		return "", fmt.Errorf("unity editor version is empty in %s", projectVersionFilePath)
	}
	return version, nil
}

func readUnityVersionForLog(projectRoot string) string {
	version, err := readUnityEditorVersion(projectRoot)
	if err != nil {
		return "unknown"
	}
	return version
}

func unityExecutableCandidates(version string) []string {
	switch runtime.GOOS {
	case "darwin":
		return []string{fmt.Sprintf("/Applications/Unity/Hub/Editor/%s/Unity.app/Contents/MacOS/Unity", version)}
	case "windows":
		return windowsUnityExecutableCandidates(version)
	default:
		return []string{}
	}
}

func windowsUnityExecutableCandidates(version string) []string {
	candidates := []string{}
	for _, base := range []string{
		os.Getenv("ProgramFiles"),
		os.Getenv("ProgramFiles(x86)"),
		os.Getenv("LOCALAPPDATA"),
		`C:\Program Files`,
	} {
		if base == "" {
			continue
		}
		candidates = append(candidates, filepath.Join(base, "Unity", "Hub", "Editor", version, "Editor", "Unity.exe"))
	}
	return candidates
}

func newUnityLaunchCommand(ctx context.Context, unityPath string, launchArgs []string) *exec.Cmd {
	command := exec.CommandContext(ctx, unityPath, launchArgs...)
	command.Env = append(os.Environ(), "MSYS_NO_PATHCONV=1")
	configureDetachedUnityLaunchCommand(command)
	return command
}

func unityLockfilePath(projectRoot string) string {
	return filepath.Join(projectRoot, launchTempDirectoryName, unityLockfileName)
}

func waitForPath(ctx context.Context, targetPath string, timeout time.Duration) error {
	timeoutContext, cancel := context.WithTimeout(ctx, timeout)
	defer cancel()

	ticker := time.NewTicker(launchPathPollInterval)
	defer ticker.Stop()

	for {
		if _, err := os.Stat(targetPath); err == nil {
			return nil
		} else if !os.IsNotExist(err) {
			return err
		}

		select {
		case <-timeoutContext.Done():
			if ctx.Err() != nil {
				return ctx.Err()
			}
			return fmt.Errorf("timed out waiting for %s", targetPath)
		case <-ticker.C:
		}
	}
}

func launchMaxDepth(args []string) int {
	for index := 0; index < len(args); index++ {
		arg := args[index]
		if arg == "--max-depth" && index+1 < len(args) {
			depth, err := strconv.Atoi(args[index+1])
			if err == nil {
				return depth
			}
			return defaultLaunchMaxDepth
		}
		if strings.HasPrefix(arg, "--max-depth=") {
			depth, err := strconv.Atoi(strings.TrimPrefix(arg, "--max-depth="))
			if err == nil {
				return depth
			}
			return defaultLaunchMaxDepth
		}
	}
	return defaultLaunchMaxDepth
}

func printLaunchHelp(stdout io.Writer) {
	writeLine(stdout, "Usage:")
	writeLine(stdout, "  uloop launch [project-path] [options]")
	writeLine(stdout, "")
	writeLine(stdout, "Options:")
	writeLine(stdout, "  -r, --restart                 Restart Unity if it is already running")
	writeLine(stdout, "  -q, --quit                    Quit Unity if it is running")
	writeLine(stdout, "  -d, --delete-recovery         Delete Assets/_Recovery before launching")
	writeLine(stdout, "  -p, --platform <target>       Pass Unity -buildTarget")
	writeLine(stdout, "  --max-depth <n>               Search depth for nested project discovery")
}
