#!/bin/sh
# SimulateMouse Demo Scenario
# Click buttons to reach Total Clicks: 10, then drag each colored box to the DropZone.
#
# Usage: sh demo-scenario.sh [--project-path <path>]
#
# Prerequisites:
#   - Unity must be running with SimulateMouseDemoScene loaded
#   - PlayMode must be active

set -e

PROJECT_ARGS=""
if [ -n "$1" ] && [ "$1" = "--project-path" ]; then
    PROJECT_ARGS="--project-path $2"
fi

log() {
    printf "\n=== %s ===\n" "$1"
}

# Resolve coordinates by taking an annotated screenshot
log "Taking annotated screenshot"
SCREENSHOT_JSON=$(uloop screenshot --capture-mode rendering --annotate-elements $PROJECT_ARGS 2>&1)

# Extract SimX/SimY for each element by name
get_coord() {
    element_name="$1"
    coord="$2"
    printf '%s' "$SCREENSHOT_JSON" | jq -r ".AnnotatedElements[] | select(.Name == \"$element_name\") | .$coord"
}

BUTTON1_X=$(get_coord "ClickButton1" "SimX")
BUTTON1_Y=$(get_coord "ClickButton1" "SimY")
DROPZONE_X=$(get_coord "DropZone" "SimX")
DROPZONE_Y=$(get_coord "DropZone" "SimY")
REDBOX_X=$(get_coord "RedBox" "SimX")
REDBOX_Y=$(get_coord "RedBox" "SimY")
GREENBOX_X=$(get_coord "GreenBox" "SimX")
GREENBOX_Y=$(get_coord "GreenBox" "SimY")
BLUEBOX_X=$(get_coord "BlueBox" "SimX")
BLUEBOX_Y=$(get_coord "BlueBox" "SimY")

printf "ClickButton1: (%s, %s)\n" "$BUTTON1_X" "$BUTTON1_Y"
printf "DropZone:     (%s, %s)\n" "$DROPZONE_X" "$DROPZONE_Y"
printf "RedBox:       (%s, %s)\n" "$REDBOX_X" "$REDBOX_Y"
printf "GreenBox:     (%s, %s)\n" "$GREENBOX_X" "$GREENBOX_Y"
printf "BlueBox:      (%s, %s)\n" "$BLUEBOX_X" "$BLUEBOX_Y"

# Phase 1: Click button 10 times to reach Total Clicks: 10
log "Phase 1: Clicking ClickButton1 x10"
i=1
while [ "$i" -le 10 ]; do
    printf "  Click %d/10\n" "$i"
    uloop simulate-mouse --action Click --x "$BUTTON1_X" --y "$BUTTON1_Y" $PROJECT_ARGS
    i=$((i + 1))
done

# Take screenshot to verify count
log "Verifying click count"
uloop screenshot --capture-mode rendering $PROJECT_ARGS

# Phase 2: Drag each colored box to the DropZone
log "Phase 2: Dragging RedBox to DropZone"
uloop simulate-mouse --action Drag \
    --x "$REDBOX_X" --y "$REDBOX_Y" \
    --end-x "$DROPZONE_X" --end-y "$DROPZONE_Y" \
    --drag-speed 500 $PROJECT_ARGS

log "Phase 2: Dragging GreenBox to DropZone"
uloop simulate-mouse --action Drag \
    --x "$GREENBOX_X" --y "$GREENBOX_Y" \
    --end-x "$DROPZONE_X" --end-y "$DROPZONE_Y" \
    --drag-speed 500 $PROJECT_ARGS

log "Phase 2: Dragging BlueBox to DropZone"
uloop simulate-mouse --action Drag \
    --x "$BLUEBOX_X" --y "$BLUEBOX_Y" \
    --end-x "$DROPZONE_X" --end-y "$DROPZONE_Y" \
    --drag-speed 500 $PROJECT_ARGS

# Final verification screenshot
log "Taking final screenshot"
uloop screenshot --capture-mode rendering $PROJECT_ARGS

log "Demo scenario complete!"
