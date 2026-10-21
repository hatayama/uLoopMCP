package projectrunner

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"math"
	"os"
	"os/exec"
	"path/filepath"
	"strconv"
	"strings"

	clierrors "github.com/hatayama/unity-cli-loop/common/errors"

	"github.com/hatayama/unity-cli-loop/common/clicore"
	"github.com/hatayama/unity-cli-loop/common/tooldocs"
	"github.com/hatayama/unity-cli-loop/common/unityipc"
)

const (
	recordVideoFfmpegNotFoundErrorCode = "FFMPEG_NOT_FOUND"
	recordVideoFfmpegFailedErrorCode   = "FFMPEG_FAILED"

	recordVideoExtractMaxTiles    = 40
	recordVideoExtractColumns     = 5
	recordVideoExtractTileWidth   = 320
	recordVideoExtractSheetFilter = "fps=%s,scale=%d:-2,tile=%dx%d:padding=2:color=0x282828"
	recordVideoAnalysisSuffix     = "_frames.json"
	recordVideoFfmpegStderrLimit  = 2000
)

// Injection points so tests can drive extraction without ffmpeg installed.
var (
	lookPathForRecordVideo  = exec.LookPath
	runFfmpegForRecordVideo = runFfmpegDefault
)

// recordVideoExtractFrame is the time, in seconds into the video, of one extracted image or tile.
type recordVideoExtractFrame struct {
	Seconds float64 `json:"Seconds"`
}

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
	frameRate, err := readRecordVideoExtractFrameRate(video)
	if err != nil {
		clierrors.WriteClassifiedError(stderr, err, recordVideoErrorContext(connection))
		return 1
	}

	plan := planRecordVideoExtract(options, video, frameRate)
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

// readRecordVideoExtractFrameRate reads the frame rate the recording was made at from the
// frames.json written beside it when it stopped.
func readRecordVideoExtractFrameRate(video string) (int, error) {
	info, err := os.Stat(video)
	if err != nil || info.IsDir() {
		return 0, recordVideoArgumentError("--"+tooldocs.RecordVideoVideoFlagName,
			"--video "+video+" does not exist or is not a file.",
			"Pass the OutputPath of the stop or one-shot response.")
	}
	content, err := os.ReadFile(recordVideoStem(video) + recordVideoAnalysisSuffix)
	if err != nil {
		return 0, recordVideoArgumentError("--"+tooldocs.RecordVideoVideoFlagName,
			"no frames.json beside the video; only recordings made by this version can be re-cut.",
			"Record again with this version, or open the video in a player.")
	}
	var analysis struct {
		FrameRate int `json:"FrameRate"`
	}
	if err := json.Unmarshal(content, &analysis); err != nil || analysis.FrameRate <= 0 {
		return 0, recordVideoArgumentError("--"+tooldocs.RecordVideoVideoFlagName,
			"the frames.json beside the video has no valid FrameRate.",
			"Record again with this version.")
	}
	return analysis.FrameRate, nil
}

func planRecordVideoExtract(options recordVideoExtractOptions, video string, frameRate int) recordVideoExtractPlan {
	stem := recordVideoStem(video)
	if options.hasFrame {
		output := fmt.Sprintf("%s_frame_%d.png", stem, recordVideoMilliseconds(options.frameAt))
		return recordVideoExtractPlan{
			args:   []string{"-y", "-ss", formatRecordVideoSeconds(options.frameAt), "-i", video, "-frames:v", "1", output},
			output: output,
			result: recordVideoExtractResult{
				Success:   true,
				Video:     video,
				FramePath: output,
				Frames:    []recordVideoExtractFrame{{Seconds: roundRecordVideoSeconds(options.frameAt)}},
			},
		}
	}

	duration := options.sheetTo - options.sheetFrom
	// The small epsilon keeps a range of a whole number of frames from rounding up to one more.
	count := int(math.Min(recordVideoExtractMaxTiles, math.Ceil(duration*float64(frameRate)-1e-9)))
	count = max(count, 1)
	rate := float64(count) / duration
	rows := (count + recordVideoExtractColumns - 1) / recordVideoExtractColumns
	output := fmt.Sprintf("%s_sheet_%d_%d.png", stem,
		recordVideoMilliseconds(options.sheetFrom), recordVideoMilliseconds(options.sheetTo))
	filter := fmt.Sprintf(recordVideoExtractSheetFilter,
		strconv.FormatFloat(rate, 'f', 6, 64), recordVideoExtractTileWidth, recordVideoExtractColumns, rows)
	frames := make([]recordVideoExtractFrame, count)
	for index := range frames {
		frames[index] = recordVideoExtractFrame{Seconds: roundRecordVideoSeconds(options.sheetFrom + float64(index)/rate)}
	}
	return recordVideoExtractPlan{
		args: []string{
			"-y", "-ss", formatRecordVideoSeconds(options.sheetFrom), "-t", formatRecordVideoSeconds(duration),
			"-i", video, "-vf", filter, "-frames:v", "1", output,
		},
		output: output,
		result: recordVideoExtractResult{
			Success:   true,
			Video:     video,
			SheetPath: output,
			Columns:   recordVideoExtractColumns,
			TileWidth: recordVideoExtractTileWidth,
			Frames:    frames,
		},
	}
}

func recordVideoStem(video string) string {
	return strings.TrimSuffix(video, filepath.Ext(video))
}

func recordVideoMilliseconds(seconds float64) int {
	return int(math.Round(seconds * 1000))
}

func formatRecordVideoSeconds(seconds float64) string {
	return strconv.FormatFloat(seconds, 'f', 3, 64)
}

func roundRecordVideoSeconds(seconds float64) float64 {
	return math.Round(seconds*1000) / 1000
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
