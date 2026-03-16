#!/bin/sh
# Automated UnityChan demo choreography
# Showcases all available motions: idle, walk, run, jump, 8-directional movement

set -e

cleanup() {
    # Release any stuck keys and stop PlayMode on interruption
    uloop control-play-mode --action Stop 2>/dev/null
}
trap cleanup EXIT INT TERM

press() {
    uloop simulate-keyboard --action Press --key "$1" --duration "${2:-0.5}"
}

# Hold keys for a duration, then auto-release all of them
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

# Hold keys without releasing (for combining with press mid-hold)
hold() {
    uloop simulate-keyboard --action KeyDown --key "$1"
}

release() {
    uloop simulate-keyboard --action KeyUp --key "$1"
}

echo "=== UnityChan Demo Choreography ==="
echo ""

# --- Ensure PlayMode and focus ---
echo "[Setup] Starting PlayMode and focusing window..."
uloop control-play-mode --action Play
sleep 2
uloop focus-window
sleep 0.5

# --- Act 1: Idle Jump ---
echo "[Act 1] Idle jump - showing off from standstill"
press Space 0.3
sleep 2.0

# --- Act 2: Gentle walk forward ---
echo "[Act 2] Elegant walk forward"
hold_for 3.0 LeftShift W
sleep 0.5

# --- Act 3: Sprint forward ---
echo "[Act 3] Break into a sprint!"
hold W
sleep 2.0

# --- Act 4: Running jump ---
echo "[Act 4] Running jump!"
press Space 0.3
sleep 2.0
release W
sleep 0.5

# --- Act 5: Strafe right while walking ---
echo "[Act 5] Walk to the right"
hold_for 2.5 LeftShift D
sleep 0.5

# --- Act 6: Sprint left ---
echo "[Act 6] Sprint to the left"
hold A
sleep 2.0

# --- Act 7: Jump while sprinting left ---
echo "[Act 7] Jump while sprinting left!"
press Space 0.3
sleep 2.0
release A
sleep 0.5

# --- Act 8: Diagonal run (forward-right) ---
echo "[Act 8] Diagonal sprint forward-right"
hold W
hold D
sleep 2.5

# --- Act 9: Jump mid-diagonal sprint ---
echo "[Act 9] Jump during diagonal sprint!"
press Space 0.3
sleep 2.0
release D
release W
sleep 0.5

# --- Act 10: Walk backward ---
echo "[Act 10] Walk backward"
hold_for 2.0 LeftShift S
sleep 0.5

# --- Act 11: Sprint backward ---
echo "[Act 11] Sprint backward"
hold_for 1.5 S
sleep 0.5

# --- Act 12: Quick direction changes (zig-zag) ---
echo "[Act 12] Zig-zag sprint!"
hold W
hold D
sleep 0.8
release D
hold A
sleep 0.8
release A
hold D
sleep 0.8
release D
release W
sleep 0.5

# --- Act 13: Walk in a square ---
echo "[Act 13] Walk in a square"
hold LeftShift
hold_for 1.5 W
hold_for 1.5 D
hold_for 1.5 S
hold_for 1.5 A
release LeftShift
sleep 0.5

# --- Act 14: Arrow keys - run forward with arrow ---
echo "[Act 14] Arrow key sprint forward"
hold UpArrow
sleep 2.0

# --- Act 15: Arrow key jump ---
echo "[Act 15] Arrow key running jump!"
press Space 0.3
sleep 2.0
release UpArrow
sleep 0.5

# --- Act 16: Diagonal arrow keys ---
echo "[Act 16] Diagonal arrow keys"
hold_for 2.0 UpArrow RightArrow
sleep 0.5

# --- Grand Finale: Sprint forward, jump, land, idle jump ---
echo "[Finale] Grand finale!"
hold W
sleep 1.5
echo "  Jump 1!"
press Space 0.3
sleep 2.0
echo "  Jump 2!"
press Space 0.3
sleep 2.0
release W
sleep 1.0
echo "  Final idle jump!"
press Space 0.3
sleep 2.5

# --- Cleanup handled by trap ---
echo ""
echo "=== Demo Complete! ==="
