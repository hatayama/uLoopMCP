package fastcheck

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"os"
	"os/exec"
	"strings"
	"time"
)

const (
	fingerprintCommandName = "fingerprint"
	checkCommandName       = "check"
	compareSurfaceCommand  = "compare-surface"
	defaultWorkerTimeout   = 60 * time.Second
	requestFileSuffix      = ".in.json"
	responseFileSuffix     = ".out.json"
	requestFilePattern     = "fastcheck-*" + requestFileSuffix

	// CheckedOutcome means the changed files were checked and the diagnostics are complete for them.
	CheckedOutcome = "Checked"
	// DeclarationChangedOutcome means a changed file's declarations differ from the baseline.
	DeclarationChangedOutcome = "DeclarationChanged"
	// UnsupportedOutcome means the assembly uses something the changed-files check cannot handle.
	UnsupportedOutcome = "Unsupported"

	// SameSurfaceOutcome means every assembly compiling against either reference assembly sees the
	// same API.
	SameSurfaceOutcome = "same"
	// DifferentSurfaceOutcome means the two reference assemblies may show a dependent a different API,
	// including when the worker could not read one of them.
	DifferentSurfaceOutcome = "different"
)

// The request and response types mirror the worker's JSON contract field for field. Field names
// stay PascalCase in JSON because the worker reads and writes them that way.

// FingerprintRequest asks for the declaration fingerprint and content hash of each source.
type FingerprintRequest struct {
	ProjectRoot      string
	ResponseFilePath string   // absolute
	Sources          []string // absolute
}

// FingerprintResponse holds, per absolute source path, the declaration fingerprint and the sha256
// of the exact bytes it was computed from.
type FingerprintResponse struct {
	Fingerprints  map[string]string
	ContentHashes map[string]string
}

// CheckRequest asks to check only the changed files of an assembly.
type CheckRequest struct {
	ProjectRoot          string
	ResponseFilePath     string            // absolute
	ChangedFiles         []string          // absolute
	BaselineFingerprints map[string]string // keyed by absolute path
	GeneratedDirectory   string            // absolute, or "" when there is no saved generator output
}

// CheckResponse is the outcome of a changed-files check.
type CheckResponse struct {
	Outcome            string
	Reason             string // why, when Outcome is not CheckedOutcome
	Diagnostics        []WorkerDiagnostic
	UncheckedCodes     []string // sorted, unique
	GeneratorsNotRerun bool
}

// CompareSurfaceRequest asks whether two reference assemblies of one assembly show its dependents
// the same API.
type CompareSurfaceRequest struct {
	ProducedPath   string // absolute
	UnityBuildPath string // absolute
}

// CompareSurfaceResponse is the answer of one surface comparison.
type CompareSurfaceResponse struct {
	Outcome string
	Detail  string // the first line of the two descriptions that differs, or why one could not be read
}

// WorkerDiagnostic is one error or warning in a changed file, with a 1-based line and column.
type WorkerDiagnostic struct {
	File     string // absolute
	Line     int
	Column   int
	Severity string // "error" or "warning"
	Code     string
	Message  string
}

// Client runs one request at a time through a built worker.
type Client struct {
	Worker WorkerPaths
	// TempDirectory receives the request and response files of each call; they are removed after it.
	TempDirectory string
	Run           CommandRunner
	// Timeout bounds each call; zero means one minute.
	Timeout time.Duration
}

// Fingerprint fingerprints every source of an assembly.
func (c Client) Fingerprint(ctx context.Context, request FingerprintRequest) (FingerprintResponse, error) {
	response := FingerprintResponse{}
	if err := c.invoke(ctx, fingerprintCommandName, request, &response); err != nil {
		return FingerprintResponse{}, err
	}

	return response, nil
}

// Check checks only the changed files of an assembly.
func (c Client) Check(ctx context.Context, request CheckRequest) (CheckResponse, error) {
	response := CheckResponse{}
	if err := c.invoke(ctx, checkCommandName, request, &response); err != nil {
		return CheckResponse{}, err
	}

	// Rejected here so callers can rely on the three outcomes the worker defines.
	switch response.Outcome {
	case CheckedOutcome, DeclarationChangedOutcome, UnsupportedOutcome:
		return response, nil
	default:
		return CheckResponse{}, fmt.Errorf("changed-files check worker returned an unknown outcome %q", response.Outcome)
	}
}

// CompareSurface asks whether two reference assemblies show their dependents the same API.
func (c Client) CompareSurface(ctx context.Context, request CompareSurfaceRequest) (CompareSurfaceResponse, error) {
	response := CompareSurfaceResponse{}
	if err := c.invoke(ctx, compareSurfaceCommand, request, &response); err != nil {
		return CompareSurfaceResponse{}, err
	}

	// Rejected here so an answer the worker does not define is never read as either.
	switch response.Outcome {
	case SameSurfaceOutcome, DifferentSurfaceOutcome:
		return response, nil
	default:
		return CompareSurfaceResponse{}, fmt.Errorf("changed-files check worker returned an unknown outcome %q", response.Outcome)
	}
}

// invoke writes the request, runs the worker command, and reads its response into response.
func (c Client) invoke(ctx context.Context, command string, request any, response any) error {
	if c.Worker.DllPath == "" {
		return errWorkerNotBuilt
	}

	requestPath, err := writeRequestFile(c.TempDirectory, request)
	if err != nil {
		return err
	}
	defer func() { _ = os.Remove(requestPath) }()

	// Why the response path is derived from the request path: the request file name is already
	// unique, so the pair never collides with another call sharing the directory.
	responsePath := strings.TrimSuffix(requestPath, requestFileSuffix) + responseFileSuffix
	defer func() { _ = os.Remove(responsePath) }()

	invocationContext, cancel := context.WithTimeout(ctx, c.timeout())
	defer cancel()

	cmd := exec.CommandContext(invocationContext,
		c.Worker.DotnetHostPath, compilerExecArgument, c.Worker.DllPath, command, requestPath, responsePath)
	cmd.Env = append(os.Environ(), multiLevelLookupSetting)
	_, stderr, exitCode, runErr := c.Run(invocationContext, cmd)
	if contextErr := invocationContext.Err(); contextErr != nil {
		return fmt.Errorf("changed-files check worker %s did not finish: %w", command, contextErr)
	}

	if runErr != nil || exitCode != 0 {
		return fmt.Errorf("changed-files check worker %s failed (exit code %d): %s",
			command, exitCode, workerFailureDetail(stderr, runErr))
	}

	content, err := os.ReadFile(responsePath)
	if err != nil {
		return fmt.Errorf("failed to read the changed-files check worker response: %w", err)
	}

	if err := json.Unmarshal(content, response); err != nil {
		return fmt.Errorf("failed to parse the changed-files check worker response: %w", err)
	}

	return nil
}

func (c Client) timeout() time.Duration {
	if c.Timeout <= 0 {
		return defaultWorkerTimeout
	}

	return c.Timeout
}

func writeRequestFile(directory string, request any) (string, error) {
	file, err := os.CreateTemp(directory, requestFilePattern)
	if err != nil {
		return "", fmt.Errorf("failed to create the changed-files check request file: %w", err)
	}

	encodeErr := json.NewEncoder(file).Encode(request)
	closeErr := file.Close()
	if err := errors.Join(encodeErr, closeErr); err != nil {
		_ = os.Remove(file.Name())
		return "", fmt.Errorf("failed to write the changed-files check request file: %w", err)
	}

	return file.Name(), nil
}

func workerFailureDetail(stderr string, runErr error) string {
	detail := strings.TrimSpace(stderr)
	if detail == "" && runErr != nil {
		detail = runErr.Error()
	}

	return truncate(detail, buildFailureReportLimit)
}
