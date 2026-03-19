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

# --- Phase 1: Look around (SmoothDelta) ---
log "Looking around (SmoothDelta)..."
look 200 0 0.6
wait_sec 0.2
look -400 0 1.0
wait_sec 0.2
look 200 0 0.6
wait_sec 0.3

# --- Phase 2: Shoot with default color (Click - Left) ---
log "Shooting (Left Click) - Yellow bullets"
shoot
wait_sec 0.3
shoot
wait_sec 0.3
shoot
wait_sec 0.5

# --- Phase 3: Switch weapon color (Scroll Up) ---
log "Switching weapon color (Scroll Up)..."
scroll 120
wait_sec 0.5

log "Shooting - Red bullets"
shoot
wait_sec 0.3
shoot
wait_sec 0.5

# --- Phase 4: Switch again (Scroll Up) ---
log "Switching weapon color (Scroll Up)..."
scroll 120
wait_sec 0.5

log "Shooting - Blue bullets"
shoot
wait_sec 0.3
shoot
wait_sec 0.5

# --- Phase 5: Switch back (Scroll Down) ---
log "Switching weapon color (Scroll Down)..."
scroll -120
wait_sec 0.5

log "Shooting - Red bullets again"
shoot
wait_sec 0.3
shoot
wait_sec 0.5

# --- Phase 6: Pan camera + shoot (combined mouse actions) ---
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

# --- Phase 7: Right Click demo ---
log "Right Click demo..."
right_click
wait_sec 0.5

# --- Phase 8: Middle Click demo ---
log "Middle Click demo..."
middle_click
wait_sec 0.5

# --- Phase 9: Long Press demo ---
log "Long Press demo (1.5s)..."
long_press 1.5
wait_sec 0.5

# --- Phase 10: Rapid scroll cycle through all colors ---
log "Rapid weapon cycling..."
scroll 120
wait_sec 0.2
scroll 120
wait_sec 0.2
scroll 120
wait_sec 0.2
scroll 120
wait_sec 0.2
scroll 120
wait_sec 0.2

# --- Phase 11: Final burst with last color ---
log "Final burst!"
look -100 0 0.3
shoot
wait_sec 0.15
shoot
wait_sec 0.15
shoot
wait_sec 0.5

log "=== Mouse Input Demo Complete ==="
