#!/bin/sh
# Test script for Meta key and modifier key overlay display
# Verifies that platform-specific symbols render correctly:
#   macOS: LeftMeta/RightMeta → ⌘
#   Windows/Linux: LeftMeta/RightMeta → ⊞
# Also tests Windows-specific keys and all modifier combinations.

set -e

cleanup() {
    uloop simulate-keyboard --action KeyUp --key LeftMeta 2>/dev/null
    uloop simulate-keyboard --action KeyUp --key RightMeta 2>/dev/null
    uloop simulate-keyboard --action KeyUp --key LeftShift 2>/dev/null
    uloop simulate-keyboard --action KeyUp --key LeftCtrl 2>/dev/null
    uloop simulate-keyboard --action KeyUp --key LeftAlt 2>/dev/null
    uloop control-play-mode --action Stop 2>/dev/null
}
trap cleanup EXIT INT TERM

hold() {
    uloop simulate-keyboard --action KeyDown --key "$1"
}

release() {
    uloop simulate-keyboard --action KeyUp --key "$1"
}

press() {
    uloop simulate-keyboard --action Press --key "$1" --duration "${2:-0.5}"
}

screenshot() {
    uloop screenshot --capture-mode rendering
}

echo "=== Meta Key & Modifier Overlay Test ==="

uloop control-play-mode --action Play
sleep 2

# --- Single modifier keys ---

echo "[1/7] LeftMeta (macOS: ⌘, Win: ⊞)"
hold LeftMeta
sleep 1
screenshot
release LeftMeta
sleep 1

echo "[2/7] RightMeta (macOS: ⌘, Win: ⊞)"
hold RightMeta
sleep 1
screenshot
release RightMeta
sleep 1

echo "[3/7] LeftShift (⇧)"
hold LeftShift
sleep 1
screenshot
release LeftShift
sleep 1

echo "[4/7] LeftCtrl (Ctrl)"
hold LeftCtrl
sleep 1
screenshot
release LeftCtrl
sleep 1

echo "[5/7] LeftAlt (Alt)"
hold LeftAlt
sleep 1
screenshot
release LeftAlt
sleep 1

# --- Multi-modifier combos ---

echo "[6/7] Ctrl + Alt + Meta (triple modifier)"
hold LeftCtrl
hold LeftAlt
hold LeftMeta
sleep 1.5
screenshot
release LeftMeta
release LeftAlt
release LeftCtrl
sleep 1

# --- Special keys (Press) ---

echo "[7/7] Special keys: Tab(⇥) Esc Enter(⏎) Backspace(⌫) Space(␣) Arrows(↑↓←→)"
press Tab 0.3
sleep 1
press Escape 0.3
sleep 1
press Enter 0.3
sleep 1
press Backspace 0.3
sleep 1
press Space 0.3
sleep 1
press UpArrow 0.3
sleep 0.8
press DownArrow 0.3
sleep 0.8
press LeftArrow 0.3
sleep 0.8
press RightArrow 0.3
sleep 1.5

echo "=== Meta Key & Modifier Overlay Test Complete! ==="
echo "Check screenshots to verify platform-specific symbols."
