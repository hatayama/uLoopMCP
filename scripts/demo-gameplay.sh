#!/bin/sh
set -e
# Automated gameplay demo using simulate-keyboard and simulate-mouse-input tools.
# Demonstrates a player exploring the scene, looking around, and shooting targets.
#
# Usage: sh scripts/demo-gameplay.sh [--project-path <path>]
#
# Prerequisites:
#   - SimulateKeyboardDemoScene must be open in Unity
#   - uloop CLI must be installed

PROJECT_PATH=""
if [ "$1" = "--project-path" ] && [ -n "$2" ]; then
    PROJECT_PATH="$2"
fi

cleanup() {
    printf "\033[36m[gameplay]\033[0m Stopping PlayMode (cleanup)...\n"
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
    printf "\033[36m[gameplay]\033[0m %s\n" "$1"
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

# Smooth camera pan using SmoothDelta (frame-interpolated inside Unity).
# Usage: look <total_delta_x> <duration_sec>
look() {
    run_uloop simulate-mouse-input --action SmoothDelta --delta-x "$1" --delta-y 0 --duration "${2:-0.5}" > /dev/null
}

shoot() {
    run_uloop simulate-mouse-input --action Click --x 400 --y 300 > /dev/null
}

scroll() {
    run_uloop simulate-mouse-input --action Scroll --scroll-y "$1" > /dev/null
}

wait_sec() {
    sleep "$1"
}

# ============================================================
log "Starting PlayMode..."
run_uloop control-play-mode --action Play > /dev/null
wait_sec 2

log "=== Gameplay Demo Start ==="

# --- Phase 1: Look around ---
log "Looking around..."
look 300 0.8
wait_sec 0.2
look -600 1.2
wait_sec 0.2
look 300 0.8
wait_sec 0.3

# --- Phase 2: Walk forward toward targets ---
log "Walking forward..."
key_down W
wait_sec 1.5
key_up W
wait_sec 0.3

# --- Phase 3: Shoot! ---
log "Taking aim..."
look 50 0.3
wait_sec 0.1

log "Fire!"
shoot
wait_sec 0.3
shoot
wait_sec 0.3
shoot
wait_sec 0.5

# --- Phase 4: Strafe right and shoot ---
log "Strafing right..."
key_down D
wait_sec 0.8
key_up D
wait_sec 0.2

log "Turning to target..."
look -200 0.6
wait_sec 0.2

log "Fire!"
shoot
wait_sec 0.2
shoot
wait_sec 0.5

# --- Phase 5: Sprint forward (Shift + W) ---
log "Sprinting forward!"
key_down LeftShift
key_down W
wait_sec 1.5
key_up W
key_up LeftShift
wait_sec 0.3

# --- Phase 6: Look around panorama ---
log "Panoramic view..."
look 750 2.0
wait_sec 0.3

# --- Phase 7: Rapid fire while backing up ---
log "Suppressive fire while retreating!"
key_down S
shoot
wait_sec 0.2
shoot
wait_sec 0.2
look -100 0.3
shoot
wait_sec 0.2
shoot
wait_sec 0.2
look 100 0.3
shoot
key_up S
wait_sec 0.5

# --- Phase 8: Scroll (hotbar demo) ---
log "Scrolling hotbar..."
scroll 120
wait_sec 0.3
scroll 120
wait_sec 0.3
scroll -120
wait_sec 0.5

# --- Phase 9: Final charge + triple shot ---
log "Final charge!"
key_down W
wait_sec 0.5
look -50 0.3
shoot
wait_sec 0.15
shoot
wait_sec 0.15
shoot
key_up W
wait_sec 0.5

log "=== Gameplay Demo Complete ==="
