---
name: simulate-keyboard-demo
description: "Run the SimulateKeyboard demo scenario on SimulateKeyboardDemoScene. Presses individual keys, holds multiple keys simultaneously, and releases them to exercise the keyboard visual feedback. Use when the user asks to run the simulate-keyboard demo, test keyboard simulation, or exercise the keyboard demo scene."
context: fork
---

# Task

Run the SimulateKeyboard demo scenario: $ARGUMENTS

## What

Automate the SimulateKeyboardDemoScene by pressing individual keys (Press), holding keys down (KeyDown), holding multiple keys simultaneously, and releasing them (KeyUp). Exercises all three keyboard actions across various key types: letters, function keys, modifiers, arrow keys, and space.

## When

Use when you need to:
1. Run the simulate-keyboard demo to verify Press, KeyDown, and KeyUp functionality
2. Test keyboard simulation on the demo scene after code changes
3. Exercise the demo scene end-to-end (single press + simultaneous hold + release)

## How

### Prerequisites

- Unity must be running with **SimulateKeyboardDemoScene** loaded
- **PlayMode** must be active
- **Input System package** must be installed

If PlayMode is not active, start it with `uloop control-play-mode --action Play` and wait a moment for the scene to initialize.

### Step 1: Run keyboard demo sequence — chain all in one Bash call

**IMPORTANT**: Chain all commands with `&&` in a **single Bash tool call** to eliminate round-trip latency between operations.

**Phase 1 — Single key presses**: Press individual keys one at a time to verify each lights up and fades.

**Phase 2 — Rapid fire**: Press Enter 5 times in quick succession to test rapid repeated input.

**Phase 3 — WASD movement simulation**: Hold W (forward), then add LeftShift (sprint), release W, release Shift.

**Phase 4 — Function keys**: Press F1, F2, F3 in sequence.

**Phase 5 — Arrow keys**: Hold all four arrow keys simultaneously, then release them.

**Phase 6 — Combo keys**: Hold LeftCtrl + LeftAlt + Space simultaneously, then release all.

```bash
# Phase 1: Single key presses (--duration 0.5 so each key lights up visibly)
uloop simulate-keyboard --action Press --key W --duration 0.5 && sleep 0.2 && \
uloop simulate-keyboard --action Press --key A --duration 0.5 && sleep 0.2 && \
uloop simulate-keyboard --action Press --key S --duration 0.5 && sleep 0.2 && \
uloop simulate-keyboard --action Press --key D --duration 0.5 && sleep 0.2 && \
uloop simulate-keyboard --action Press --key Space --duration 0.5 && sleep 0.2 && \
uloop simulate-keyboard --action Press --key Enter --duration 0.5 && sleep 0.2 && \
uloop simulate-keyboard --action Press --key Escape --duration 0.5 && sleep 0.2 && \
# Phase 2: Rapid fire Enter x5 (shorter duration for machine-gun feel)
uloop simulate-keyboard --action Press --key Enter --duration 0.2 && sleep 0.1 && \
uloop simulate-keyboard --action Press --key Enter --duration 0.2 && sleep 0.1 && \
uloop simulate-keyboard --action Press --key Enter --duration 0.2 && sleep 0.1 && \
uloop simulate-keyboard --action Press --key Enter --duration 0.2 && sleep 0.1 && \
uloop simulate-keyboard --action Press --key Enter --duration 0.2 && sleep 0.3 && \
# Phase 3: WASD + Shift sprint
uloop simulate-keyboard --action KeyDown --key W && sleep 0.5 && \
uloop simulate-keyboard --action KeyDown --key LeftShift && sleep 1.0 && \
uloop simulate-keyboard --action KeyUp --key W && sleep 0.3 && \
uloop simulate-keyboard --action KeyUp --key LeftShift && sleep 0.3 && \
# Phase 4: Function keys
uloop simulate-keyboard --action Press --key F1 --duration 0.5 && sleep 0.2 && \
uloop simulate-keyboard --action Press --key F2 --duration 0.5 && sleep 0.2 && \
uloop simulate-keyboard --action Press --key F3 --duration 0.5 && sleep 0.3 && \
# Phase 5: Arrow keys simultaneous hold
uloop simulate-keyboard --action KeyDown --key UpArrow && sleep 0.3 && \
uloop simulate-keyboard --action KeyDown --key LeftArrow && sleep 0.3 && \
uloop simulate-keyboard --action KeyDown --key DownArrow && sleep 0.3 && \
uloop simulate-keyboard --action KeyDown --key RightArrow && sleep 1.0 && \
uloop simulate-keyboard --action KeyUp --key UpArrow && sleep 0.2 && \
uloop simulate-keyboard --action KeyUp --key LeftArrow && sleep 0.2 && \
uloop simulate-keyboard --action KeyUp --key DownArrow && sleep 0.2 && \
uloop simulate-keyboard --action KeyUp --key RightArrow && sleep 0.3 && \
# Phase 6: Modifier combo
uloop simulate-keyboard --action KeyDown --key LeftCtrl && sleep 0.3 && \
uloop simulate-keyboard --action KeyDown --key LeftAlt && sleep 0.3 && \
uloop simulate-keyboard --action KeyDown --key Space && sleep 1.0 && \
uloop simulate-keyboard --action KeyUp --key Space && sleep 0.2 && \
uloop simulate-keyboard --action KeyUp --key LeftAlt && sleep 0.2 && \
uloop simulate-keyboard --action KeyUp --key LeftCtrl
```

### Step 2: Report results

Report which phases completed successfully and any errors returned by the commands.
