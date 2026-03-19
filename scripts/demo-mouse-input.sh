#!/bin/sh
set -e
# Mouse input demo: look, shoot, and switch weapon colors with scroll wheel.
# Showcases simulate-mouse-input tool (Click, SmoothDelta, Scroll, LongPress).
#
# Usage: sh scripts/demo-mouse-input.sh [--project-path <path>]
#
# Prerequisites:
#   - SimulateMouseInputDemoScene must be open in Unity
#   - uloop CLI must be installed

PROJECT_PATH=""
if [ "$1" = "--project-path" ] && [ -n "$2" ]; then
    PROJECT_PATH="$2"
fi

cleanup() {
    printf "\033[35m[mouse]\033[0m Stopping PlayMode (cleanup)...\n"
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
    printf "\033[35m[mouse]\033[0m %s\n" "$1"
}

look() {
    run_uloop simulate-mouse-input --action SmoothDelta --delta-x "$1" --delta-y "${2:-0}" --duration "${3:-0.5}" > /dev/null
}

shoot() {
    run_uloop simulate-mouse-input --action Click --x 400 --y 300 > /dev/null
}

right_click() {
    run_uloop simulate-mouse-input --action Click --x 400 --y 300 --button Right > /dev/null
}

middle_click() {
    run_uloop simulate-mouse-input --action Click --x 400 --y 300 --button Middle > /dev/null
}

long_press() {
    run_uloop simulate-mouse-input --action LongPress --x 400 --y 300 --duration "${1:-1.0}" > /dev/null
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

log "=== Mouse Input Demo Start ==="

# --- Phase 1: Click demos (Left, Right, Middle) ---
log "Left Click x3..."
shoot
wait_sec 0.5
shoot
wait_sec 0.5
shoot
wait_sec 0.8

log "Right Click..."
right_click
wait_sec 0.8

log "Middle Click..."
middle_click
wait_sec 0.8

log "Scroll Up..."
scroll 120
wait_sec 0.8

log "Scroll Down..."
scroll -120
wait_sec 0.8

log "Left Click rapid fire..."
shoot
wait_sec 0.2
shoot
wait_sec 0.2
shoot
wait_sec 0.2
shoot
wait_sec 0.8

# --- Phase 2: Look around (SmoothDelta) ---
log "Looking around (SmoothDelta)..."
look 200 0 0.6
wait_sec 0.2
look -400 0 1.0
wait_sec 0.2
look 200 0 0.6
wait_sec 0.3

# --- Phase 3: Shoot with default color ---
log "Shooting - Yellow bullets"
shoot
wait_sec 0.3
shoot
wait_sec 0.3
shoot
wait_sec 0.5

# --- Phase 3-8: Color switch + shoot cycle ---
log "Switch to Red..."
scroll 120
wait_sec 0.4
shoot
wait_sec 0.3
shoot
wait_sec 0.3
shoot
wait_sec 0.5

log "Switch to Blue..."
scroll 120
wait_sec 0.4
look -100 0 0.3
shoot
wait_sec 0.3
shoot
wait_sec 0.5

log "Switch to Green..."
scroll 120
wait_sec 0.4
shoot
wait_sec 0.3
shoot
wait_sec 0.3
shoot
wait_sec 0.5

log "Switch to Orange..."
scroll 120
wait_sec 0.4
look 150 0 0.4
shoot
wait_sec 0.3
shoot
wait_sec 0.5

log "Switch to Magenta..."
scroll 120
wait_sec 0.4
shoot
wait_sec 0.3
shoot
wait_sec 0.3
shoot
wait_sec 0.5

log "Back to Yellow..."
scroll 120
wait_sec 0.4
shoot
wait_sec 0.3
shoot
wait_sec 0.5

# --- Phase 9: Pan camera + shoot ---
log "Pan and shoot combo..."
look 150 0 0.4
wait_sec 0.1
shoot
wait_sec 0.2
look -300 0 0.6
wait_sec 0.1
shoot
wait_sec 0.2
shoot
wait_sec 0.5

# --- Phase 10: Long Press demo ---
log "Long Press demo (1.5s)..."
long_press 1.5
wait_sec 0.5

# --- Phase 11: Final burst ---
log "Final burst!"
look -100 0 0.3
shoot
wait_sec 0.15
shoot
wait_sec 0.15
shoot
wait_sec 0.5

log "=== Mouse Input Demo Complete ==="
