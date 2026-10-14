package fastcheck

import (
	"bytes"
	"context"
	"debug/pe"
	"encoding/binary"
	"errors"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strings"
	"testing"
)

const testRuntimeConfig = `{"runtimeOptions":{"tfm":"net6.0"}}`

// fakeCompiler stands in for csc: it records each response file and, when it succeeds, writes the
// worker DLL to the -out path the response file names.
type fakeCompiler struct {
	calls         int
	responseFiles []string
	exitCode      int
	output        string
}

func (f *fakeCompiler) run(_ context.Context, cmd *exec.Cmd) (string, string, int, error) {
	f.calls++
	responseFilePath := strings.TrimPrefix(cmd.Args[len(cmd.Args)-1], "@")
	content, err := os.ReadFile(responseFilePath)
	if err != nil {
		return "", err.Error(), 1, err
	}

	f.responseFiles = append(f.responseFiles, string(content))
	if f.exitCode != 0 {
		return f.output, "", f.exitCode, exitErrorWithCode(f.exitCode)
	}

	for _, line := range strings.Split(string(content), "\n") {
		if strings.HasPrefix(line, "-out:") {
			outputPath := strings.Trim(strings.TrimPrefix(line, "-out:"), `"`)
			if err := os.WriteFile(outputPath, []byte("worker"), cacheFilePermissions); err != nil {
				return "", err.Error(), 1, err
			}
		}
	}

	return f.output, "", 0, nil
}

// newTestToolchain lays out a compiler directory and an empty shared framework under root.
func newTestToolchain(t *testing.T, root string, name string) ToolchainPaths {
	t.Helper()
	compilerDirectory := filepath.Join(root, name, "Roslyn")
	sharedFramework := filepath.Join(root, name, "shared")
	for _, directory := range []string{compilerDirectory, sharedFramework} {
		if err := os.MkdirAll(directory, cacheDirectoryPermissions); err != nil {
			t.Fatalf("failed to create %s: %v", directory, err)
		}
	}

	writeTestFile(t, filepath.Join(compilerDirectory, compilerRuntimeConfigFileName), []byte(testRuntimeConfig))
	return ToolchainPaths{
		DotnetHostPath:      filepath.Join(root, name, "dotnet"),
		CompilerDllPath:     filepath.Join(compilerDirectory, "csc.dll"),
		CompilerDirectory:   compilerDirectory,
		SharedFrameworkPath: sharedFramework,
	}
}

func writeTestFile(t *testing.T, filePath string, content []byte) {
	t.Helper()
	if err := os.WriteFile(filePath, content, cacheFilePermissions); err != nil {
		t.Fatalf("failed to write %s: %v", filePath, err)
	}
}

// peImage builds an AMD64 PE32+ image with headers only. clrHeaderAddress is the CLR runtime header
// directory entry: non-zero for a managed assembly, zero for a native image.
func peImage(t *testing.T, clrHeaderAddress uint32) []byte {
	t.Helper()
	return peImageForMachine(t, pe.IMAGE_FILE_MACHINE_AMD64, clrHeaderAddress)
}

// peImageForMachine builds a PE32+ image with headers only for the given COFF machine value.
func peImageForMachine(t *testing.T, machine uint16, clrHeaderAddress uint32) []byte {
	t.Helper()
	const peHeaderOffset = 0x40
	buffer := bytes.Buffer{}
	dosHeader := make([]byte, peHeaderOffset)
	copy(dosHeader, "MZ")
	binary.LittleEndian.PutUint32(dosHeader[0x3c:], peHeaderOffset)
	buffer.Write(dosHeader)
	buffer.WriteString("PE\x00\x00")

	optionalHeader := pe.OptionalHeader64{Magic: 0x20b, NumberOfRvaAndSizes: 16}
	optionalHeader.DataDirectory[clrRuntimeHeaderDirectoryIndex] = pe.DataDirectory{VirtualAddress: clrHeaderAddress, Size: 72}
	fileHeader := pe.FileHeader{
		Machine:              machine,
		SizeOfOptionalHeader: uint16(binary.Size(optionalHeader)),
		Characteristics:      pe.IMAGE_FILE_DLL,
	}
	for _, header := range []any{fileHeader, optionalHeader} {
		if err := binary.Write(&buffer, binary.LittleEndian, header); err != nil {
			t.Fatalf("failed to build the PE image: %v", err)
		}
	}

	return buffer.Bytes()
}

// peImage32 builds a PE32 image with headers only and a CLR runtime header, like a 32-bit managed assembly.
func peImage32(t *testing.T) []byte {
	t.Helper()
	const peHeaderOffset = 0x40
	buffer := bytes.Buffer{}
	dosHeader := make([]byte, peHeaderOffset)
	copy(dosHeader, "MZ")
	binary.LittleEndian.PutUint32(dosHeader[0x3c:], peHeaderOffset)
	buffer.Write(dosHeader)
	buffer.WriteString("PE\x00\x00")

	optionalHeader := pe.OptionalHeader32{Magic: 0x10b, NumberOfRvaAndSizes: 16}
	optionalHeader.DataDirectory[clrRuntimeHeaderDirectoryIndex] = pe.DataDirectory{VirtualAddress: 0x2008, Size: 72}
	fileHeader := pe.FileHeader{
		Machine:              pe.IMAGE_FILE_MACHINE_I386,
		SizeOfOptionalHeader: uint16(binary.Size(optionalHeader)),
		Characteristics:      pe.IMAGE_FILE_DLL,
	}
	for _, header := range []any{fileHeader, optionalHeader} {
		if err := binary.Write(&buffer, binary.LittleEndian, header); err != nil {
			t.Fatalf("failed to build the PE image: %v", err)
		}
	}

	return buffer.Bytes()
}

// Verifies that the worker is built once and the second Ensure reuses the cache without running csc.
func TestEnsureBuildsOnceAndReusesCache(t *testing.T) {
	root := t.TempDir()
	compiler := &fakeCompiler{}
	bootstrap := Bootstrap{CompilerPaths: newTestToolchain(t, root, "editor"), CacheRoot: filepath.Join(root, "cache"), Run: compiler.run}

	first, err := bootstrap.Ensure(context.Background())
	if err != nil {
		t.Fatalf("expected the first Ensure to build the worker, got error: %v", err)
	}

	second, err := bootstrap.Ensure(context.Background())
	if err != nil {
		t.Fatalf("expected the second Ensure to reuse the worker, got error: %v", err)
	}

	if compiler.calls != 1 {
		t.Fatalf("expected csc to run once, ran %d times", compiler.calls)
	}

	if first != second {
		t.Fatalf("expected both calls to return the same worker, got %+v and %+v", first, second)
	}

	workerDirectory := filepath.Dir(first.DllPath)
	sidecar, err := os.ReadFile(filepath.Join(workerDirectory, roslynDirectorySidecarFileName))
	if err != nil || string(sidecar) != bootstrap.CompilerPaths.CompilerDirectory+"\n" {
		t.Fatalf("expected the Roslyn sidecar to name the compiler directory, got %q (%v)", sidecar, err)
	}

	runtimeConfig, err := os.ReadFile(filepath.Join(workerDirectory, workerRuntimeConfigFileName))
	if err != nil || string(runtimeConfig) != testRuntimeConfig {
		t.Fatalf("expected the compiler runtime config next to the worker, got %q (%v)", runtimeConfig, err)
	}
}

// Verifies that a failed build is recorded, and the next Ensure reports it without running csc again.
func TestEnsureRecordsBuildFailureAndDoesNotRetry(t *testing.T) {
	root := t.TempDir()
	compiler := &fakeCompiler{exitCode: 1, output: "Program.cs(1,1): error CS1002: ; expected"}
	bootstrap := Bootstrap{CompilerPaths: newTestToolchain(t, root, "editor"), CacheRoot: filepath.Join(root, "cache"), Run: compiler.run}

	_, firstErr := bootstrap.Ensure(context.Background())
	if firstErr == nil || !strings.Contains(firstErr.Error(), "CS1002") {
		t.Fatalf("expected the build failure with the compiler output, got %v", firstErr)
	}

	_, secondErr := bootstrap.Ensure(context.Background())
	if secondErr == nil || !strings.Contains(secondErr.Error(), "failed to build earlier") ||
		!strings.Contains(secondErr.Error(), "CS1002") {
		t.Fatalf("expected the recorded failure, got %v", secondErr)
	}

	if compiler.calls != 1 {
		t.Fatalf("expected csc to run once, ran %d times", compiler.calls)
	}
}

// Verifies that a different toolchain path names a different worker build, so each is built separately.
func TestEnsureKeyChangesWithToolchainPath(t *testing.T) {
	root := t.TempDir()
	compiler := &fakeCompiler{}
	cacheRoot := filepath.Join(root, "cache")
	older := Bootstrap{CompilerPaths: newTestToolchain(t, root, "editor-a"), CacheRoot: cacheRoot, Run: compiler.run}
	newer := Bootstrap{CompilerPaths: newTestToolchain(t, root, "editor-b"), CacheRoot: cacheRoot, Run: compiler.run}

	olderWorker, err := older.Ensure(context.Background())
	if err != nil {
		t.Fatalf("expected the first toolchain to build, got error: %v", err)
	}

	newerWorker, err := newer.Ensure(context.Background())
	if err != nil {
		t.Fatalf("expected the second toolchain to build, got error: %v", err)
	}

	if olderWorker.Key == newerWorker.Key || compiler.calls != 2 {
		t.Fatalf("expected two builds under different keys, got keys %s and %s after %d builds",
			olderWorker.Key, newerWorker.Key, compiler.calls)
	}
}

// Verifies that only managed assemblies of the shared framework are passed to csc as references.
func TestEnsureSkipsNativeImagesInSharedFramework(t *testing.T) {
	root := t.TempDir()
	compiler := &fakeCompiler{}
	toolchain := newTestToolchain(t, root, "editor")
	writeTestFile(t, filepath.Join(toolchain.SharedFrameworkPath, "System.Runtime.dll"), peImage(t, 0x2008))
	writeTestFile(t, filepath.Join(toolchain.SharedFrameworkPath, "System.Private.Uri.dll"), peImage32(t))
	// ReadyToRun images of the macOS shared framework XOR the machine with an OS mask: ARM64 reads
	// 0xEC20 and AMD64 reads 0xC020.
	writeTestFile(t, filepath.Join(toolchain.SharedFrameworkPath, "System.Private.CoreLib.dll"), peImageForMachine(t, 0xEC20, 0x2008))
	writeTestFile(t, filepath.Join(toolchain.SharedFrameworkPath, "System.Collections.Immutable.dll"), peImageForMachine(t, 0xC020, 0x2008))
	writeTestFile(t, filepath.Join(toolchain.SharedFrameworkPath, "NativeHelper.dll"), peImage(t, 0))
	writeTestFile(t, filepath.Join(toolchain.SharedFrameworkPath, "NativeMasked.dll"), peImageForMachine(t, 0xEC20, 0))
	writeTestFile(t, filepath.Join(toolchain.SharedFrameworkPath, "NotAnImage.dll"), []byte("text"))
	bootstrap := Bootstrap{CompilerPaths: toolchain, CacheRoot: filepath.Join(root, "cache"), Run: compiler.run}

	if _, err := bootstrap.Ensure(context.Background()); err != nil {
		t.Fatalf("expected the worker to build, got error: %v", err)
	}

	responseFile := compiler.responseFiles[0]
	for _, managed := range []string{
		"System.Runtime.dll", "System.Private.Uri.dll", "System.Private.CoreLib.dll", "System.Collections.Immutable.dll",
	} {
		if !strings.Contains(responseFile, managed) {
			t.Fatalf("expected the managed assembly %s as a reference, got:\n%s", managed, responseFile)
		}
	}

	for _, rejected := range []string{"NativeHelper.dll", "NativeMasked.dll", "NotAnImage.dll"} {
		if strings.Contains(responseFile, rejected) {
			t.Fatalf("expected %s to be left out, got:\n%s", rejected, responseFile)
		}
	}

	for _, roslynAssembly := range []string{codeAnalysisDllFileName, codeAnalysisCSharpDllFileName} {
		if !strings.Contains(responseFile, quote(filepath.Join(toolchain.CompilerDirectory, roslynAssembly))) {
			t.Fatalf("expected %s as a reference, got:\n%s", roslynAssembly, responseFile)
		}
	}
}

// Verifies that a failed build leaves no temporary build directory in the cache.
func TestEnsureLeavesNoTempDirectoryOnFailure(t *testing.T) {
	root := t.TempDir()
	compiler := &fakeCompiler{exitCode: 1, output: "error"}
	cacheRoot := filepath.Join(root, "cache")
	bootstrap := Bootstrap{CompilerPaths: newTestToolchain(t, root, "editor"), CacheRoot: cacheRoot, Run: compiler.run}

	if _, err := bootstrap.Ensure(context.Background()); err == nil {
		t.Fatal("expected the build to fail")
	}

	entries, err := os.ReadDir(filepath.Join(cacheRoot, workerCacheDirectoryName))
	if err != nil {
		t.Fatalf("failed to list the worker cache: %v", err)
	}

	for _, entry := range entries {
		if strings.Contains(entry.Name(), ".tmp-") {
			t.Fatalf("expected no temporary build directory, found %s", entry.Name())
		}
	}
}

// Verifies that a build stopped by its context records no failure, so the next run builds again.
func TestEnsureDoesNotRecordStoppedBuild(t *testing.T) {
	root := t.TempDir()
	ctx, cancel := context.WithCancel(context.Background())
	calls := 0
	bootstrap := Bootstrap{
		CompilerPaths: newTestToolchain(t, root, "editor"),
		CacheRoot:     filepath.Join(root, "cache"),
		Run: func(_ context.Context, _ *exec.Cmd) (string, string, int, error) {
			calls++
			cancel()
			return "", "", -1, context.Canceled
		},
	}

	_, err := bootstrap.Ensure(ctx)
	if err == nil || !strings.Contains(err.Error(), "was stopped") {
		t.Fatalf("expected a stopped build error, got %v", err)
	}

	compiler := &fakeCompiler{}
	bootstrap.Run = compiler.run
	if _, err := bootstrap.Ensure(context.Background()); err != nil {
		t.Fatalf("expected the next Ensure to build, got error: %v", err)
	}

	if calls != 1 || compiler.calls != 1 {
		t.Fatalf("expected one stopped and one completed build, got %d and %d", calls, compiler.calls)
	}
}

// Verifies that when another run put a complete worker in place first, Ensure uses that worker.
func TestEnsureUsesWorkerAnotherRunFinishedFirst(t *testing.T) {
	root := t.TempDir()
	compiler := &fakeCompiler{}
	bootstrap := Bootstrap{CompilerPaths: newTestToolchain(t, root, "editor"), CacheRoot: filepath.Join(root, "cache"), Run: nil}
	finalDirectory := filepath.Join(bootstrap.CacheRoot, workerCacheDirectoryName, bootstrap.computeKey(workerBuildRecipeVersion))
	bootstrap.Run = func(ctx context.Context, cmd *exec.Cmd) (string, string, int, error) {
		// The other run renames its directory into place while this one is still compiling.
		if err := os.MkdirAll(finalDirectory, cacheDirectoryPermissions); err != nil {
			return "", err.Error(), 1, err
		}

		for _, name := range []string{workerDllFileName, workerRuntimeConfigFileName, roslynDirectorySidecarFileName} {
			writeTestFile(t, filepath.Join(finalDirectory, name), []byte("other"))
		}

		return compiler.run(ctx, cmd)
	}

	worker, err := bootstrap.Ensure(context.Background())
	if err != nil {
		t.Fatalf("expected the other run's worker, got error: %v", err)
	}

	content, err := os.ReadFile(worker.DllPath)
	if err != nil || string(content) != "other" {
		t.Fatalf("expected the worker the other run built, got %q (%v)", content, err)
	}
}

// Verifies that a compiler directory without csc's runtime config fails the build without a cached worker.
func TestEnsureFailsWithoutCompilerRuntimeConfig(t *testing.T) {
	root := t.TempDir()
	compiler := &fakeCompiler{}
	toolchain := newTestToolchain(t, root, "editor")
	if err := os.Remove(filepath.Join(toolchain.CompilerDirectory, compilerRuntimeConfigFileName)); err != nil {
		t.Fatalf("failed to remove the runtime config: %v", err)
	}

	bootstrap := Bootstrap{CompilerPaths: toolchain, CacheRoot: filepath.Join(root, "cache"), Run: compiler.run}
	worker, err := bootstrap.Ensure(context.Background())
	if err == nil || !strings.Contains(err.Error(), "runtime config") {
		t.Fatalf("expected a missing runtime config error, got %v", err)
	}

	if isCompleteWorker(filepath.Dir(worker.DllPath)) {
		t.Fatal("expected no cached worker")
	}
}

// Verifies that a cache root that cannot hold directories is reported as an error.
func TestEnsureReportsUnusableCacheRoot(t *testing.T) {
	root := t.TempDir()
	cacheRoot := filepath.Join(root, "cache")
	writeTestFile(t, cacheRoot, []byte("not a directory"))
	compiler := &fakeCompiler{}
	bootstrap := Bootstrap{CompilerPaths: newTestToolchain(t, root, "editor"), CacheRoot: cacheRoot, Run: compiler.run}

	if _, err := bootstrap.Ensure(context.Background()); err == nil {
		t.Fatal("expected an error for a cache root that is a file")
	}

	if compiler.calls != 0 {
		t.Fatalf("expected csc not to run, ran %d times", compiler.calls)
	}
}

// Verifies that a build that fails without output records the run error, and that the report is cut
// to its limit.
func TestRecordBuildFailureFallsBackToRunErrorAndTruncates(t *testing.T) {
	workerDirectory := filepath.Join(t.TempDir(), "worker")
	err := recordBuildFailure(workerDirectory, "", errors.New("dotnet not found"))
	if err == nil || !strings.Contains(err.Error(), "dotnet not found") {
		t.Fatalf("expected the run error in the failure, got %v", err)
	}

	recorded, readErr := os.ReadFile(filepath.Join(workerDirectory, buildFailedFileName))
	if readErr != nil || string(recorded) != "dotnet not found" {
		t.Fatalf("expected the run error to be recorded, got %q (%v)", recorded, readErr)
	}

	long := strings.Repeat("x", buildFailureReportLimit+10)
	if got := truncate(long, buildFailureReportLimit); len(got) != buildFailureReportLimit {
		t.Fatalf("expected the report cut to %d bytes, got %d", buildFailureReportLimit, len(got))
	}
}

// Verifies that a leftover worker directory that is neither a worker nor a failure record is
// replaced by the built worker, and that the next Ensure reuses it without building.
func TestEnsureReplacesIncompleteWorkerDirectory(t *testing.T) {
	root := t.TempDir()
	compiler := &fakeCompiler{}
	bootstrap := Bootstrap{CompilerPaths: newTestToolchain(t, root, "editor"), CacheRoot: filepath.Join(root, "cache"), Run: compiler.run}
	finalDirectory := filepath.Join(bootstrap.CacheRoot, workerCacheDirectoryName, bootstrap.computeKey(workerBuildRecipeVersion))
	if err := os.MkdirAll(finalDirectory, cacheDirectoryPermissions); err != nil {
		t.Fatalf("failed to create the worker directory: %v", err)
	}

	writeTestFile(t, filepath.Join(finalDirectory, "leftover.txt"), []byte("leftover"))
	worker, err := bootstrap.Ensure(context.Background())
	if err != nil {
		t.Fatalf("expected the incomplete directory to be replaced, got error: %v", err)
	}

	if !isCompleteWorker(filepath.Dir(worker.DllPath)) {
		t.Fatalf("expected a complete worker at %s", worker.DllPath)
	}

	if _, err := os.Stat(filepath.Join(finalDirectory, "leftover.txt")); !os.IsNotExist(err) {
		t.Fatalf("expected the leftover file to be gone, got %v", err)
	}

	entries, err := os.ReadDir(filepath.Join(bootstrap.CacheRoot, workerCacheDirectoryName))
	if err != nil || len(entries) != 1 {
		t.Fatalf("expected only the worker directory in the cache, got %v (%v)", entries, err)
	}

	if _, err := bootstrap.Ensure(context.Background()); err != nil || compiler.calls != 1 {
		t.Fatalf("expected the replaced worker to be reused, got %v after %d builds", err, compiler.calls)
	}
}

// Verifies that a worker directory holding a failure record is not replaced by a build that
// finished later: the record is the answer for this key.
func TestEnsureKeepsFailureRecordAgainstLaterBuild(t *testing.T) {
	root := t.TempDir()
	compiler := &fakeCompiler{}
	bootstrap := Bootstrap{CompilerPaths: newTestToolchain(t, root, "editor"), CacheRoot: filepath.Join(root, "cache"), Run: nil}
	finalDirectory := filepath.Join(bootstrap.CacheRoot, workerCacheDirectoryName, bootstrap.computeKey(workerBuildRecipeVersion))
	bootstrap.Run = func(ctx context.Context, cmd *exec.Cmd) (string, string, int, error) {
		// Another run records a failure while this build is running.
		_ = recordBuildFailure(finalDirectory, "error CS0001", nil)
		return compiler.run(ctx, cmd)
	}

	if _, err := bootstrap.Ensure(context.Background()); err == nil || !strings.Contains(err.Error(), "failed to move") {
		t.Fatalf("expected the move to fail against the failure record, got %v", err)
	}

	if _, err := os.Stat(filepath.Join(finalDirectory, buildFailedFileName)); err != nil {
		t.Fatalf("expected the failure record to stay, got %v", err)
	}
}

// Verifies that the worker key changes with the build recipe version, so a failure recorded by an
// older recipe is not returned to a newer one.
func TestEnsureKeyChangesWithBuildRecipeVersion(t *testing.T) {
	root := t.TempDir()
	bootstrap := Bootstrap{CompilerPaths: newTestToolchain(t, root, "editor"), CacheRoot: filepath.Join(root, "cache")}

	if bootstrap.computeKey(1) == bootstrap.computeKey(2) {
		t.Fatal("expected different keys for different build recipe versions")
	}
}

// Verifies that a compiler killed by a signal records no failure, so the next run builds again.
func TestEnsureDoesNotRecordSignaledBuild(t *testing.T) {
	if runtime.GOOS == "windows" {
		t.Skip("a killed process on Windows reports an exit code, not a signal")
	}

	root := t.TempDir()
	killed := 0
	bootstrap := Bootstrap{
		CompilerPaths: newTestToolchain(t, root, "editor"),
		CacheRoot:     filepath.Join(root, "cache"),
		Run: func(_ context.Context, _ *exec.Cmd) (string, string, int, error) {
			killed++
			return "", "", -1, signaledExitError()
		},
	}

	_, err := bootstrap.Ensure(context.Background())
	if err == nil || !strings.Contains(err.Error(), "was killed") {
		t.Fatalf("expected a killed build error, got %v", err)
	}

	compiler := &fakeCompiler{}
	bootstrap.Run = compiler.run
	if _, err := bootstrap.Ensure(context.Background()); err != nil {
		t.Fatalf("expected the next Ensure to build, got error: %v", err)
	}

	if killed != 1 || compiler.calls != 1 {
		t.Fatalf("expected one killed and one completed build, got %d and %d", killed, compiler.calls)
	}
}

// Verifies that a PE file without an optional header is not taken for a managed assembly.
func TestIsManagedAssemblyRejectsImageWithoutOptionalHeader(t *testing.T) {
	image := peImage(t, 0x2008)
	const sizeOfOptionalHeaderOffset = 0x40 + 4 + 16
	binary.LittleEndian.PutUint16(image[sizeOfOptionalHeaderOffset:], 0)
	imagePath := filepath.Join(t.TempDir(), "NoOptionalHeader.dll")
	writeTestFile(t, imagePath, image[:0x40+4+binary.Size(pe.FileHeader{})])

	if isManagedAssembly(imagePath) {
		t.Fatal("expected an image without an optional header to be rejected")
	}
}

// Verifies that a failure that cannot be recorded is still reported as an error.
func TestRecordBuildFailureReportsUnwritableCache(t *testing.T) {
	blocker := filepath.Join(t.TempDir(), "file")
	writeTestFile(t, blocker, []byte("file"))

	err := recordBuildFailure(filepath.Join(blocker, "worker"), "error", nil)
	if err == nil || !strings.Contains(err.Error(), "failed to record") {
		t.Fatalf("expected a record failure, got %v", err)
	}
}

// Verifies that a cache directory where no build directory can be created is reported as an error.
func TestEnsureReportsUnwritableCacheDirectory(t *testing.T) {
	if runtime.GOOS == "windows" || os.Geteuid() == 0 {
		t.Skip("directory permissions do not stop this user from creating directories")
	}

	root := t.TempDir()
	cacheDirectory := filepath.Join(root, "cache", workerCacheDirectoryName)
	if err := os.MkdirAll(cacheDirectory, cacheDirectoryPermissions); err != nil {
		t.Fatalf("failed to create the cache directory: %v", err)
	}

	if err := os.Chmod(cacheDirectory, 0o500); err != nil {
		t.Fatalf("failed to make the cache directory read-only: %v", err)
	}
	t.Cleanup(func() { _ = os.Chmod(cacheDirectory, cacheDirectoryPermissions) })

	compiler := &fakeCompiler{}
	bootstrap := Bootstrap{CompilerPaths: newTestToolchain(t, root, "editor"), CacheRoot: filepath.Join(root, "cache"), Run: compiler.run}
	if _, err := bootstrap.Ensure(context.Background()); err == nil || !strings.Contains(err.Error(), "build directory") {
		t.Fatalf("expected a build directory error, got %v", err)
	}
}

// Verifies that dotnet failing to start records no failure, so the next run builds again.
func TestEnsureDoesNotRecordStartFailure(t *testing.T) {
	root := t.TempDir()
	starts := 0
	bootstrap := Bootstrap{
		CompilerPaths: newTestToolchain(t, root, "editor"),
		CacheRoot:     filepath.Join(root, "cache"),
		Run: func(_ context.Context, _ *exec.Cmd) (string, string, int, error) {
			starts++
			return "", "", 0, errors.New("fork/exec dotnet: resource temporarily unavailable")
		},
	}

	_, err := bootstrap.Ensure(context.Background())
	if err == nil || !strings.Contains(err.Error(), "failed to start") {
		t.Fatalf("expected a start failure, got %v", err)
	}

	compiler := &fakeCompiler{}
	bootstrap.Run = compiler.run
	if _, err := bootstrap.Ensure(context.Background()); err != nil {
		t.Fatalf("expected the next Ensure to build, got error: %v", err)
	}

	if starts != 1 || compiler.calls != 1 {
		t.Fatalf("expected one failed start and one build, got %d and %d", starts, compiler.calls)
	}
}

// Verifies that the CoreLib of every shared framework bundled with an installed Unity Editor is
// taken for a managed assembly. Skipped where no Editor is installed.
func TestIsManagedAssemblyAcceptsInstalledSharedFrameworkCoreLib(t *testing.T) {
	coreLibs, err := filepath.Glob(filepath.Join(
		"/Applications/Unity/Hub/Editor", "*", "Unity.app", "Contents", "Resources", "Scripting",
		"NetCoreRuntime", "shared", "Microsoft.NETCore.App", "*", "System.Private.CoreLib.dll"))
	if err != nil || len(coreLibs) == 0 {
		t.Skip("no Unity Editor with a bundled shared framework is installed here")
	}

	for _, coreLib := range coreLibs {
		if !isManagedAssembly(coreLib) {
			t.Errorf("expected %s to be a managed assembly", coreLib)
		}
	}
}

// Verifies that files too short or malformed to hold a CLR runtime header are not taken for managed
// assemblies: a truncated DOS header, a missing PE signature, and an unknown optional header magic.
func TestIsManagedAssemblyRejectsMalformedImages(t *testing.T) {
	const optionalHeaderOffset = 0x40 + 4 + 20
	noSignature := peImage(t, 0x2008)
	copy(noSignature[0x40:], "XX\x00\x00")
	unknownMagic := peImage(t, 0x2008)
	binary.LittleEndian.PutUint16(unknownMagic[optionalHeaderOffset:], 0x107)
	for name, image := range map[string][]byte{
		"Truncated.dll":    []byte("MZ"),
		"NoSignature.dll":  noSignature,
		"UnknownMagic.dll": unknownMagic,
	} {
		imagePath := filepath.Join(t.TempDir(), name)
		writeTestFile(t, imagePath, image)
		if isManagedAssembly(imagePath) {
			t.Errorf("expected %s to be rejected", name)
		}
	}
}
