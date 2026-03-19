#!/bin/sh
set -e
# Combined keyboard + mouse input demo.
# Demonstrates WASD movement with mouse look happening simultaneously,
# verifying both overlay visualizations work together.
#
# Usage: sh scripts/demo-combined-input.sh [--project-path <path>]
#
# Prerequisites:
#   - SimulateKeyboardDemoScene must be open in Unity
#   - uloop CLI must be installed

PROJECT_PATH=""
if [ "$1" = "--project-path" ] && [ -n "$2" ]; then
    PROJECT_PATH="$2"
fi

cleanup() {
    printf "\033[33m[combined]\033[0m Stopping PlayMode (cleanup)...\n"
    run_uloop control-play-mode --action Stop > /dev/null 2>&1 || true
}
trap cleanup EXIT

run_uloop() {
    if [ -n "$PROJECT_PATH" ]; then
        uloop "$@" --project-path "$PROJECT_PATH"
    else
        uloop "$@"
    fi
}

log() {
    printf "\033[33m[combined]\033[0m %s\n" "$1"
}

look() {
    run_uloop simulate-mouse-input --action SmoothDelta --delta-x "$1" --delta-y "${2:-0}" --duration "${3:-0.5}" > /dev/null
}

shoot() {
    run_uloop simulate-mouse-input --action Click --x 400 --y 300 > /dev/null
}

middle_click() {
    run_uloop simulate-mouse-input --action Click --x 400 --y 300 --button Middle > /dev/null
}

scroll() {
    run_uloop simulate-mouse-input --action Scroll --scroll-y "$1" > /dev/null
}

key_press() {
    run_uloop simulate-keyboard --action Press --key "$1" --duration "${2:-0.1}" > /dev/null
}

key_down() {
    run_uloop simulate-keyboard --action KeyDown --key "$1" > /dev/null
}

key_up() {
    run_uloop simulate-keyboard --action KeyUp --key "$1" > /dev/null
}

wait_sec() {
    sleep "$1"
}

# ============================================================
log "Starting PlayMode..."
run_uloop control-play-mode --action Play > /dev/null
wait_sec 2

log "=== Combined Input Demo Start ==="

# --- Phase 1: Walk forward + look around ---
log "Walking forward while looking around..."
key_down W
look 200 0 0.8
look -400 0 1.2
look 200 0 0.8
key_up W
wait_sec 0.3

# --- Phase 2: Strafe right + pan camera left ---
log "Strafing right while panning left..."
key_down D
look -300 0 1.0
key_up D
wait_sec 0.3

# --- Phase 3: Walk + shoot ---
log "Walking forward and shooting..."
key_down W
wait_sec 0.3
shoot
wait_sec 0.3
shoot
wait_sec 0.3
shoot
key_up W
wait_sec 0.3

# --- Phase 4: Sprint + look ---
log "Sprinting while scanning..."
key_down LeftShift
key_down W
look 400 0 1.5
key_up W
key_up LeftShift
wait_sec 0.3

# --- Phase 5: Strafe left + shoot + look ---
log "Strafing left, shooting, looking right..."
key_down A
look 200 0 0.4
shoot
wait_sec 0.2
shoot
look 200 0 0.4
shoot
key_up A
wait_sec 0.3

# --- Phase 6: Walk backward + pan ---
log "Retreating while panning..."
key_down S
look -300 0 0.8
look 300 0 0.8
key_up S
wait_sec 0.3

# --- Phase 7: Color switch + walk + shoot ---
log "Switch color, walk, and shoot..."
scroll 120
wait_sec 0.3
key_down W
look -100 0 0.3
shoot
wait_sec 0.2
shoot
wait_sec 0.2
scroll 120
wait_sec 0.2
shoot
wait_sec 0.2
shoot
key_up W
wait_sec 0.3

# --- Phase 8: Jump while moving ---
log "Jump while walking forward..."
key_down W
wait_sec 0.3
middle_click
wait_sec 0.5
middle_click
wait_sec 0.5
key_up W
wait_sec 0.3

# --- Phase 9: Full combo - sprint + look + color switch + shoot ---
log "Full combo: sprint + look + color switch + shoot!"
scroll 120
key_down LeftShift
key_down W
look 300 0 0.6
shoot
wait_sec 0.2
look -200 0 0.4
shoot
wait_sec 0.2
scroll 120
shoot
wait_sec 0.2
look 100 0 0.3
shoot
key_up W
key_up LeftShift
wait_sec 0.3

# --- Phase 10: Panorama spin + rapid fire ---
log "360 spin with rapid fire..."
scroll -120
wait_sec 0.2
look 800 0 2.0 &
LOOK_PID=$!
wait_sec 0.3
shoot
wait_sec 0.3
shoot
wait_sec 0.3
shoot
wait_sec 0.3
shoot
wait_sec 0.3
shoot
wait $LOOK_PID
wait_sec 0.5

log "=== Combined Input Demo Complete ==="
