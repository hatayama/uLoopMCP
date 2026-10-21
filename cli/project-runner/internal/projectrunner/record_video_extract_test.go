package projectrunner

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"os"
	"path/filepath"
	"strconv"
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
	Success   bool                      `json:"Success"`
	Video     string                    `json:"Video"`
	FramePath string                    `json:"FramePath"`
	SheetPath string                    `json:"SheetPath"`
	Columns   int                       `json:"Columns"`
	TileWidth int                       `json:"TileWidth"`
	Frames    []recordVideoExtractFrame `json:"Frames"`
}

// recordVideoExtractTestSamples are 100 captures at 30 fps, 10 ms after their frame's video time.
// Capture 50 was late, so the encoder repeated capture 49 as frame 50 and capture 50 is frame 51:
// frame 50 has no sample of its own.
func recordVideoExtractTestSamples() []recordVideoExtractFrame {
	samples := make([]recordVideoExtractFrame, 0, 100)
	for index := 0; index < 100; index++ {
		encoded := index
		if index >= 50 {
			encoded++
		}
		samples = append(samples, recordVideoExtractFrame{
			CapturedIndex: index,
			EncodedFrame:  encoded,
			Seconds:       float64(index)/30 + 0.01,
		})
	}
	return samples
}

// writeRecordVideoExtractFixture writes a video and, when frameRate is positive, the frames.json
// a stopped recording leaves beside it, with recordVideoExtractTestSamples. It returns the
// directory and the video path.
func writeRecordVideoExtractFixture(t *testing.T, frameRate int) (string, string) {
	t.Helper()
	return writeRecordVideoExtractFixtureWithSamples(t, frameRate, recordVideoExtractTestSamples())
}

func writeRecordVideoExtractFixtureWithSamples(t *testing.T, frameRate int, samples []recordVideoExtractFrame) (string, string) {
	t.Helper()
	directory := t.TempDir()
	video := filepath.Join(directory, "clip.mp4")
	if err := os.WriteFile(video, []byte("mp4"), 0o600); err != nil {
		t.Fatalf("write video: %v", err)
	}
	if frameRate > 0 {
		sidecar, err := json.Marshal(map[string]any{"Version": 1, "FrameRate": frameRate, "Samples": samples})
		if err != nil {
			t.Fatalf("encode sidecar: %v", err)
		}
		if err = os.WriteFile(filepath.Join(directory, "clip_frames.json"), sidecar, 0o600); err != nil {
			t.Fatalf("write sidecar: %v", err)
		}
	}
	return directory, video
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

// Verifies --frame-at maps the capture-clock time to the frame captured last at or before it,
// seeks half a frame before that frame, and reports the frame's number, capture, and seconds,
// without contacting Unity.
func TestRunRecordVideoExtract_FrameAt_CutsTheFrameCapturedAtThatTime(t *testing.T) {
	directory, video := writeRecordVideoExtractFixture(t, 30)
	fake := &recordVideoExtractFake{}
	fake.install(t)

	run, unity := runRecordVideoExtractForTest(t, "/project", []string{"--video", video, "--frame-at", "1.5"})

	if run.code != 0 {
		t.Fatalf("run = %+v", run)
	}
	// Capture 44 was taken at 44/30 + 0.01 = 1.477 s, the last one at or before 1.5 s.
	wantOut := filepath.Join(directory, "clip_frame_1500.png")
	wantArgs := []string{"-y", "-ss", "1.450", "-i", video, "-frames:v", "1", wantOut}
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
	want := recordVideoExtractTestSamples()[44]
	if len(payload.Frames) != 1 || payload.Frames[0] != want {
		t.Fatalf("frames = %+v, want %+v", payload.Frames, want)
	}
	requireRecordVideoCalls(t, unity)
}

// Verifies the time-to-frame boundaries: a time exactly at a capture picks that capture, and times
// outside the recording clamp to its first and last captured frames with the seek never below zero.
func TestRunRecordVideoExtract_FrameAt_MapsBoundaryTimes(t *testing.T) {
	samples := recordVideoExtractTestSamples()
	for _, testCase := range []struct {
		at       string
		wantSeek string
		want     recordVideoExtractFrame
	}{
		{at: strconv.FormatFloat(samples[44].Seconds, 'g', -1, 64), wantSeek: "1.450", want: samples[44]},
		{at: "0", wantSeek: "0.000", want: samples[0]},
		{at: "99", wantSeek: "3.317", want: samples[99]},
	} {
		_, video := writeRecordVideoExtractFixture(t, 30)
		fake := &recordVideoExtractFake{}
		fake.install(t)

		run, _ := runRecordVideoExtractForTest(t, "/project", []string{"--video", video, "--frame-at", testCase.at})

		if run.code != 0 {
			t.Fatalf("at %s: run = %+v", testCase.at, run)
		}
		if got := fake.ffmpegArgs[0][2]; got != testCase.wantSeek {
			t.Fatalf("at %s: seek = %q, want %q", testCase.at, got, testCase.wantSeek)
		}
		payload := decodeRecordVideoExtractPayload(t, run.stdout)
		if payload.Frames[0] != testCase.want {
			t.Fatalf("at %s: frame = %+v, want %+v", testCase.at, payload.Frames[0], testCase.want)
		}
	}
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

// Verifies the sheet reads exactly frames A..B (seek half a frame before A, length B-A+1 frames),
// keeps every step-th frame so at most 40 remain, and sizes the tile rows from that count.
func TestRunRecordVideoExtract_Sheet_SelectsFramesOfTheRange(t *testing.T) {
	for _, testCase := range []struct {
		name       string
		from, to   string
		wantOut    string
		wantSeek   string
		wantLength string
		wantFilter string
		wantFrames []int
	}{
		{
			// 1.5 s is capture 44 (frame 44) and 1.75 s is capture 52 (frame 53): 10 frames, one each.
			name: "every frame of a short range", from: "1.5", to: "1.75",
			wantOut:    "clip_sheet_1500_1750.png",
			wantSeek:   "1.450",
			wantLength: "0.333",
			wantFilter: "select='not(mod(n,1))',scale=320:-2,tile=5x2:padding=2:color=0x282828",
			wantFrames: recordVideoExtractSteps(44, 53, 1),
		},
		{
			// The whole recording is frames 0..100: 101 frames, every third keeps 34.
			name: "the whole recording thinned to 40 or fewer", from: "0", to: "99",
			wantOut:    "clip_sheet_0_99000.png",
			wantSeek:   "0.000",
			wantLength: "3.367",
			wantFilter: "select='not(mod(n,3))',scale=320:-2,tile=5x7:padding=2:color=0x282828",
			wantFrames: recordVideoExtractSteps(0, 100, 3),
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
			wantOut := filepath.Join(directory, testCase.wantOut)
			wantArgs := []string{
				"-y", "-ss", testCase.wantSeek, "-t", testCase.wantLength, "-i", video,
				"-vf", testCase.wantFilter, "-frames:v", "1", wantOut,
			}
			if strings.Join(fake.ffmpegArgs[0], " ") != strings.Join(wantArgs, " ") {
				t.Fatalf("ffmpeg args = %q, want %q", fake.ffmpegArgs[0], wantArgs)
			}
			payload := decodeRecordVideoExtractPayload(t, run.stdout)
			if payload.SheetPath != wantOut || payload.Columns != 5 || payload.TileWidth != 320 || payload.FramePath != "" {
				t.Fatalf("payload = %+v", payload)
			}
			if got := recordVideoExtractEncodedFrames(payload.Frames); strings.Join(got, ",") != strings.Join(intsToStrings(testCase.wantFrames), ",") {
				t.Fatalf("frames = %v, want %v", got, testCase.wantFrames)
			}
			requireRecordVideoCalls(t, unity)
		})
	}
}

// Verifies a tile on a frame the encoder repeated reports the capture it repeats, and every other
// tile reports its own capture.
func TestRunRecordVideoExtract_Sheet_RepeatedFrameReportsItsSource(t *testing.T) {
	_, video := writeRecordVideoExtractFixture(t, 30)
	fake := &recordVideoExtractFake{}
	fake.install(t)

	run, _ := runRecordVideoExtractForTest(t, "/project", []string{"--video", video, "--sheet-from", "1.62", "--sheet-to", "1.72"})

	if run.code != 0 {
		t.Fatalf("run = %+v", run)
	}
	samples := recordVideoExtractTestSamples()
	repeated := samples[49]
	repeated.EncodedFrame = 50
	want := []recordVideoExtractFrame{samples[48], samples[49], repeated, samples[50], samples[51]}
	payload := decodeRecordVideoExtractPayload(t, run.stdout)
	if len(payload.Frames) != len(want) {
		t.Fatalf("frames = %+v, want %+v", payload.Frames, want)
	}
	for index := range want {
		if payload.Frames[index] != want[index] {
			t.Fatalf("frame %d = %+v, want %+v", index, payload.Frames[index], want[index])
		}
	}
}

// Verifies a --sheet-to past the end of the recording ends the range at its last frame.
func TestRunRecordVideoExtract_Sheet_ToPastTheEndStopsAtTheLastFrame(t *testing.T) {
	_, video := writeRecordVideoExtractFixture(t, 30)
	fake := &recordVideoExtractFake{}
	fake.install(t)

	run, _ := runRecordVideoExtractForTest(t, "/project", []string{"--video", video, "--sheet-from", "3", "--sheet-to", "99"})

	if run.code != 0 {
		t.Fatalf("run = %+v", run)
	}
	// From capture 89 (frame 90) to the last frame, 100: 11 frames.
	args := fake.ffmpegArgs[0]
	if args[2] != "2.983" || args[4] != "0.367" {
		t.Fatalf("args = %q", args)
	}
	payload := decodeRecordVideoExtractPayload(t, run.stdout)
	frames := recordVideoExtractEncodedFrames(payload.Frames)
	if len(frames) != 11 || frames[0] != "90" || frames[10] != "100" {
		t.Fatalf("frames = %v", frames)
	}
}

func recordVideoExtractSteps(from int, to int, step int) []int {
	frames := []int{}
	for frame := from; frame <= to; frame += step {
		frames = append(frames, frame)
	}
	return frames
}

func recordVideoExtractEncodedFrames(frames []recordVideoExtractFrame) []string {
	encoded := make([]string, 0, len(frames))
	for _, frame := range frames {
		encoded = append(encoded, strconv.Itoa(frame.EncodedFrame))
	}
	return encoded
}

func intsToStrings(values []int) []string {
	converted := make([]string, 0, len(values))
	for _, value := range values {
		converted = append(converted, strconv.Itoa(value))
	}
	return converted
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

// Verifies a frames.json without samples is rejected before ffmpeg runs, since no time can be mapped to a frame.
func TestRunRecordVideoExtract_AnalysisWithoutSamples_RunsNoFfmpeg(t *testing.T) {
	_, video := writeRecordVideoExtractFixtureWithSamples(t, 30, []recordVideoExtractFrame{})
	fake := &recordVideoExtractFake{}
	fake.install(t)

	run, _ := runRecordVideoExtractForTest(t, "/project", []string{"--video", video, "--frame-at", "1"})

	if run.code != 1 || !strings.Contains(run.stderr, "no captured frames") {
		t.Fatalf("run = %+v", run)
	}
	if len(fake.ffmpegArgs) != 0 {
		t.Fatal("ffmpeg ran")
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
