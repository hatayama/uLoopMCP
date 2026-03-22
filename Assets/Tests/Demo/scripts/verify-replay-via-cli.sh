#!/bin/sh
# E2E verification: human plays freely, then CLI replays and verifies.
#
# Usage: sh verify-replay-via-cli.sh [--project-path <path>]
#
# Prerequisites:
#   - Unity Editor running with InputReplayVerificationScene loaded
#   - PlayMode is NOT running (script starts it)

set -e

PROJECT_OPTS=""
if [ "$1" = "--project-path" ] && [ -n "$2" ]; then
    PROJECT_OPTS="--project-path $2"
fi

RECORDING_LOG=".uloop/outputs/InputRecordings/recording-event-log.txt"
REPLAY_LOG=".uloop/outputs/InputRecordings/replay-event-log.txt"

wait_for_unity() {
    i=0
    while [ $i -lt 15 ]; do
        if uloop get-logs --max-count 1 $PROJECT_OPTS > /dev/null 2>&1; then
            return 0
        fi
        sleep 2
        i=$((i + 1))
    done
    echo "ERROR: Unity not responding"
    exit 1
}

# activate_controller uses Canvas/Transform.Find to locate children
# because GameObject.Find cannot find inactive GameObjects
activate_for_record() {
    uloop execute-dynamic-code $PROJECT_OPTS --code '
var cube = GameObject.Find("VerificationCube");
if (cube == null) return "ERROR: VerificationCube not found";
cube.SendMessage("ActivateForExternalControl");
var canvas = GameObject.Find("Canvas");
if (canvas == null) return "OK: activated (no canvas)";
var sp = canvas.transform.Find("StartPanel");
if (sp != null) sp.gameObject.SetActive(false);
var tp = canvas.transform.Find("StopPanel");
if (tp != null) tp.gameObject.SetActive(false);
return "OK: activated for recording";
'
}

activate_for_replay() {
    uloop execute-dynamic-code $PROJECT_OPTS --code '
var cube = GameObject.Find("VerificationCube");
if (cube == null) return "ERROR: VerificationCube not found";
cube.SendMessage("ActivateForExternalControl");
var canvas = GameObject.Find("Canvas");
if (canvas == null) return "OK: activated (no canvas)";
var sp = canvas.transform.Find("StartPanel");
if (sp != null) sp.gameObject.SetActive(false);
var tp = canvas.transform.Find("StopPanel");
if (tp != null) tp.gameObject.SetActive(false);
return "OK: activated for replay";
'
}

save_log() {
    uloop execute-dynamic-code $PROJECT_OPTS --code "
var cube = GameObject.Find(\"VerificationCube\");
if (cube == null) return \"ERROR: VerificationCube not found\";
cube.SendMessage(\"SaveLog\", \"$1\");
return \"OK: log saved\";
"
}

echo ""
echo "========================================="
echo "  Input Record/Replay E2E Verification"
echo "========================================="

# ---- Phase 1: Record human input ----

echo ""
echo "[1/8] Starting PlayMode..."
uloop control-play-mode --action Play $PROJECT_OPTS
echo "  Waiting for Unity..."
sleep 6
wait_for_unity

echo "[2/8] Activating controller..."
activate_for_record

echo "[3/8] Starting recording via CLI..."
uloop record-input --action Start $PROJECT_OPTS

echo ""
echo "========================================="
echo "  Recording is active!"
echo "  Go to the Unity Game View and play."
echo ""
echo "  WASD: move | Mouse: rotate"
echo "  Left click: red | Right click: blue"
echo "  Scroll: scale"
echo ""
echo "  Press ENTER here when done."
echo "========================================="
echo ""
read dummy

echo "[4/8] Stopping recording via CLI..."
uloop record-input --action Stop $PROJECT_OPTS

echo "  Saving recording event log..."
save_log ".uloop/outputs/InputRecordings/recording-event-log.txt"

# ---- Phase 2: Replay via CLI ----

echo "[5/8] Restarting PlayMode..."
uloop control-play-mode --action Stop $PROJECT_OPTS
sleep 3
uloop control-play-mode --action Play $PROJECT_OPTS
echo "  Waiting for Unity..."
sleep 6
wait_for_unity

echo "[6/8] Activating controller + starting replay via CLI..."
activate_for_replay
echo "  Starting replay..."
REPLAY_RESULT=$(uloop replay-input --action Start $PROJECT_OPTS 2>&1) || true
echo "  $REPLAY_RESULT"

echo "  Waiting for replay to finish..."
waited=0
while [ $waited -lt 60 ]; do
    STATUS_RESULT=$(uloop replay-input --action Status $PROJECT_OPTS 2>&1) || true
    playing=$(echo "$STATUS_RESULT" | grep -o '"IsReplaying":[a-z]*' | cut -d: -f2)
    if [ "$playing" = "false" ]; then
        echo "  Replay completed."
        break
    fi
    if [ $((waited % 5)) -eq 0 ]; then
        progress=$(echo "$STATUS_RESULT" | grep -o '"Progress":[0-9.]*' | cut -d: -f2)
        echo "  Progress: $progress"
    fi
    sleep 1
    waited=$((waited + 1))
done
echo ""

if [ $waited -ge 60 ]; then
    echo "ERROR: Replay did not complete within 60s"
    echo "  Last status: $STATUS_RESULT"
    exit 1
fi
sleep 1

echo "[7/8] Saving replay event log..."
save_log ".uloop/outputs/InputRecordings/replay-event-log.txt"

# ---- Phase 3: Compare ----

echo ""
echo "[8/8] Comparing logs..."
echo ""

# Normalize frame numbers to relative (first event = frame 0).
# CLI commands introduce variable delays, so absolute frame numbers
# differ, but relative timing between events should be identical.
normalize_frames() {
    awk 'NR==1 { match($0, /Frame ([0-9]+):/, a); base=a[1] }
         { match($0, /Frame ([0-9]+):(.*)/, a); printf "Frame %d:%s\n", a[1]-base, a[2] }' "$1"
}

normalize_frames "$RECORDING_LOG" > "$RECORDING_LOG.norm"
normalize_frames "$REPLAY_LOG" > "$REPLAY_LOG.norm"

if diff "$RECORDING_LOG.norm" "$REPLAY_LOG.norm" > /dev/null 2>&1; then
    lines=$(wc -l < "$RECORDING_LOG.norm" | tr -d ' ')
    echo "========================================="
    echo "  RESULT: MATCH ($lines events identical)"
    echo "  Relative frame timing verified."
    echo "========================================="
    echo ""
    rm -f "$RECORDING_LOG.norm" "$REPLAY_LOG.norm"
    exit 0
else
    cnt=$(diff "$RECORDING_LOG.norm" "$REPLAY_LOG.norm" | grep -c '^[<>]' || true)
    echo "========================================="
    echo "  RESULT: MISMATCH ($cnt differences)"
    echo "========================================="
    echo ""
    diff "$RECORDING_LOG.norm" "$REPLAY_LOG.norm" | head -20
    echo ""
    rm -f "$RECORDING_LOG.norm" "$REPLAY_LOG.norm"
    exit 1
fi
