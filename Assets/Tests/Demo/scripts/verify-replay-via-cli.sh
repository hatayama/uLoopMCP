#!/bin/sh
# E2E verification of record-input / replay-input through the full CLI stack.
# Tests: CLI -> TCP -> Unity -> InputRecorder/InputReplayer -> game logic -> event log comparison
#
# Usage: sh verify-replay-via-cli.sh [--project-path <path>]
#
# Prerequisites:
#   - Unity Editor running with uLoopMCP and InputReplayVerificationScene loaded
#   - uloop CLI installed and connected

set -e

PROJECT_OPTS=""
if [ "$1" = "--project-path" ] && [ -n "$2" ]; then
    PROJECT_OPTS="--project-path $2"
fi

LOG_DIR=".uloop/outputs/InputRecordings"
RECORDING_LOG="$LOG_DIR/cli-recording-event-log.txt"
REPLAY_LOG="$LOG_DIR/cli-replay-event-log.txt"
CTRL_TYPE="io.github.hatayama.uLoopMCP.InputReplayVerificationController"

echo "=== Step 1: Enter PlayMode ==="
uloop control-play-mode --action Play $PROJECT_OPTS
sleep 2

echo "=== Step 2: Activate controller (no bridge, CLI controls recording) ==="
uloop execute-dynamic-code $PROJECT_OPTS --code "
var ctrl = FindObjectOfType<${CTRL_TYPE}>();
if (ctrl == null) return \"ERROR: Controller not found. Load InputReplayVerificationScene first.\";
ctrl.ActivateForExternalControl();
return \"Controller activated\";
"

echo "=== Step 3: Start recording via CLI ==="
uloop record-input --action Start $PROJECT_OPTS
sleep 1

echo "=== Step 4: Inject input via CLI ==="
uloop simulate-keyboard --action KeyDown --key A $PROJECT_OPTS
sleep 0.5
uloop simulate-keyboard --action KeyUp --key A $PROJECT_OPTS
sleep 0.2

uloop simulate-keyboard --action KeyDown --key W $PROJECT_OPTS
sleep 0.5
uloop simulate-keyboard --action KeyUp --key W $PROJECT_OPTS
sleep 0.2

uloop simulate-keyboard --action Press --key D --duration 0.3 $PROJECT_OPTS
sleep 0.2

echo "=== Step 5: Stop recording via CLI ==="
uloop record-input --action Stop $PROJECT_OPTS

echo "=== Step 6: Save recording event log ==="
uloop execute-dynamic-code $PROJECT_OPTS --code "
var ctrl = FindObjectOfType<${CTRL_TYPE}>();
ctrl.SaveLog(\"${RECORDING_LOG}\");
return \"Recording event log saved\";
"

echo "=== Step 7: Stop PlayMode ==="
uloop control-play-mode --action Stop $PROJECT_OPTS
sleep 2

echo "=== Step 8: Re-enter PlayMode ==="
uloop control-play-mode --action Play $PROJECT_OPTS
sleep 2

echo "=== Step 9: Activate controller for replay (with frame offset) ==="
uloop execute-dynamic-code $PROJECT_OPTS --code "
var ctrl = FindObjectOfType<${CTRL_TYPE}>();
if (ctrl == null) return \"ERROR: Controller not found\";
ctrl.ActivateForExternalReplay();
return \"Controller activated for replay\";
"

echo "=== Step 10: Start replay via CLI ==="
uloop replay-input --action Start $PROJECT_OPTS

echo "=== Step 11: Wait for replay to complete ==="
MAX_WAIT=60
WAITED=0
while [ $WAITED -lt $MAX_WAIT ]; do
    STATUS=$(uloop replay-input --action Status $PROJECT_OPTS 2>/dev/null) || true
    IS_REPLAYING=$(echo "$STATUS" | grep -o '"IsReplaying":[a-z]*' | cut -d: -f2)
    if [ "$IS_REPLAYING" = "false" ]; then
        echo "Replay completed."
        break
    fi
    sleep 1
    WAITED=$((WAITED + 1))
done
echo ""

if [ $WAITED -ge $MAX_WAIT ]; then
    echo "ERROR: Replay did not complete within ${MAX_WAIT}s"
    exit 1
fi

sleep 1

echo "=== Step 12: Save replay event log ==="
uloop execute-dynamic-code $PROJECT_OPTS --code "
var ctrl = FindObjectOfType<${CTRL_TYPE}>();
ctrl.SaveLog(\"${REPLAY_LOG}\");
return \"Replay event log saved\";
"

echo "=== Step 13: Compare logs ==="
echo ""
echo "Recording log: $RECORDING_LOG"
echo "Replay log:    $REPLAY_LOG"
echo ""

if diff "$RECORDING_LOG" "$REPLAY_LOG" > /dev/null 2>&1; then
    LINES=$(wc -l < "$RECORDING_LOG" | tr -d ' ')
    echo "RESULT: MATCH ($LINES lines identical)"
    echo "Full CLI stack (CLI -> TCP -> Unity) reproduces input accurately."
    exit 0
else
    echo "RESULT: MISMATCH"
    echo ""
    diff "$RECORDING_LOG" "$REPLAY_LOG" | head -30
    exit 1
fi
