#!/bin/sh
# Automated UnityChan demo - walk, run, jump around the center
# Moves in short loops to stay on the ground plane

set -e

cleanup() {
    uloop control-play-mode --action Stop 2>/dev/null
}
trap cleanup EXIT INT TERM

press() {
    uloop simulate-keyboard --action Press --key "$1" --duration "${2:-0.5}"
}

hold_for() {
    duration="$1"
    shift
    for key in "$@"; do
        uloop simulate-keyboard --action KeyDown --key "$key"
    done
    sleep "$duration"
    for key in "$@"; do
        uloop simulate-keyboard --action KeyUp --key "$key"
    done
}

hold() {
    uloop simulate-keyboard --action KeyDown --key "$1"
}

release() {
    uloop simulate-keyboard --action KeyUp --key "$1"
}

echo "=== UnityChan Demo ==="

echo "[Setup] Starting PlayMode..."
uloop control-play-mode --action Play
sleep 2
uloop focus-window
sleep 0.5

# --- Idle jump ---
echo "[1] Idle jump"
press Space 0.3
sleep 2.0

# --- Walk a small circle (W→D→S→A) ---
echo "[2] Walk circle"
hold LeftShift
hold_for 1.0 W
hold_for 1.0 D
hold_for 1.0 S
hold_for 1.0 A
release LeftShift
sleep 0.3

# --- Walk forward, jump mid-walk, walk back ---
echo "[3] Walking jump"
hold LeftShift
hold W
sleep 1.0
press Space 0.3
sleep 2.0
release W
hold_for 1.0 S
release LeftShift
sleep 0.3

# --- Run a small circle (W→D→S→A) ---
echo "[4] Run circle"
hold_for 0.8 W
hold_for 0.8 D
hold_for 0.8 S
hold_for 0.8 A
sleep 0.3

# --- Run forward, jump, run back ---
echo "[5] Running jump"
hold W
sleep 0.8
press Space 0.3
sleep 2.0
release W
hold_for 0.8 S
sleep 0.3

# --- Diagonal walk circle (WD→DS→SA→AW) ---
echo "[6] Diagonal walk circle"
hold LeftShift
hold_for 1.0 W D
hold_for 1.0 D S
hold_for 1.0 S A
hold_for 1.0 A W
release LeftShift
sleep 0.3

# --- Run diagonal with jump ---
echo "[7] Diagonal run + jump"
hold_for 0.8 W D
press Space 0.3
sleep 2.0
hold_for 0.8 S A
sleep 0.3

# --- Final lap: run circle with double jump ---
echo "[Finale] Run circle with jumps"
hold W
sleep 0.8
press Space 0.3
sleep 2.0
release W
hold D
sleep 0.8
release D
hold S
sleep 0.8
press Space 0.3
sleep 2.0
release S
hold_for 0.8 A
sleep 0.5

# --- End with idle jump ---
echo "  Final jump!"
press Space 0.3
sleep 2.5

echo ""
echo "=== Demo Complete! ==="
