package projectrunner

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"os"
	"path/filepath"
	"strings"
	"testing"
)

const recordVideoExtractTestFfmpegPath = "/fake/bin/ffmpeg"

// recordVideoExtractFake stands in for ffmpeg: it records each call and, unless told otherwise,
// writes the output image so the command sees ffmpeg succeed.
type recordVideoExtractFake struct {
	lookPathErr   error
	runErr        error
	stderrTail    string
	skipOutput    bool
	ffmpegPath    string
	ffmpegArgs    [][]string
	lookedUpNames []string
}

func (fake *recordVideoExtractFake) install(t *testing.T) {
	t.Helper()
	originalLookPath := lookPathForRecordVideo
	originalRun := runFfmpegForRecordVideo
	t.Cleanup(func() {
		lookPathForRecordVideo = originalLookPath
		runFfmpegForRecordVideo = originalRun
	})

	lookPathForRecordVideo = func(name string) (string, error) {
		fake.lookedUpNames = append(fake.lookedUpNames, name)
		if fake.lookPathErr != nil {
			return "", fake.lookPathErr
		}
		return recordVideoExtractTestFfmpegPath, nil
	}
	runFfmpegForRecordVideo = func(_ context.Context, ffmpegPath string, args []string) (string, error) {
		fake.ffmpegPath = ffmpegPath
		fake.ffmpegArgs = append(fake.ffmpegArgs, args)
		if fake.runErr != nil {
			return fake.stderrTail, fake.runErr
		}
		if !fake.skipOutput {
			if err := os.WriteFile(args[len(args)-1], []byte("png"), 0o600); err != nil {
				t.Fatalf("write fake output: %v", err)
			}
		}
		return "", nil
	}
}

// recordVideoExtractPayload decodes an extraction response.
type recordVideoExtractPayload struct {
	Success   bool   `json:"Success"`
	Video     string `json:"Video"`
	FramePath string `json:"FramePath"`
	SheetPath string `json:"SheetPath"`
	Columns   int    `json:"Columns"`
	TileWidth int    `json:"TileWidth"`
	Frames    []struct {
		Seconds float64 `json:"Seconds"`
	} `json:"Frames"`
}

// writeRecordVideoExtractFixture writes a video and, when frameRate is positive, the frames.json
// a stopped recording leaves beside it. It returns the directory and the video path.
func writeRecordVideoExtractFixture(t *testing.T, frameRate int) (string, string) {
	t.Helper()
	directory := t.TempDir()
	video := filepath.Join(directory, "clip.mp4")
	if err := os.WriteFile(video, []byte("mp4"), 0o600); err != nil {
		t.Fatalf("write video: %v", err)
	}
	if frameRate > 0 {
		sidecar := []byte(`{"Version":1,"FrameRate":` + jsonInt(frameRate) + `,"Samples":[]}`)
		if err := os.WriteFile(filepath.Join(directory, "clip_frames.json"), sidecar, 0o600); err != nil {
			t.Fatalf("write sidecar: %v", err)
		}
	}
	return directory, video
}

func jsonInt(value int) string {
	encoded, _ := json.Marshal(value)
	return string(encoded)
}

func runRecordVideoExtractForTest(t *testing.T, startPath string, args []string) (recordVideoOneShotRun, *recordVideoOneShotFake) {
	t.Helper()
	unity := newRecordVideoOneShotFake()
	unity.install(t)
	var stdout, stderr bytes.Buffer
	code := runRecordVideoCommand(context.Background(), unreachableConnection("/project"), args, startPath, &stdout, &stderr)
	return recordVideoOneShotRun{code: code, stdout: stdout.String(), stderr: stderr.String()}, unity
}

func decodeRecordVideoExtractPayload(t *testing.T, stdout string) recordVideoExtractPayload {
	t.Helper()
	var payload recordVideoExtractPayload
	if err := json.Unmarshal([]byte(stdout), &payload); err != nil {
		t.Fatalf("decode %q: %v", stdout, err)
	}
	return payload
}

// Verifies a missing ffmpeg is reported as FFMPEG_NOT_FOUND without running anything or contacting Unity.
func TestRunRecordVideoExtract_FfmpegMissing_ReportsNotFound(t *testing.T) {
	_, video := writeRecordVideoExtractFixture(t, 30)
	fake := &recordVideoExtractFake{lookPathErr: errors.New("executable file not found in $PATH")}
	fake.install(t)

	run, unity := runRecordVideoExtractForTest(t, "/project", []string{"--video", video, "--frame-at", "1.5"})

	if run.code != 1 || !strings.Contains(run.stderr, recordVideoFfmpegNotFoundErrorCode) {
		t.Fatalf("run = %+v", run)
	}
	if len(fake.ffmpegArgs) != 0 {
		t.Fatalf("ffmpeg ran: %v", fake.ffmpegArgs)
	}
	if strings.Join(fake.lookedUpNames, ",") != "ffmpeg" {
		t.Fatalf("looked up %v, want ffmpeg", fake.lookedUpNames)
	}
	requireRecordVideoCalls(t, unity)
}

// Verifies --frame-at runs ffmpeg once, by the looked-up path, with a seek and a single-frame
// output beside the video, and reports that frame without contacting Unity.
func TestRunRecordVideoExtract_FrameAt_WritesOneFrameBesideVideo(t *testing.T) {
	directory, video := writeRecordVideoExtractFixture(t, 30)
	fake := &recordVideoExtractFake{}
	fake.install(t)

	run, unity := runRecordVideoExtractForTest(t, "/project", []string{"--video", video, "--frame-at", "1.5"})

	if run.code != 0 {
		t.Fatalf("run = %+v", run)
	}
	wantOut := filepath.Join(directory, "clip_frame_1500.png")
	wantArgs := []string{"-y", "-ss", "1.500", "-i", video, "-frames:v", "1", wantOut}
	if len(fake.ffmpegArgs) != 1 || strings.Join(fake.ffmpegArgs[0], " ") != strings.Join(wantArgs, " ") {
		t.Fatalf("ffmpeg args = %q, want %q", fake.ffmpegArgs, wantArgs)
	}
	if fake.ffmpegPath != recordVideoExtractTestFfmpegPath {
		t.Fatalf("ffmpeg path = %q", fake.ffmpegPath)
	}
	payload := decodeRecordVideoExtractPayload(t, run.stdout)
	if !payload.Success || payload.FramePath != wantOut || payload.Video != video || payload.SheetPath != "" {
		t.Fatalf("payload = %+v", payload)
	}
	if len(payload.Frames) != 1 || payload.Frames[0].Seconds != 1.5 {
		t.Fatalf("frames = %+v", payload.Frames)
	}
	requireRecordVideoCalls(t, unity)
}

// Verifies a relative --video is read from the directory the command ran in.
func TestRunRecordVideoExtract_RelativeVideo_ResolvesFromStartPath(t *testing.T) {
	directory, _ := writeRecordVideoExtractFixture(t, 30)
	fake := &recordVideoExtractFake{}
	fake.install(t)

	run, _ := runRecordVideoExtractForTest(t, directory, []string{"--video", "clip.mp4", "--frame-at", "0"})

	if run.code != 0 {
		t.Fatalf("run = %+v", run)
	}
	if got := fake.ffmpegArgs[0][4]; got != filepath.Join(directory, "clip.mp4") {
		t.Fatalf("input = %q", got)
	}
}

// Verifies the sheet's tile count, rate, and rows: every frame of a short range, capped at 40 for a
// longer one, and the tile seconds that follow from the rate.
func TestRunRecordVideoExtract_Sheet_SizesTilesFromRangeAndFrameRate(t *testing.T) {
	for _, testCase := range []struct {
		name        string
		from, to    string
		wantOut     string
		wantFilter  string
		wantCount   int
		wantSecond1 float64
	}{
		{
			name: "one second at 30 fps", from: "1", to: "2",
			wantOut:     "clip_sheet_1000_2000.png",
			wantFilter:  "fps=30.000000,scale=320:-2,tile=5x6:padding=2:color=0x282828",
			wantCount:   30,
			wantSecond1: 1 + 1.0/30,
		},
		{
			name: "three seconds capped at 40", from: "0.5", to: "3.5",
			wantOut:     "clip_sheet_500_3500.png",
			wantFilter:  "fps=13.333333,scale=320:-2,tile=5x8:padding=2:color=0x282828",
			wantCount:   40,
			wantSecond1: 0.5 + 3.0/40,
		},
		{
			name: "range not a whole number of frames", from: "0.2", to: "0.8",
			wantOut:     "clip_sheet_200_800.png",
			wantFilter:  "fps=30.000000,scale=320:-2,tile=5x4:padding=2:color=0x282828",
			wantCount:   18,
			wantSecond1: 0.2 + 0.6/18,
		},
	} {
		t.Run(testCase.name, func(t *testing.T) {
			directory, video := writeRecordVideoExtractFixture(t, 30)
			fake := &recordVideoExtractFake{}
			fake.install(t)

			run, unity := runRecordVideoExtractForTest(t, "/project",
				[]string{"--video", video, "--sheet-from", testCase.from, "--sheet-to", testCase.to})

			if run.code != 0 {
				t.Fatalf("run = %+v", run)
			}
			args := fake.ffmpegArgs[0]
			wantOut := filepath.Join(directory, testCase.wantOut)
			if args[len(args)-1] != wantOut {
				t.Fatalf("args = %q", args)
			}
			if got := args[indexOfToken(args, "-vf")+1]; got != testCase.wantFilter {
				t.Fatalf("filter = %q, want %q", got, testCase.wantFilter)
			}
			payload := decodeRecordVideoExtractPayload(t, run.stdout)
			if payload.SheetPath != wantOut || payload.Columns != 5 || payload.TileWidth != 320 || payload.FramePath != "" {
				t.Fatalf("payload = %+v", payload)
			}
			if len(payload.Frames) != testCase.wantCount {
				t.Fatalf("frames = %d, want %d", len(payload.Frames), testCase.wantCount)
			}
			if !closeTo(payload.Frames[1].Seconds, testCase.wantSecond1) {
				t.Fatalf("second tile at %v, want %v", payload.Frames[1].Seconds, testCase.wantSecond1)
			}
			requireRecordVideoCalls(t, unity)
		})
	}
}

// Verifies the sheet seeks to the range start and limits the read to the range length.
func TestRunRecordVideoExtract_Sheet_SeeksToRange(t *testing.T) {
	_, video := writeRecordVideoExtractFixture(t, 30)
	fake := &recordVideoExtractFake{}
	fake.install(t)

	run, _ := runRecordVideoExtractForTest(t, "/project", []string{"--video", video, "--sheet-from=0.5", "--sheet-to=3.5"})

	if run.code != 0 {
		t.Fatalf("run = %+v", run)
	}
	wantHead := []string{"-y", "-ss", "0.500", "-t", "3.000", "-i", video, "-vf"}
	if strings.Join(fake.ffmpegArgs[0][:len(wantHead)], " ") != strings.Join(wantHead, " ") {
		t.Fatalf("args = %q", fake.ffmpegArgs[0])
	}
}

// Verifies argument mistakes are rejected before ffmpeg is looked up or Unity is contacted.
func TestRunRecordVideoExtract_InvalidArguments_RunNothing(t *testing.T) {
	_, video := writeRecordVideoExtractFixture(t, 30)
	for _, testCase := range []struct {
		args     []string
		wantText string
	}{
		{args: []string{"--frame-at", "1"}, wantText: "--video"},
		{args: []string{"--video", video}, wantText: "--frame-at or --sheet-from"},
		{args: []string{"--video", video, "--frame-at", "1", "--sheet-from", "0", "--sheet-to", "1"}, wantText: "cannot be combined"},
		{args: []string{"--video", video, "--sheet-from", "1"}, wantText: "--sheet-to"},
		{args: []string{"--video", video, "--sheet-to", "1"}, wantText: "--sheet-from"},
		{args: []string{"--video", video, "--sheet-from", "2", "--sheet-to", "2"}, wantText: "after --sheet-from"},
		{args: []string{"--video", video, "--frame-at", "-1"}, wantText: "--frame-at"},
		{args: []string{"--video", video, "--frame-at", "soon"}, wantText: "--frame-at"},
		{args: []string{"--video", video, "--frame-at", "NaN"}, wantText: "--frame-at"},
		{args: []string{"--video", "", "--frame-at", "1"}, wantText: "--video requires a value"},
		{args: []string{"--video", video, "--frame-at", "1", "--seconds", "3"}, wantText: "--seconds"},
		{args: []string{"--video", video, "--frame-at", "1", "--action", "stop"}, wantText: "--action"},
		{args: []string{"--seconds", "3", "--video", video, "--frame-at", "1"}, wantText: "--seconds"},
	} {
		fake := &recordVideoExtractFake{}
		fake.install(t)

		run, unity := runRecordVideoExtractForTest(t, "/project", testCase.args)

		if run.code != 1 || !strings.Contains(run.stderr, "INVALID_ARGUMENT") || !strings.Contains(run.stderr, testCase.wantText) {
			t.Fatalf("args %q: run = %+v", testCase.args, run)
		}
		if len(fake.lookedUpNames) != 0 || len(fake.ffmpegArgs) != 0 {
			t.Fatalf("args %q: ffmpeg was looked up or run", testCase.args)
		}
		requireRecordVideoCalls(t, unity)
	}
}

// Verifies a video that does not exist, or that has no frames.json beside it, is rejected before ffmpeg runs.
func TestRunRecordVideoExtract_MissingVideoOrAnalysis_RunsNoFfmpeg(t *testing.T) {
	directory, video := writeRecordVideoExtractFixture(t, 0)
	for _, testCase := range []struct {
		video    string
		wantText string
	}{
		{video: filepath.Join(directory, "absent.mp4"), wantText: "does not exist"},
		{video: video, wantText: "frames.json"},
	} {
		fake := &recordVideoExtractFake{}
		fake.install(t)

		run, _ := runRecordVideoExtractForTest(t, "/project", []string{"--video", testCase.video, "--frame-at", "1"})

		if run.code != 1 || !strings.Contains(run.stderr, testCase.wantText) {
			t.Fatalf("video %q: run = %+v", testCase.video, run)
		}
		if len(fake.ffmpegArgs) != 0 {
			t.Fatalf("video %q: ffmpeg ran", testCase.video)
		}
	}
}

// Verifies an ffmpeg failure is reported as FFMPEG_FAILED with the tail of its stderr.
func TestRunRecordVideoExtract_FfmpegFails_ReportsStderrTail(t *testing.T) {
	_, video := writeRecordVideoExtractFixture(t, 30)
	fake := &recordVideoExtractFake{runErr: errors.New("exit status 1"), stderrTail: "Invalid data found when processing input"}
	fake.install(t)

	run, _ := runRecordVideoExtractForTest(t, "/project", []string{"--video", video, "--frame-at", "1"})

	if run.code != 1 || !strings.Contains(run.stderr, recordVideoFfmpegFailedErrorCode) ||
		!strings.Contains(run.stderr, "Invalid data found when processing input") {
		t.Fatalf("run = %+v", run)
	}
	if run.stdout != "" {
		t.Fatalf("stdout = %q", run.stdout)
	}
}

// Verifies ffmpeg exiting cleanly without writing the image, as it does for a time past the end,
// is reported as a failure instead of a path to a missing file.
func TestRunRecordVideoExtract_FfmpegWritesNothing_ReportsFailure(t *testing.T) {
	_, video := writeRecordVideoExtractFixture(t, 30)
	fake := &recordVideoExtractFake{skipOutput: true}
	fake.install(t)

	run, _ := runRecordVideoExtractForTest(t, "/project", []string{"--video", video, "--frame-at", "99"})

	if run.code != 1 || !strings.Contains(run.stderr, recordVideoFfmpegFailedErrorCode) || !strings.Contains(run.stderr, "past the end") {
		t.Fatalf("run = %+v", run)
	}
}

// Verifies a stale image from an earlier run is not reported when ffmpeg writes nothing this time.
func TestRunRecordVideoExtract_StaleOutput_IsNotReported(t *testing.T) {
	directory, video := writeRecordVideoExtractFixture(t, 30)
	if err := os.WriteFile(filepath.Join(directory, "clip_frame_99000.png"), []byte("old"), 0o600); err != nil {
		t.Fatalf("write stale output: %v", err)
	}
	fake := &recordVideoExtractFake{skipOutput: true}
	fake.install(t)

	run, _ := runRecordVideoExtractForTest(t, "/project", []string{"--video", video, "--frame-at", "99"})

	if run.code != 1 || !strings.Contains(run.stderr, recordVideoFfmpegFailedErrorCode) {
		t.Fatalf("run = %+v", run)
	}
}

func indexOfToken(tokens []string, want string) int {
	for index, token := range tokens {
		if token == want {
			return index
		}
	}
	return -1
}

func closeTo(got float64, want float64) bool {
	difference := got - want
	return difference < 0.0005 && difference > -0.0005
}
