package projectrunner

import (
	"encoding/json"
	"fmt"
	"math"
	"os"
	"path/filepath"
	"sort"
	"strconv"
	"strings"

	"github.com/hatayama/unity-cli-loop/common/tooldocs"
)

const (
	recordVideoExtractMaxTiles    = 40
	recordVideoExtractColumns     = 5
	recordVideoExtractTileWidth   = 320
	recordVideoExtractSheetFilter = "select='not(mod(n,%d))',scale=%d:-2,tile=%dx%d:padding=2:color=0x282828"
	recordVideoAnalysisSuffix     = "_frames.json"
)

// recordVideoExtractFrame names one video frame the way the burst and timeline sheets do: its
// number in the video, and the capture it shows with that capture's time.
type recordVideoExtractFrame struct {
	EncodedFrame  int     `json:"EncodedFrame"`
	CapturedIndex int     `json:"CapturedIndex"`
	Seconds       float64 `json:"Seconds"`
}

// recordVideoCaptures maps capture-clock seconds, the clock of MarkSeconds and the sheets'
// Seconds, to video frames. The video's own clock (frame number / frame rate) drifts from the
// capture clock by a frame or more, so times are never converted by the frame rate alone.
type recordVideoCaptures struct {
	frameRate int
	samples   []recordVideoExtractFrame
}

// readRecordVideoCaptures reads the frame rate and per-capture samples from the frames.json
// written beside the video when the recording stopped.
func readRecordVideoCaptures(video string) (recordVideoCaptures, error) {
	info, err := os.Stat(video)
	if err != nil || info.IsDir() {
		return recordVideoCaptures{}, recordVideoArgumentError("--"+tooldocs.RecordVideoVideoFlagName,
			"--video "+video+" does not exist or is not a file.",
			"Pass the OutputPath of the stop or one-shot response.")
	}
	content, err := os.ReadFile(recordVideoStem(video) + recordVideoAnalysisSuffix)
	if err != nil {
		return recordVideoCaptures{}, recordVideoArgumentError("--"+tooldocs.RecordVideoVideoFlagName,
			"no frames.json beside the video; only recordings made by this version can be re-cut.",
			"Record again with this version, or open the video in a player.")
	}
	var analysis struct {
		FrameRate int                       `json:"FrameRate"`
		Samples   []recordVideoExtractFrame `json:"Samples"`
	}
	if err := json.Unmarshal(content, &analysis); err != nil || analysis.FrameRate <= 0 {
		return recordVideoCaptures{}, recordVideoArgumentError("--"+tooldocs.RecordVideoVideoFlagName,
			"the frames.json beside the video has no valid FrameRate.",
			"Record again with this version.")
	}
	if len(analysis.Samples) == 0 {
		return recordVideoCaptures{}, recordVideoArgumentError("--"+tooldocs.RecordVideoVideoFlagName,
			"the recording has no captured frames to cut.",
			"Record again; the recording stopped before its first frame.")
	}
	return recordVideoCaptures{frameRate: analysis.FrameRate, samples: analysis.Samples}, nil
}

// frameAt is the video frame showing the last capture at or before seconds, clamped to the first
// and last captures.
func (captures recordVideoCaptures) frameAt(seconds float64) int {
	index := sort.Search(len(captures.samples), func(i int) bool { return captures.samples[i].Seconds > seconds })
	return captures.samples[max(index-1, 0)].EncodedFrame
}

// describe names a video frame. A frame without a sample of its own is a repeat the encoder wrote
// to keep pace, and shows the capture before it.
func (captures recordVideoCaptures) describe(encodedFrame int) recordVideoExtractFrame {
	index := sort.Search(len(captures.samples), func(i int) bool { return captures.samples[i].EncodedFrame > encodedFrame })
	source := captures.samples[max(index-1, 0)]
	return recordVideoExtractFrame{EncodedFrame: encodedFrame, CapturedIndex: source.CapturedIndex, Seconds: source.Seconds}
}

// seekBefore is the input seek to frame: half a frame early, so the first decoded frame is that
// frame whichever way its timestamp rounds.
func (captures recordVideoCaptures) seekBefore(encodedFrame int) string {
	return formatRecordVideoSeconds(math.Max(0, (float64(encodedFrame)-0.5)/float64(captures.frameRate)))
}

func planRecordVideoExtract(options recordVideoExtractOptions, video string, captures recordVideoCaptures) recordVideoExtractPlan {
	stem := recordVideoStem(video)
	if options.hasFrame {
		frame := captures.frameAt(options.frameAt)
		output := fmt.Sprintf("%s_frame_%d.png", stem, recordVideoMilliseconds(options.frameAt))
		return recordVideoExtractPlan{
			args:   []string{"-y", "-ss", captures.seekBefore(frame), "-i", video, "-frames:v", "1", output},
			output: output,
			result: recordVideoExtractResult{
				Success:   true,
				Video:     video,
				FramePath: output,
				Frames:    []recordVideoExtractFrame{captures.describe(frame)},
			},
		}
	}

	first := captures.frameAt(options.sheetFrom)
	last := captures.frameAt(options.sheetTo)
	span := last - first + 1
	step := (span + recordVideoExtractMaxTiles - 1) / recordVideoExtractMaxTiles
	count := (last-first)/step + 1
	rows := (count + recordVideoExtractColumns - 1) / recordVideoExtractColumns
	frames := make([]recordVideoExtractFrame, count)
	for index := range frames {
		frames[index] = captures.describe(first + index*step)
	}
	output := fmt.Sprintf("%s_sheet_%d_%d.png", stem,
		recordVideoMilliseconds(options.sheetFrom), recordVideoMilliseconds(options.sheetTo))
	filter := fmt.Sprintf(recordVideoExtractSheetFilter, step, recordVideoExtractTileWidth, recordVideoExtractColumns, rows)
	// -t bounds the read to frames first..last; without it select would keep frames after last and
	// fill the sheet's empty slots with them.
	length := formatRecordVideoSeconds(float64(span) / float64(captures.frameRate))
	return recordVideoExtractPlan{
		args: []string{
			"-y", "-ss", captures.seekBefore(first), "-t", length,
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
