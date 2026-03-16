#!/bin/sh
# Automated UnityChan demo - plays ALL available motions
# Stays near center using back-and-forth patterns

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

# CrossFade to a named animator state, wait for it to finish
play_motion() {
    name="$1"
    wait_time="$2"
    echo "  -> $name (${wait_time}s)"
    uloop execute-dynamic-code --code "
        var go = UnityEngine.GameObject.Find(\"unitychan\");
        var animator = go.GetComponent<UnityEngine.Animator>();
        animator.CrossFade(\"$name\", 0.25f);
    "
    sleep "$wait_time"
}

echo "=== UnityChan Full Motion Demo ==="

echo "[Setup] Starting PlayMode..."
uloop control-play-mode --action Play
sleep 2
uloop focus-window
sleep 0.5

# ===== Part 1: Movement motions (WASD controlled) =====
echo ""
echo "[Part 1] Movement motions"

echo "  Idle (WAIT00)"
sleep 2.0

echo "  Walk forward (Shift+W)"
hold_for 2.0 LeftShift W
sleep 0.3
hold_for 2.0 LeftShift S
sleep 0.5

echo "  Run forward (W)"
hold_for 1.5 W
sleep 0.3
hold_for 1.5 S
sleep 0.5

echo "  Walk backward (Shift+S)"
hold_for 2.0 LeftShift S
sleep 0.3
hold_for 2.0 LeftShift W
sleep 0.5

echo "  Jump from idle (Space)"
press Space 0.3
sleep 2.5

echo "  Running jump (W + Space)"
hold_for 1.0 W
press Space 0.3
sleep 2.0
hold_for 1.0 S
sleep 0.5

# ===== Part 2: Idle variants =====
echo ""
echo "[Part 2] Idle variants"

play_motion WAIT00 3.0
play_motion WAIT01 4.0
play_motion WAIT02 4.0
play_motion WAIT03 3.0
play_motion WAIT04 3.0

# ===== Part 3: Walk variants =====
echo ""
echo "[Part 3] Walk variants"

play_motion WALK00_F 2.0
play_motion WALK00_B 2.5
play_motion WALK00_L 2.0
play_motion WALK00_R 2.0

# ===== Part 4: Run variants =====
echo ""
echo "[Part 4] Run variants"

play_motion RUN00_F 1.5
play_motion RUN00_L 1.5
play_motion RUN00_R 1.5

# ===== Part 5: Jump variants =====
echo ""
echo "[Part 5] Jump variants"

play_motion JUMP00 2.5
play_motion JUMP00B 2.5
play_motion JUMP01 2.5
play_motion JUMP01B 2.5

# ===== Part 6: Action motions =====
echo ""
echo "[Part 6] Action motions"

play_motion HANDUP00_R 1.5
play_motion SLIDE00 2.0
play_motion UMATOBI00 2.0
play_motion REFLESH00 4.0

# ===== Part 7: Reaction motions =====
echo ""
echo "[Part 7] Reaction motions"

play_motion DAMAGED00 1.8
play_motion DAMAGED01 4.0
play_motion LOSE00 4.0

# ===== Part 8: Finale =====
echo ""
echo "[Part 8] Finale"

play_motion WIN00 4.5

echo ""
echo "=== All 25 motions demonstrated! ==="
