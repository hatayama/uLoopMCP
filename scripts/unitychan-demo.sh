#!/bin/sh
# Automated UnityChan demo choreography
# Moves in loops around the center to stay on the ground plane

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

# --- Walk forward, then walk back (return to center) ---
echo "[2] Walk forward and back"
hold_for 2.0 LeftShift W
sleep 0.3
hold_for 2.0 LeftShift S
sleep 0.5

# --- Sprint right, jump, sprint back left ---
echo "[3] Sprint right with jump"
hold D
sleep 1.5
press Space 0.3
sleep 2.0
release D
hold_for 1.5 A
sleep 0.5

# --- Diagonal sprint: forward-right, then back-left (return) ---
echo "[4] Diagonal sprint loop"
hold_for 1.5 W D
sleep 0.3
hold_for 1.5 S A
sleep 0.5

# --- Walk a square (stays in same area) ---
echo "[5] Walk in a square"
hold LeftShift
hold_for 1.2 W
hold_for 1.2 D
hold_for 1.2 S
hold_for 1.2 A
release LeftShift
sleep 0.5

# --- Sprint forward, double jump, sprint back ---
echo "[6] Sprint and double jump"
hold W
sleep 1.0
press Space 0.3
sleep 2.0
press Space 0.3
sleep 2.0
release W
hold_for 2.0 S
sleep 0.5

# --- Finale: diagonal walk, then idle jump ---
echo "[Finale] Closing"
hold_for 1.5 LeftShift W D
sleep 0.3
hold_for 1.5 LeftShift S A
sleep 0.5
press Space 0.3
sleep 2.5

echo ""
echo "=== Demo Complete! ==="
