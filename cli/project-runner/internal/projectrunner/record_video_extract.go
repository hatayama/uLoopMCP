package projectrunner

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"io"
	"os"
	"os/exec"
	"path/filepath"

	clierrors "github.com/hatayama/unity-cli-loop/common/errors"

	"github.com/hatayama/unity-cli-loop/common/clicore"
	"github.com/hatayama/unity-cli-loop/common/tooldocs"
	"github.com/hatayama/unity-cli-loop/common/unityipc"
)

const (
	recordVideoFfmpegNotFoundErrorCode = "FFMPEG_NOT_FOUND"
	recordVideoFfmpegFailedErrorCode   = "FFMPEG_FAILED"

	recordVideoFfmpegStderrLimit = 2000
)

// Injection points so tests can drive extraction without ffmpeg installed.
var (
	lookPathForRecordVideo  = exec.LookPath
	runFfmpegForRecordVideo = runFfmpegDefault
)

type recordVideoExtractResult struct {
	Success   bool                      `json:"Success"`
	Video     string                    `json:"Video"`
	FramePath string                    `json:"FramePath,omitempty"`
	SheetPath string                    `json:"SheetPath,omitempty"`
	Columns   int                       `json:"Columns,omitempty"`
	TileWidth int                       `json:"TileWidth,omitempty"`
	Frames    []recordVideoExtractFrame `json:"Frames"`
}

// recordVideoExtractPlan is one ffmpeg run and what it reports when the image is written.
type recordVideoExtractPlan struct {
	args   []string
	output string
	result recordVideoExtractResult
}

// runRecordVideoExtract cuts one frame or a sheet of a range out of a finished recording with
// ffmpeg. It never contacts Unity, so it works while the Editor is closed or busy.
func runRecordVideoExtract(
	ctx context.Context,
	connection unityipc.Connection,
	options recordVideoExtractOptions,
	startPath string,
	stdout io.Writer,
	stderr io.Writer,
) int {
	ffmpegPath, err := lookPathForRecordVideo("ffmpeg")
	if err != nil {
		clierrors.WriteErrorEnvelope(stderr, recordVideoExtractError(connection, recordVideoFfmpegNotFoundErrorCode,
			"ffmpeg is not installed; the sheets and numbers written when the recording stopped are still available beside the video.",
			"Install ffmpeg and put it on PATH, or read the timeline and burst sheets and frames.json beside the video.", ""))
		return 1
	}

	video := options.video
	if !filepath.IsAbs(video) {
		video = filepath.Join(startPath, video)
	}
	captures, err := readRecordVideoCaptures(video)
	if err != nil {
		clierrors.WriteClassifiedError(stderr, err, recordVideoErrorContext(connection))
		return 1
	}

	plan := planRecordVideoExtract(options, video, captures)
	// A file left by an earlier run with the same times would otherwise be reported as this run's image.
	if err := os.Remove(plan.output); err != nil && !errors.Is(err, os.ErrNotExist) {
		clierrors.WriteClassifiedError(stderr, err, recordVideoErrorContext(connection))
		return 1
	}
	stderrTail, err := runFfmpegForRecordVideo(ctx, ffmpegPath, plan.args)
	if err != nil {
		clierrors.WriteErrorEnvelope(stderr, recordVideoExtractError(connection, recordVideoFfmpegFailedErrorCode,
			"ffmpeg failed: "+err.Error(), "Read Details.Stderr; check that the video plays and the times are inside it.", stderrTail))
		return 1
	}
	if _, err := os.Stat(plan.output); err != nil {
		clierrors.WriteErrorEnvelope(stderr, recordVideoExtractError(connection, recordVideoFfmpegFailedErrorCode,
			"ffmpeg wrote no image; the time is probably past the end of the video.",
			"Pass a time inside the recording; ElapsedSeconds of the stop response is its length.", stderrTail))
		return 1
	}

	encoded, err := json.Marshal(plan.result)
	if err != nil {
		clierrors.WriteClassifiedError(stderr, err, recordVideoErrorContext(connection))
		return 1
	}
	clicore.WriteJSON(stdout, encoded)
	return 0
}

// runFfmpegDefault runs ffmpeg by the absolute path LookPath returned, so a binary in the current
// directory is never picked up, and returns the end of its stderr for the error report.
func runFfmpegDefault(ctx context.Context, ffmpegPath string, args []string) (string, error) {
	var stderr bytes.Buffer
	command := exec.CommandContext(ctx, ffmpegPath, args...)
	command.Stdout = io.Discard
	command.Stderr = &stderr
	err := command.Run()
	tail := stderr.String()
	if len(tail) > recordVideoFfmpegStderrLimit {
		tail = tail[len(tail)-recordVideoFfmpegStderrLimit:]
	}
	return tail, err
}

func recordVideoExtractError(connection unityipc.Connection, code string, message string, nextAction string, stderrTail string) clierrors.CLIError {
	cliError := clierrors.CLIError{
		ErrorCode:   code,
		Phase:       clierrors.ErrorPhaseExecution,
		Message:     message,
		ProjectRoot: connection.ProjectRoot,
		Command:     tooldocs.RecordVideoCommandName,
		NextActions: []string{nextAction},
	}
	if stderrTail != "" {
		cliError.Details = map[string]any{"Stderr": stderrTail}
	}
	return cliError
}
