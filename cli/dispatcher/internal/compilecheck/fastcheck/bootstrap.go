package fastcheck

import (
	"context"
	"crypto/sha256"
	"debug/pe"
	"encoding/hex"
	"errors"
	"fmt"
	"io/fs"
	"os"
	"os/exec"
	"path"
	"path/filepath"
	"sort"
	"strings"
	"time"
)

const (
	workerCacheDirectoryName       = "fastcheck-worker"
	workerDllFileName              = "worker.dll"
	workerRuntimeConfigFileName    = "worker.runtimeconfig.json"
	compilerRuntimeConfigFileName  = "csc.runtimeconfig.json"
	roslynDirectorySidecarFileName = "roslyn-directory.txt"
	buildFailedFileName            = "build-failed.txt"
	workerResponseFileName         = "worker.rsp"
	workerSourceSubdirectory       = "src"
	codeAnalysisDllFileName        = "Microsoft.CodeAnalysis.dll"
	codeAnalysisCSharpDllFileName  = "Microsoft.CodeAnalysis.CSharp.dll"
	compilerExecArgument           = "exec"
	multiLevelLookupSetting        = "DOTNET_MULTILEVEL_LOOKUP=0"
	workerBuildTimeout             = 120 * time.Second
	buildFailureReportLimit        = 2000
	clrRuntimeHeaderDirectoryIndex = 14
	cacheDirectoryPermissions      = 0o755
	cacheFilePermissions           = 0o644
	temporaryDirectoryInfix        = ".tmp-"
)

// CommandRunner runs one process, so tests can stand in for dotnet. It has the same shape as the
// compile-check compiler runner, so the caller can hand the same function to both.
type CommandRunner func(ctx context.Context, cmd *exec.Cmd) (string, string, int, error)

// ToolchainPaths are the Editor's dotnet and Roslyn the worker is built with and runs on.
type ToolchainPaths struct {
	DotnetHostPath      string
	CompilerDllPath     string
	CompilerDirectory   string // holds Microsoft.CodeAnalysis*.dll and csc.runtimeconfig.json
	SharedFrameworkPath string
}

// WorkerPaths locate a built worker.
type WorkerPaths struct {
	// Key names the worker build: the same sources built with the same toolchain share it.
	Key            string
	DllPath        string
	DotnetHostPath string
}

// Bootstrap builds the worker once per source and toolchain and caches it under CacheRoot.
type Bootstrap struct {
	CompilerPaths ToolchainPaths
	CacheRoot     string
	Run           CommandRunner
}

// Ensure returns a built worker for the toolchain, building it when the cache has none. A build
// that failed once is not retried for the same sources and toolchain: it returns the recorded
// failure instead, so an environment the worker cannot build in pays the build only once.
func (b *Bootstrap) Ensure(ctx context.Context) (WorkerPaths, error) {
	key := b.computeKey()
	cacheDirectory := filepath.Join(b.CacheRoot, workerCacheDirectoryName)
	workerDirectory := filepath.Join(cacheDirectory, key)
	paths := WorkerPaths{
		Key:            key,
		DllPath:        filepath.Join(workerDirectory, workerDllFileName),
		DotnetHostPath: b.CompilerPaths.DotnetHostPath,
	}
	if isCompleteWorker(workerDirectory) {
		return paths, nil
	}

	if report, readErr := os.ReadFile(filepath.Join(workerDirectory, buildFailedFileName)); readErr == nil {
		return WorkerPaths{}, fmt.Errorf(
			"changed-files check worker failed to build earlier: %s", truncate(string(report), buildFailureReportLimit))
	}

	if err := os.MkdirAll(cacheDirectory, cacheDirectoryPermissions); err != nil {
		return WorkerPaths{}, fmt.Errorf("failed to create the worker cache directory: %w", err)
	}

	// Why a private temporary directory: runs that start together each build in their own, so none
	// reads another's half-written worker. The first to rename its directory into place wins.
	temporaryDirectory, err := temporaryBuildDirectory(cacheDirectory, key)
	if err != nil {
		return WorkerPaths{}, err
	}
	defer func() { _ = os.RemoveAll(temporaryDirectory) }()

	if err := b.build(ctx, temporaryDirectory, workerDirectory); err != nil {
		return WorkerPaths{}, err
	}

	if err := os.Rename(temporaryDirectory, workerDirectory); err != nil {
		if isCompleteWorker(workerDirectory) {
			return paths, nil
		}

		return WorkerPaths{}, fmt.Errorf("failed to move the built worker into the cache: %w", err)
	}

	return paths, nil
}

// build compiles the worker into temporaryDirectory, recording a failed compile in workerDirectory.
func (b *Bootstrap) build(ctx context.Context, temporaryDirectory string, workerDirectory string) error {
	sourcePaths, err := writeWorkerSources(filepath.Join(temporaryDirectory, workerSourceSubdirectory))
	if err != nil {
		return err
	}

	references, err := managedAssembliesIn(b.CompilerPaths.SharedFrameworkPath)
	if err != nil {
		return err
	}

	references = append(references,
		filepath.Join(b.CompilerPaths.CompilerDirectory, codeAnalysisDllFileName),
		filepath.Join(b.CompilerPaths.CompilerDirectory, codeAnalysisCSharpDllFileName))
	responseFilePath := filepath.Join(temporaryDirectory, workerResponseFileName)
	responseFile := workerResponseFile(filepath.Join(temporaryDirectory, workerDllFileName), references, sourcePaths)
	if err := os.WriteFile(responseFilePath, []byte(responseFile), cacheFilePermissions); err != nil {
		return fmt.Errorf("failed to write the worker response file: %w", err)
	}

	invocationContext, cancel := context.WithTimeout(ctx, workerBuildTimeout)
	defer cancel()

	command := exec.CommandContext(invocationContext,
		b.CompilerPaths.DotnetHostPath, compilerExecArgument, b.CompilerPaths.CompilerDllPath, "@"+responseFilePath)
	command.Dir = temporaryDirectory
	command.Env = append(os.Environ(), multiLevelLookupSetting)
	stdout, stderr, exitCode, runErr := b.Run(invocationContext, command)
	// Why a stopped build leaves no failure record: it says nothing about whether the worker builds
	// with this toolchain, and recording it would turn the check off until the sources change.
	if contextErr := invocationContext.Err(); contextErr != nil {
		return fmt.Errorf("the changed-files check worker build was stopped: %w", contextErr)
	}

	// Why only a compiler that ran is recorded: dotnet failing to start (too many processes or open
	// files) says nothing about whether the worker builds, and recording it would turn the check off
	// until the sources change.
	if runErr != nil && !isExitError(runErr) {
		return fmt.Errorf("failed to start the changed-files check worker build: %w: %s", runErr, strings.TrimSpace(stderr))
	}

	if runErr != nil || exitCode != 0 {
		return recordBuildFailure(workerDirectory, stdout+stderr, runErr)
	}

	return finishWorkerDirectory(temporaryDirectory, b.CompilerPaths.CompilerDirectory)
}

// computeKey hashes the embedded sources and the toolchain paths: a change to either is a new worker.
func (b *Bootstrap) computeKey() string {
	sources := readWorkerSources()
	hash := sha256.New()
	for _, source := range sources {
		_, _ = fmt.Fprintf(hash, "source\x00%s\x00%d\x00", source.name, len(source.content))
		_, _ = hash.Write(source.content)
	}

	for _, toolchainPath := range []string{
		b.CompilerPaths.CompilerDllPath,
		b.CompilerPaths.DotnetHostPath,
		b.CompilerPaths.SharedFrameworkPath,
		b.CompilerPaths.CompilerDirectory,
	} {
		_, _ = fmt.Fprintf(hash, "toolchain\x00%s\n", toolchainPath)
	}

	return hex.EncodeToString(hash.Sum(nil))
}

// workerSource is one embedded worker source file.
type workerSource struct {
	name    string
	content []byte
}

// readWorkerSources returns the embedded worker sources ordered by file name. Why it panics instead
// of returning an error: the sources are compiled into this binary, so failing to read them is a
// broken build, not a condition a run can recover from.
func readWorkerSources() []workerSource {
	entries, err := fs.ReadDir(workerSources, workerSourceDirectory)
	if err != nil {
		panic(fmt.Sprintf("embedded worker sources are unreadable: %v", err))
	}

	sources := make([]workerSource, 0, len(entries))
	for _, entry := range entries {
		content, err := workerSources.ReadFile(path.Join(workerSourceDirectory, entry.Name()))
		if err != nil {
			panic(fmt.Sprintf("embedded worker source %s is unreadable: %v", entry.Name(), err))
		}

		sources = append(sources, workerSource{name: entry.Name(), content: content})
	}

	sort.Slice(sources, func(i, j int) bool { return sources[i].name < sources[j].name })
	return sources
}

func writeWorkerSources(sourceDirectory string) ([]string, error) {
	sources := readWorkerSources()
	if err := os.MkdirAll(sourceDirectory, cacheDirectoryPermissions); err != nil {
		return nil, fmt.Errorf("failed to create the worker source directory: %w", err)
	}

	sourcePaths := make([]string, 0, len(sources))
	for _, source := range sources {
		sourcePath := filepath.Join(sourceDirectory, source.name)
		if err := os.WriteFile(sourcePath, source.content, cacheFilePermissions); err != nil {
			return nil, fmt.Errorf("failed to write the worker source %s: %w", source.name, err)
		}

		sourcePaths = append(sourcePaths, sourcePath)
	}

	return sourcePaths, nil
}

// managedAssembliesIn lists the shared framework DLLs csc can reference. Why native images are left
// out: the framework directory also holds native PE files named *.dll, and csc rejects them (CS0009).
func managedAssembliesIn(sharedFrameworkPath string) ([]string, error) {
	matches, err := filepath.Glob(filepath.Join(sharedFrameworkPath, "*.dll"))
	if err != nil {
		return nil, fmt.Errorf("failed to list the shared framework: %w", err)
	}

	sort.Strings(matches)
	managed := make([]string, 0, len(matches))
	for _, candidate := range matches {
		if isManagedAssembly(candidate) {
			managed = append(managed, candidate)
		}
	}

	return managed, nil
}

// isManagedAssembly reports whether the PE file has a CLR runtime header.
func isManagedAssembly(filePath string) bool {
	file, err := pe.Open(filePath)
	if err != nil {
		return false
	}
	defer func() { _ = file.Close() }()

	if header, is64 := file.OptionalHeader.(*pe.OptionalHeader64); is64 {
		return hasRuntimeHeader(header.NumberOfRvaAndSizes, header.DataDirectory)
	}

	header, is32 := file.OptionalHeader.(*pe.OptionalHeader32)
	return is32 && hasRuntimeHeader(header.NumberOfRvaAndSizes, header.DataDirectory)
}

func hasRuntimeHeader(directoryCount uint32, directories [16]pe.DataDirectory) bool {
	return directoryCount > clrRuntimeHeaderDirectoryIndex &&
		directories[clrRuntimeHeaderDirectoryIndex].VirtualAddress != 0
}

func workerResponseFile(outputPath string, references []string, sourcePaths []string) string {
	lines := []string{"-nologo", "-nostdlib+", "-target:exe", "-out:" + quote(outputPath)}
	for _, reference := range references {
		lines = append(lines, "-r:"+quote(reference))
	}

	for _, sourcePath := range sourcePaths {
		lines = append(lines, quote(sourcePath))
	}

	return strings.Join(lines, "\n") + "\n"
}

func quote(value string) string {
	return `"` + value + `"`
}

// finishWorkerDirectory adds the files the built worker needs to start next to worker.dll.
func finishWorkerDirectory(temporaryDirectory string, compilerDirectory string) error {
	// Why csc's runtime config: it names the framework the Editor's Roslyn runs on, which is the
	// one the worker was compiled against.
	runtimeConfig, err := os.ReadFile(filepath.Join(compilerDirectory, compilerRuntimeConfigFileName))
	if err != nil {
		return fmt.Errorf("failed to read the compiler runtime config: %w", err)
	}

	if err := os.WriteFile(
		filepath.Join(temporaryDirectory, workerRuntimeConfigFileName), runtimeConfig, cacheFilePermissions); err != nil {
		return fmt.Errorf("failed to write the worker runtime config: %w", err)
	}

	// UTF-8 without a BOM: the worker reads the directory back from this file to load Roslyn.
	if err := os.WriteFile(
		filepath.Join(temporaryDirectory, roslynDirectorySidecarFileName),
		[]byte(compilerDirectory+"\n"), cacheFilePermissions); err != nil {
		return fmt.Errorf("failed to write the Roslyn directory sidecar: %w", err)
	}

	return nil
}

func recordBuildFailure(workerDirectory string, output string, runErr error) error {
	report := strings.TrimSpace(output)
	if report == "" && runErr != nil {
		report = runErr.Error()
	}

	if err := os.MkdirAll(workerDirectory, cacheDirectoryPermissions); err != nil {
		return fmt.Errorf("failed to record the worker build failure: %w", err)
	}

	if err := os.WriteFile(
		filepath.Join(workerDirectory, buildFailedFileName), []byte(report), cacheFilePermissions); err != nil {
		return fmt.Errorf("failed to record the worker build failure: %w", err)
	}

	return fmt.Errorf("changed-files check worker failed to build: %s", truncate(report, buildFailureReportLimit))
}

func isCompleteWorker(workerDirectory string) bool {
	for _, name := range []string{workerDllFileName, workerRuntimeConfigFileName, roslynDirectorySidecarFileName} {
		if _, err := os.Stat(filepath.Join(workerDirectory, name)); err != nil {
			return false
		}
	}

	return true
}

func temporaryBuildDirectory(cacheDirectory string, key string) (string, error) {
	directory, err := os.MkdirTemp(cacheDirectory, key+temporaryDirectoryInfix+"*")
	if err != nil {
		return "", fmt.Errorf("failed to create the worker build directory: %w", err)
	}

	return directory, nil
}

func truncate(text string, limit int) string {
	if len(text) <= limit {
		return text
	}

	return text[:limit]
}

// isExitError reports whether the process ran and exited with a non-zero code, as opposed to not starting.
func isExitError(err error) bool {
	var exitError *exec.ExitError
	return errors.As(err, &exitError)
}

// errWorkerNotBuilt is returned by callers that need a worker but were given none.
var errWorkerNotBuilt = errors.New("changed-files check worker is not built")
