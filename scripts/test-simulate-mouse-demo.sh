#!/bin/sh
# SimulateMouse demo scenario: clicks buttons, long-presses, drags boxes,
# split-drags through waypoints, and operates the virtual pad.
# Requires: Unity running with SimulateMouseDemoScene in PlayMode.

set -e

cleanup() {
    uloop control-play-mode --action Stop 2>/dev/null
}
trap cleanup EXIT INT TERM

# --- Coordinates (SimulateMouseDemoScene at 16:9 / 1584x891) ---

CB1_X=274  CB1_Y=210
CB2_X=538  CB2_Y=210
LP_X=861   LP_Y=210
DZ_X=564   DZ_Y=446
RED_X=399  RED_Y=770
GREEN_X=564 GREEN_Y=770
BLUE_X=729  BLUE_Y=770
PAD_X=1259  PAD_Y=554

# --- Start PlayMode ---

echo "=== SimulateMouse Demo ==="
uloop control-play-mode --action Play
sleep 2

# --- Phase 1: Button clicks (4x alternating) ---

echo "[1/5] Button clicks"
uloop simulate-mouse-ui --action Click --x $CB1_X --y $CB1_Y && sleep 0.3
uloop simulate-mouse-ui --action Click --x $CB2_X --y $CB2_Y && sleep 0.3
uloop simulate-mouse-ui --action Click --x $CB1_X --y $CB1_Y && sleep 0.3
uloop simulate-mouse-ui --action Click --x $CB2_X --y $CB2_Y && sleep 0.3

# --- Phase 1.5: LongPress (3 seconds) ---

echo "[2/5] LongPress"
uloop simulate-mouse-ui --action LongPress --x $LP_X --y $LP_Y --duration 3.0 && sleep 0.3

# --- Phase 2: One-shot drag RedBox to DropZone top ---

echo "[3/5] Drag RedBox"
uloop simulate-mouse-ui --action Drag --from-x $RED_X --from-y $RED_Y --x $RED_X --y $((DZ_Y - 80)) --drag-speed 700 && sleep 0.3

# --- Phase 3: Split drag GreenBox through waypoints ---

echo "[4/5] Split drag GreenBox"
uloop simulate-mouse-ui --action DragStart --x $GREEN_X --y $GREEN_Y && sleep 0.3
uloop simulate-mouse-ui --action DragMove --x $((DZ_X + 150)) --y $((GREEN_Y - 50)) --drag-speed 400 && sleep 0.3
uloop simulate-mouse-ui --action DragMove --x $((DZ_X - 150)) --y $((DZ_Y + 50)) --drag-speed 400 && sleep 0.3
uloop simulate-mouse-ui --action DragMove --x $DZ_X --y $((DZ_Y - 80)) --drag-speed 400 && sleep 0.3
uloop simulate-mouse-ui --action DragEnd --x $DZ_X --y $DZ_Y --drag-speed 400 && sleep 0.3

# --- Phase 4: One-shot drag BlueBox to DropZone bottom ---

echo "[4.5/5] Drag BlueBox"
uloop simulate-mouse-ui --action Drag --from-x $BLUE_X --from-y $BLUE_Y --x $BLUE_X --y $((DZ_Y + 80)) --drag-speed 700 && sleep 0.3

# --- Phase 5: Virtual Pad (8 directions) ---

echo "[5/5] Virtual Pad"
uloop simulate-mouse-ui --action DragStart --x $PAD_X --y $PAD_Y && sleep 0.3
uloop simulate-mouse-ui --action DragMove --x $((PAD_X + 60)) --y $((PAD_Y - 60)) --drag-speed 300 && sleep 0.4
uloop simulate-mouse-ui --action DragMove --x $((PAD_X - 70)) --y $((PAD_Y + 50)) --drag-speed 300 && sleep 0.4
uloop simulate-mouse-ui --action DragMove --x $PAD_X --y $((PAD_Y - 75)) --drag-speed 300 && sleep 0.4
uloop simulate-mouse-ui --action DragMove --x $((PAD_X + 80)) --y $PAD_Y --drag-speed 300 && sleep 0.4
uloop simulate-mouse-ui --action DragMove --x $((PAD_X + 50)) --y $((PAD_Y + 60)) --drag-speed 300 && sleep 0.4
uloop simulate-mouse-ui --action DragMove --x $((PAD_X - 80)) --y $PAD_Y --drag-speed 300 && sleep 0.4
uloop simulate-mouse-ui --action DragMove --x $((PAD_X - 55)) --y $((PAD_Y - 65)) --drag-speed 300 && sleep 0.4
uloop simulate-mouse-ui --action DragMove --x $PAD_X --y $((PAD_Y + 75)) --drag-speed 300 && sleep 0.4
uloop simulate-mouse-ui --action DragEnd --x $PAD_X --y $PAD_Y --drag-speed 300

echo "=== SimulateMouse Demo Complete! ==="
