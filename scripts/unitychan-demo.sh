#!/bin/sh
# Automated UnityChan demo - walk, run, jump around the center

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

uloop control-play-mode --action Play
sleep 2
# uloop focus-window
# sleep 0.5

# Idle jump
press Space 0.3
sleep 2.0

# Walk circle with jump mid-walk
hold LeftShift
hold W
sleep 1.0
press Space 0.3
sleep 2.0
release W
hold_for 1.0 D
hold_for 1.0 S
hold_for 1.0 A
release LeftShift
sleep 0.3

# Run circle with jump mid-run
hold W
sleep 0.8
press Space 0.3
sleep 2.0
release W
hold_for 0.8 D
hold_for 0.8 S
hold_for 0.8 A
sleep 0.3

# Diagonal run + jump mid-run, return
hold W
hold D
sleep 0.8
press Space 0.3
sleep 2.0
release D
release W
hold_for 0.8 S A
sleep 0.5

# Final idle jump
press Space 0.3
sleep 2.5

echo "=== Demo Complete! ==="
