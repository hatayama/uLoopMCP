package projectrunner

import (
	"bytes"
	"context"
	"encoding/json"
	"io"
	"strings"
	"testing"
	"time"

	"github.com/hatayama/unity-cli-loop/common/unityipc"
)

const (
	recordVideoTestStopResponse = `{"Success":true,"OutputPath":"clip.mp4","StoppedBy":"cli","BurstAnchor":"mark"}`
	recordVideoTestSuccess      = `{"Success":true}`
)

// recordVideoOneShotFake replaces every request the one-shot makes and records them in order.
type recordVideoOneShotFake struct {
	calls         []string
	focusCode     int
	startResponse string
	markResponse  string
	markErr       error
	stopResponse  string
	triggerStdout string
	triggerStderr string
	triggerExit   int
	onTrigger     func()
	onMark        func()
	onWait        func(ctx context.Context) error
	waitDuration  time.Duration
	startArgs     []string
	stopCtxErr    error
}

func newRecordVideoOneShotFake() *recordVideoOneShotFake {
	return &recordVideoOneShotFake{
		startResponse: recordVideoTestSuccess,
		markResponse:  recordVideoTestSuccess,
		stopResponse:  recordVideoTestStopResponse,
		triggerStdout: recordVideoTestSuccess,
	}
}

func fakeToolResult(response string) toolExecutionResult {
	return toolExecutionResult{result: json.RawMessage(response), exitCode: toolEnvelopeExitCode([]byte(response))}
}

func (fake *recordVideoOneShotFake) install(t *testing.T) {
	t.Helper()
	originalStart := sendRecordVideoStart
	originalRequest := sendRecordVideoRequest
	originalFocus := runFocusWindowForRecordVideo
	originalWait := waitForRecordVideo
	originalDispatch := dispatchPausePointTriggerCommand
	t.Cleanup(func() {
		sendRecordVideoStart = originalStart
		sendRecordVideoRequest = originalRequest
		runFocusWindowForRecordVideo = originalFocus
		waitForRecordVideo = originalWait
		dispatchPausePointTriggerCommand = originalDispatch
	})

	runFocusWindowForRecordVideo = func(context.Context, string, io.Writer, io.Writer) int {
		fake.calls = append(fake.calls, "focus")
		return fake.focusCode
	}
	sendRecordVideoStart = func(_ context.Context, _ unityipc.Connection, args []string, _ string, _ io.Writer) toolExecutionResult {
		fake.calls = append(fake.calls, "start")
		fake.startArgs = args
		return fakeToolResult(fake.startResponse)
	}
	sendRecordVideoRequest = func(ctx context.Context, _ unityipc.Connection, args []string, _ string) (toolExecutionResult, error) {
		action := args[len(args)-1]
		fake.calls = append(fake.calls, action)
		if action == "stop" {
			fake.stopCtxErr = ctx.Err()
			return fakeToolResult(fake.stopResponse), nil
		}
		if fake.onMark != nil {
			fake.onMark()
		}
		if fake.markErr != nil {
			return toolExecutionResult{exitCode: 1}, fake.markErr
		}
		return fakeToolResult(fake.markResponse), nil
	}
	waitForRecordVideo = func(ctx context.Context, duration time.Duration) error {
		fake.calls = append(fake.calls, "wait")
		fake.waitDuration = duration
		if fake.onWait != nil {
			return fake.onWait(ctx)
		}
		return nil
	}
	dispatchPausePointTriggerCommand = func(
		_ context.Context, _ unityipc.Connection, command string, _ []string, _ string, stdout io.Writer, stderr io.Writer,
	) int {
		fake.calls = append(fake.calls, "trigger:"+command)
		if fake.onTrigger != nil {
			fake.onTrigger()
		}
		_, _ = io.WriteString(stdout, fake.triggerStdout)
		_, _ = io.WriteString(stderr, fake.triggerStderr)
		return fake.triggerExit
	}
}

type recordVideoOneShotRun struct {
	code   int
	stdout string
	stderr string
}

func runRecordVideoOneShotForTest(ctx context.Context, args []string) recordVideoOneShotRun {
	var stdout, stderr bytes.Buffer
	code := runRecordVideoCommand(ctx, unreachableConnection("/project"), args, "/project", &stdout, &stderr)
	return recordVideoOneShotRun{code: code, stdout: stdout.String(), stderr: stderr.String()}
}

func requireRecordVideoCalls(t *testing.T, fake *recordVideoOneShotFake, want ...string) {
	t.Helper()
	if strings.Join(fake.calls, ",") != strings.Join(want, ",") {
		t.Fatalf("calls = %v, want %v", fake.calls, want)
	}
}

// recordVideoOneShotPayload decodes the stop response with its OneShot report.
type recordVideoOneShotPayload struct {
	Success     bool   `json:"Success"`
	StoppedBy   string `json:"StoppedBy"`
	BurstAnchor string `json:"BurstAnchor"`
	Message     string `json:"Message"`
	OneShot     struct {
		Seconds         int             `json:"Seconds"`
		FocusWindowNote string          `json:"FocusWindowNote"`
		TriggerCommand  string          `json:"TriggerCommand"`
		TriggerResult   json.RawMessage `json:"TriggerResult"`
		MarkAccepted    *bool           `json:"MarkAccepted"`
		Warning         string          `json:"Warning"`
	} `json:"OneShot"`
}

func decodeRecordVideoOneShotStdout(t *testing.T, run recordVideoOneShotRun) recordVideoOneShotPayload {
	t.Helper()
	if run.code != 0 {
		t.Fatalf("exit = %d, stderr:\n%s", run.code, run.stderr)
	}
	payload := recordVideoOneShotPayload{}
	if err := json.Unmarshal([]byte(run.stdout), &payload); err != nil {
		t.Fatalf("stdout is not the stop payload: %v\n%s", err, run.stdout)
	}
	return payload
}

// decodeRecordVideoOneShotFailure checks the error code and returns the attached recording.
func decodeRecordVideoOneShotFailure(t *testing.T, run recordVideoOneShotRun, wantCode string) recordVideoOneShotPayload {
	t.Helper()
	if run.code != 1 {
		t.Fatalf("exit = %d, want 1; stdout:\n%s", run.code, run.stdout)
	}
	if run.stdout != "" {
		t.Fatalf("a failed one-shot must not write stdout:\n%s", run.stdout)
	}
	envelope := struct {
		Error struct {
			ErrorCode string `json:"ErrorCode"`
			Details   struct {
				Recording recordVideoOneShotPayload `json:"Recording"`
			} `json:"Details"`
		} `json:"Error"`
	}{}
	if err := json.Unmarshal([]byte(run.stderr), &envelope); err != nil {
		t.Fatalf("stderr is not an error envelope: %v\n%s", err, run.stderr)
	}
	if envelope.Error.ErrorCode != wantCode {
		t.Fatalf("ErrorCode = %q, want %q", envelope.Error.ErrorCode, wantCode)
	}
	return envelope.Error.Details.Recording
}

func requireMarkAccepted(t *testing.T, payload recordVideoOneShotPayload, want bool) {
	t.Helper()
	if payload.OneShot.MarkAccepted == nil || *payload.OneShot.MarkAccepted != want {
		t.Fatalf("MarkAccepted = %v, want %v", payload.OneShot.MarkAccepted, want)
	}
}

// Verifies --seconds alone focuses, starts, waits that long, and stops, without a trigger or mark.
func TestRunRecordVideoOneShot_TimedOnly_StartsWaitsStops(t *testing.T) {
	fake := newRecordVideoOneShotFake()
	fake.install(t)

	payload := decodeRecordVideoOneShotStdout(t, runRecordVideoOneShotForTest(context.Background(), []string{"--seconds", "3"}))

	requireRecordVideoCalls(t, fake, "focus", "start", "wait", "stop")
	if fake.waitDuration != 3*time.Second || payload.OneShot.Seconds != 3 {
		t.Fatalf("wait = %v, Seconds = %d", fake.waitDuration, payload.OneShot.Seconds)
	}
	if payload.OneShot.TriggerCommand != "" || payload.OneShot.MarkAccepted != nil || payload.OneShot.FocusWindowNote != "" {
		t.Fatalf("OneShot = %+v, want only Seconds", payload.OneShot)
	}
	if strings.Join(fake.startArgs, " ") != "--action start" {
		t.Fatalf("start args = %q", fake.startArgs)
	}
	if payload.StoppedBy != "cli" {
		t.Fatalf("stop response fields were not kept: %+v", payload)
	}
}

// Verifies --trigger alone waits the implied three seconds.
func TestRunRecordVideoOneShot_TriggerImpliesThreeSeconds(t *testing.T) {
	fake := newRecordVideoOneShotFake()
	fake.install(t)

	payload := decodeRecordVideoOneShotStdout(t, runRecordVideoOneShotForTest(context.Background(),
		[]string{"--trigger", "simulate-keyboard --action Press --key C"}))

	if fake.waitDuration != 3*time.Second || payload.OneShot.Seconds != 3 {
		t.Fatalf("wait = %v, Seconds = %d", fake.waitDuration, payload.OneShot.Seconds)
	}
}

// Verifies a successful trigger runs after start, then the mark, the wait, and the stop, in that order.
func TestRunRecordVideoOneShot_RunsTriggerThenMarksThenWaitsThenStops(t *testing.T) {
	fake := newRecordVideoOneShotFake()
	fake.install(t)

	payload := decodeRecordVideoOneShotStdout(t, runRecordVideoOneShotForTest(context.Background(),
		[]string{"--seconds", "2", "--trigger", "simulate-keyboard --action Press --key Space", "--quality", "low"}))

	requireRecordVideoCalls(t, fake, "focus", "start", "trigger:simulate-keyboard", "mark", "wait", "stop")
	requireMarkAccepted(t, payload, true)
	if fake.waitDuration != 2*time.Second {
		t.Fatalf("wait = %v", fake.waitDuration)
	}
	if payload.OneShot.TriggerCommand != "simulate-keyboard --action Press --key Space" || payload.OneShot.Warning != "" {
		t.Fatalf("OneShot = %+v", payload.OneShot)
	}
	if strings.Join(fake.startArgs, " ") != "--quality low --action start" {
		t.Fatalf("start args = %q", fake.startArgs)
	}
}

// Verifies an enable-pause-point trace await trigger marks once the line was hit, and the stop
// response's own fields (the burst anchor) pass through.
func TestRunRecordVideoOneShot_TraceAwaitTrigger_MarksOnHit(t *testing.T) {
	fake := newRecordVideoOneShotFake()
	fake.triggerStdout = `{"Success":true,"Mode":"trace","HitCount":1}`
	fake.install(t)

	payload := decodeRecordVideoOneShotStdout(t, runRecordVideoOneShotForTest(context.Background(),
		[]string{"--trigger", "enable-pause-point --file A.cs --line 3 --mode trace --await"}))

	requireRecordVideoCalls(t, fake, "focus", "start", "trigger:enable-pause-point", "mark", "wait", "stop")
	requireMarkAccepted(t, payload, true)
	if payload.BurstAnchor != "mark" || payload.OneShot.Warning != "" {
		t.Fatalf("payload = %+v", payload)
	}
}

// Verifies a recording that auto-stopped while the trigger ran reports the rejected mark with a
// warning, still waits and stops, and succeeds with the stop's reason.
func TestRunRecordVideoOneShot_AutoStoppedDuringTrigger_WarnsMarkNotAccepted(t *testing.T) {
	fake := newRecordVideoOneShotFake()
	fake.markResponse = `{"Success":false,"Message":"No recording is in progress."}`
	fake.stopResponse = `{"Success":true,"StoppedBy":"max-duration"}`
	fake.install(t)

	payload := decodeRecordVideoOneShotStdout(t, runRecordVideoOneShotForTest(context.Background(),
		[]string{"--trigger", "enable-pause-point --file A.cs --line 3 --mode trace --await"}))

	requireRecordVideoCalls(t, fake, "focus", "start", "trigger:enable-pause-point", "mark", "wait", "stop")
	requireMarkAccepted(t, payload, false)
	if payload.OneShot.Warning != recordVideoMarkWarning || payload.StoppedBy != "max-duration" {
		t.Fatalf("payload = %+v", payload)
	}
}

// Verifies a mark rejected before Unity (an older catalog) only sets MarkAccepted false and writes
// nothing to stderr, while the wait and stop still run.
func TestRunRecordVideoOneShot_MarkRejected_ContinuesWithMarkAcceptedFalse(t *testing.T) {
	fake := newRecordVideoOneShotFake()
	fake.markErr = errRecordVideoRequestRejected
	fake.install(t)

	run := runRecordVideoOneShotForTest(context.Background(), []string{"--trigger", "simulate-keyboard --key C"})

	payload := decodeRecordVideoOneShotStdout(t, run)
	requireRecordVideoCalls(t, fake, "focus", "start", "trigger:simulate-keyboard", "mark", "wait", "stop")
	requireMarkAccepted(t, payload, false)
	if run.stderr != "" {
		t.Fatalf("stderr = %q, want empty", run.stderr)
	}
}

// Verifies an await-pause-point trigger on a trace pause point carries no warning.
func TestRunRecordVideoOneShot_AwaitTriggerTraceMode_NoWarning(t *testing.T) {
	fake := newRecordVideoOneShotFake()
	fake.triggerStdout = `{"Success":true,"Mode":"trace"}`
	fake.install(t)

	payload := decodeRecordVideoOneShotStdout(t, runRecordVideoOneShotForTest(context.Background(),
		[]string{"--trigger", "await-pause-point --id marker"}))

	if payload.OneShot.Warning != "" {
		t.Fatalf("Warning = %q, want empty", payload.OneShot.Warning)
	}
}

// Verifies an await-pause-point trigger on a pausing pause point continues with a warning.
func TestRunRecordVideoOneShot_AwaitTriggerPausingMode_WarnsAndContinues(t *testing.T) {
	fake := newRecordVideoOneShotFake()
	fake.triggerStdout = `{"Success":true,"Mode":"single-shot"}`
	fake.install(t)

	payload := decodeRecordVideoOneShotStdout(t, runRecordVideoOneShotForTest(context.Background(),
		[]string{"--trigger", "await-pause-point --id marker"}))

	requireRecordVideoCalls(t, fake, "focus", "start", "trigger:await-pause-point", "mark", "wait", "stop")
	if payload.OneShot.Warning != recordVideoPausingAwaitWarning {
		t.Fatalf("Warning = %q", payload.OneShot.Warning)
	}
}

// Verifies trigger failures skip the mark and the wait, stop at once, and fail with the recording
// attached: an await timeout, a pre-execution rejection, a dispatch failure, and a Unity-side
// Success:false without any stderr envelope.
func TestRunRecordVideoOneShot_TriggerFailures_StopWithoutMarkAndFail(t *testing.T) {
	cases := []struct {
		name    string
		trigger string
		stdout  string
		stderr  string
		exit    int
	}{
		{
			name:    "AwaitTimesOut_StopsWithoutMarkAndFails",
			trigger: "await-pause-point --id marker",
			stderr:  `{"Success":false,"Error":{"ErrorCode":"PAUSE_POINT_WAIT_TIMEOUT"}}`,
			exit:    1,
		},
		{
			name:    "TriggerRejected_StopsWithoutMarkAndFails",
			trigger: "simulate-keyboard --key C",
			stderr:  `{"Success":false,"Error":{"ErrorCode":"INVALID_ARGUMENT"}}`,
			exit:    1,
		},
		{
			name:    "TriggerFailed_StopsWithoutMarkAndFails",
			trigger: "simulate-keyboard --key C",
			stderr:  `{"Success":false,"Error":{"ErrorCode":"UNITY_NOT_REACHABLE"}}`,
			exit:    1,
		},
		{
			name:    "TriggerUnitySuccessFalse_StopsWithoutMarkAndFails",
			trigger: "simulate-keyboard --key C",
			stdout:  `{"Success":false,"Message":"Play Mode is required."}`,
			exit:    1,
		},
	}
	for _, testCase := range cases {
		t.Run(testCase.name, func(t *testing.T) {
			fake := newRecordVideoOneShotFake()
			fake.triggerStdout = testCase.stdout
			fake.triggerStderr = testCase.stderr
			fake.triggerExit = testCase.exit
			fake.install(t)

			recording := decodeRecordVideoOneShotFailure(t,
				runRecordVideoOneShotForTest(context.Background(), []string{"--trigger", testCase.trigger}),
				recordVideoTriggerFailedErrorCode)

			command := strings.Fields(testCase.trigger)[0]
			requireRecordVideoCalls(t, fake, "focus", "start", "trigger:"+command, "stop")
			if recording.StoppedBy != "cli" || len(recording.OneShot.TriggerResult) == 0 || recording.OneShot.MarkAccepted != nil {
				t.Fatalf("recording = %+v", recording)
			}
		})
	}
}

// Verifies a start that Unity rejects is reported exactly as a plain start reports it, and nothing
// else is sent: no trigger, no mark, no stop.
func TestRunRecordVideoOneShot_StartFails_DoesNotTriggerOrStop(t *testing.T) {
	const startFailure = `{"Success":false,"Message":"Game View recording requires Play Mode."}`
	originalFocus := runFocusWindowForRecordVideo
	runFocusWindowForRecordVideo = func(context.Context, string, io.Writer, io.Writer) int { return 0 }
	originalDispatch := dispatchPausePointTriggerCommand
	dispatchPausePointTriggerCommand = func(context.Context, unityipc.Connection, string, []string, string, io.Writer, io.Writer) int {
		t.Fatal("the trigger must not run after a failed start")
		return 1
	}
	t.Cleanup(func() {
		runFocusWindowForRecordVideo = originalFocus
		dispatchPausePointTriggerCommand = originalDispatch
	})

	oneShotRoot := t.TempDir()
	oneShotServer := startFakeUnityResultServer(t, oneShotRoot, "record-video", startFailure)
	var oneShotStdout, oneShotStderr bytes.Buffer
	oneShotCode := runResolvedProjectCommand(context.Background(), oneShotServer.connection, "record-video",
		[]string{"--seconds", "2", "--trigger", "simulate-keyboard --key C"}, oneShotRoot, &oneShotStdout, &oneShotStderr)
	if request := oneShotServer.receivedRequest(t); request["Action"] != "start" {
		t.Fatalf("one-shot sent %#v, want a start", request)
	}

	plainRoot := t.TempDir()
	plainServer := startFakeUnityResultServer(t, plainRoot, "record-video", startFailure)
	var plainStdout, plainStderr bytes.Buffer
	plainCode := runResolvedProjectCommand(context.Background(), plainServer.connection, "record-video",
		[]string{"--action", "start"}, plainRoot, &plainStdout, &plainStderr)
	plainServer.receivedRequest(t)

	if oneShotCode != 1 || plainCode != 1 {
		t.Fatalf("exit codes = %d / %d, want 1 / 1", oneShotCode, plainCode)
	}
	if oneShotStdout.String() != plainStdout.String() || oneShotStderr.String() != plainStderr.String() {
		t.Fatalf("one-shot output differs from a plain start:\nstdout %q vs %q\nstderr %q vs %q",
			oneShotStdout.String(), plainStdout.String(), oneShotStderr.String(), plainStderr.String())
	}
}

// Verifies a recording that auto-stopped during the wait is reported as a success with its reason.
func TestRunRecordVideoOneShot_AutoStoppedDuringWait_ReportsStoppedBy(t *testing.T) {
	fake := newRecordVideoOneShotFake()
	fake.stopResponse = `{"Success":true,"StoppedBy":"play-mode-exit"}`
	fake.install(t)

	payload := decodeRecordVideoOneShotStdout(t, runRecordVideoOneShotForTest(context.Background(), []string{"--seconds", "3"}))

	if payload.StoppedBy != "play-mode-exit" {
		t.Fatalf("StoppedBy = %q", payload.StoppedBy)
	}
}

// Verifies a stop that finds no recording (another caller stopped it) fails instead of reading as
// a recorded clip, with the stop response attached.
func TestRunRecordVideoOneShot_StoppedExternallyDuringWait_Fails(t *testing.T) {
	fake := newRecordVideoOneShotFake()
	fake.stopResponse = `{"Success":false,"Message":"No recording is in progress."}`
	fake.install(t)

	recording := decodeRecordVideoOneShotFailure(t,
		runRecordVideoOneShotForTest(context.Background(), []string{"--seconds", "3"}), recordVideoStopFailedErrorCode)

	if recording.Message != "No recording is in progress." || recording.OneShot.Seconds != 3 {
		t.Fatalf("recording = %+v", recording)
	}
}

// Verifies a cancellation at any step after start still stops the recording with a context the
// cancellation cannot cut short, skips the remaining steps, and fails with the recording attached.
func TestRunRecordVideoOneShot_Cancelled_StillStops(t *testing.T) {
	cases := []struct {
		name      string
		args      []string
		wantCalls []string
		arm       func(fake *recordVideoOneShotFake, cancel context.CancelFunc)
	}{
		{
			name:      "CancelledDuringTrigger_StillStops",
			args:      []string{"--trigger", "simulate-keyboard --key C"},
			wantCalls: []string{"focus", "start", "trigger:simulate-keyboard", "stop"},
			arm:       func(fake *recordVideoOneShotFake, cancel context.CancelFunc) { fake.onTrigger = cancel },
		},
		{
			name:      "CancelledDuringMark_StillStops",
			args:      []string{"--trigger", "simulate-keyboard --key C"},
			wantCalls: []string{"focus", "start", "trigger:simulate-keyboard", "mark", "stop"},
			arm:       func(fake *recordVideoOneShotFake, cancel context.CancelFunc) { fake.onMark = cancel },
		},
		{
			name:      "CancelledDuringWait_StillStops",
			args:      []string{"--seconds", "3"},
			wantCalls: []string{"focus", "start", "wait", "stop"},
			arm: func(fake *recordVideoOneShotFake, cancel context.CancelFunc) {
				fake.onWait = func(ctx context.Context) error {
					cancel()
					return ctx.Err()
				}
			},
		},
	}
	for _, testCase := range cases {
		t.Run(testCase.name, func(t *testing.T) {
			ctx, cancel := context.WithCancel(context.Background())
			defer cancel()
			fake := newRecordVideoOneShotFake()
			testCase.arm(fake, cancel)
			fake.install(t)

			recording := decodeRecordVideoOneShotFailure(t, runRecordVideoOneShotForTest(ctx, testCase.args), recordVideoCancelledErrorCode)

			requireRecordVideoCalls(t, fake, testCase.wantCalls...)
			if fake.stopCtxErr != nil {
				t.Fatalf("stop was sent with a cancelled context: %v", fake.stopCtxErr)
			}
			if recording.StoppedBy != "cli" {
				t.Fatalf("recording = %+v", recording)
			}
		})
	}
}

// Verifies a failed focus-window is noted and does not stop the one-shot, and a successful one adds
// no note.
func TestRunRecordVideoOneShot_FocusWindow_NotesOnlyFailure(t *testing.T) {
	for _, testCase := range []struct {
		name      string
		focusCode int
		wantNote  string
	}{
		{name: "FocusWindowFails_ContinuesWithNote", focusCode: 1, wantNote: recordVideoFocusWindowNote},
		{name: "FocusWindowSucceeds_NoNote", focusCode: 0, wantNote: ""},
	} {
		t.Run(testCase.name, func(t *testing.T) {
			fake := newRecordVideoOneShotFake()
			fake.focusCode = testCase.focusCode
			fake.install(t)

			payload := decodeRecordVideoOneShotStdout(t, runRecordVideoOneShotForTest(context.Background(), []string{"--seconds", "3"}))

			requireRecordVideoCalls(t, fake, "focus", "start", "wait", "stop")
			if payload.OneShot.FocusWindowNote != testCase.wantNote {
				t.Fatalf("FocusWindowNote = %q, want %q", payload.OneShot.FocusWindowNote, testCase.wantNote)
			}
		})
	}
}

// Verifies an invalid one-shot flag, including an empty value (an unset shell variable), is
// reported before anything is focused or sent, instead of being read as an absent flag.
func TestRunRecordVideoCommand_InvalidOneShotFlag_SendsNothing(t *testing.T) {
	for _, testCase := range []struct {
		args     []string
		wantText string
	}{
		{args: []string{"--seconds", "0"}, wantText: "--seconds must be an integer"},
		{args: []string{"--trigger", ""}, wantText: "--trigger requires a value"},
		{args: []string{"--seconds", ""}, wantText: "--seconds requires a value"},
		{args: []string{"--seconds", "3", "--trigger", ""}, wantText: "--trigger requires a value"},
	} {
		fake := newRecordVideoOneShotFake()
		fake.install(t)

		run := runRecordVideoOneShotForTest(context.Background(), testCase.args)

		if run.code != 1 || !strings.Contains(run.stderr, "INVALID_ARGUMENT") || !strings.Contains(run.stderr, testCase.wantText) {
			t.Fatalf("args %q: run = %+v", testCase.args, run)
		}
		requireRecordVideoCalls(t, fake)
	}
}

type recordVideoCommandOutput struct {
	code   int
	stdout string
	stderr string
}

func runRecordVideoAgainstFakeUnity(
	t *testing.T,
	response string,
	run func(connection unityipc.Connection, root string, stdout io.Writer, stderr io.Writer) int,
) recordVideoCommandOutput {
	t.Helper()
	root := t.TempDir()
	server := startFakeUnityResultServer(t, root, "record-video", response)
	var stdout, stderr bytes.Buffer
	code := run(server.connection, root, &stdout, &stderr)
	server.receivedRequest(t)
	return recordVideoCommandOutput{code: code, stdout: stdout.String(), stderr: stderr.String()}
}

// Verifies record-video without the one-shot flags writes byte-for-byte what the plain tool call
// writes, for every action.
func TestRunRecordVideoCommand_WithoutOneShotFlags_PassesThrough(t *testing.T) {
	for _, action := range []string{"start", "stop", "status", "mark"} {
		t.Run(action, func(t *testing.T) {
			args := []string{"--action", action}
			response := `{"Success":true,"Message":"` + action + ` done","OutputPath":"clip.mp4"}`

			routed := runRecordVideoAgainstFakeUnity(t, response,
				func(connection unityipc.Connection, root string, stdout io.Writer, stderr io.Writer) int {
					return runResolvedProjectCommand(context.Background(), connection, "record-video", args, root, stdout, stderr)
				})
			plain := runRecordVideoAgainstFakeUnity(t, response,
				func(connection unityipc.Connection, root string, stdout io.Writer, stderr io.Writer) int {
					return runDynamicProjectTool(context.Background(), connection, "record-video", args, root, stdout, stderr)
				})

			if routed != plain {
				t.Fatalf("routed output differs from the plain tool call:\n%+v\n%+v", routed, plain)
			}
			if routed.code != 0 || !strings.Contains(routed.stdout, action+" done") {
				t.Fatalf("routed = %+v", routed)
			}
		})
	}
}

// Verifies invalid values of record-video's own options are reported byte-for-byte as before when
// the one-shot flags are absent.
func TestRunRecordVideoCommand_PassesThroughInvalidValuesUntouched(t *testing.T) {
	for _, args := range [][]string{{"--max-duration-seconds", "abc"}, {"--action", "bogus"}} {
		var routedStdout, routedStderr, plainStdout, plainStderr bytes.Buffer
		connection := unreachableConnection(t.TempDir())

		routedCode := runResolvedProjectCommand(context.Background(), connection, "record-video", args,
			connection.ProjectRoot, &routedStdout, &routedStderr)
		plainCode := runDynamicProjectTool(context.Background(), connection, "record-video", args,
			connection.ProjectRoot, &plainStdout, &plainStderr)

		if routedCode != 1 || plainCode != 1 {
			t.Fatalf("args %q: exit codes = %d / %d", args, routedCode, plainCode)
		}
		if routedStdout.String() != plainStdout.String() || routedStderr.String() != plainStderr.String() {
			t.Fatalf("args %q: output differs:\n%q\n%q", args, routedStderr.String(), plainStderr.String())
		}
		if !strings.Contains(routedStderr.String(), "INVALID_ARGUMENT") {
			t.Fatalf("args %q: stderr = %q", args, routedStderr.String())
		}
	}
}

// Verifies the default mark/stop sender sends the action to Unity and returns the response without
// writing anything, and rejects arguments the catalog does not accept before Unity.
func TestSendRecordVideoRequestDefault_SendsActionAndRejectsUnknownValues(t *testing.T) {
	root := t.TempDir()
	server := startFakeUnityResultServer(t, root, "record-video", `{"Success":true,"MarkSeconds":[0.5]}`)

	result, err := sendRecordVideoRequestDefault(context.Background(), server.connection, recordVideoActionArgs("mark"), root)

	if err != nil || result.exitCode != 0 || !strings.Contains(string(result.result), "MarkSeconds") {
		t.Fatalf("result = %+v, err = %v", result, err)
	}
	if request := server.receivedRequest(t); request["Action"] != "mark" {
		t.Fatalf("sent %#v, want Action mark", request)
	}

	_, err = sendRecordVideoRequestDefault(context.Background(), unreachableConnection(root), recordVideoActionArgs("bogus"), root)
	if err != errRecordVideoRequestRejected {
		t.Fatalf("err = %v, want errRecordVideoRequestRejected", err)
	}
}

// Verifies a stop that cannot reach Unity fails with the classified error instead of a recording.
func TestRunRecordVideoOneShot_StopRequestFails_ReportsError(t *testing.T) {
	fake := newRecordVideoOneShotFake()
	fake.install(t)
	sendRecordVideoRequest = func(context.Context, unityipc.Connection, []string, string) (toolExecutionResult, error) {
		fake.calls = append(fake.calls, "stop")
		return toolExecutionResult{exitCode: 1}, errRecordVideoRequestRejected
	}

	run := runRecordVideoOneShotForTest(context.Background(), []string{"--seconds", "3"})

	if run.code != 1 || run.stdout != "" || !strings.Contains(run.stderr, errRecordVideoRequestRejected.Error()) {
		t.Fatalf("run = %+v", run)
	}
	requireRecordVideoCalls(t, fake, "focus", "start", "wait", "stop")
}
