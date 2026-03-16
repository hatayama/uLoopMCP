#!/bin/sh
# SimulateKeyboard demo - exercises all key actions on SimulateKeyboardDemoScene

set -e

# Phase 1: Single key presses
uloop simulate-keyboard --action Press --key W --duration 0.5 && sleep 0.2
uloop simulate-keyboard --action Press --key A --duration 0.5 && sleep 0.2
uloop simulate-keyboard --action Press --key S --duration 0.5 && sleep 0.2
uloop simulate-keyboard --action Press --key D --duration 0.5 && sleep 0.2
uloop simulate-keyboard --action Press --key Space --duration 0.5 && sleep 0.2
uloop simulate-keyboard --action Press --key Enter --duration 0.5 && sleep 0.2
uloop simulate-keyboard --action Press --key Escape --duration 0.5 && sleep 0.2

# Phase 2: Rapid fire Enter x5
uloop simulate-keyboard --action Press --key Enter --duration 0.2 && sleep 0.1
uloop simulate-keyboard --action Press --key Enter --duration 0.2 && sleep 0.1
uloop simulate-keyboard --action Press --key Enter --duration 0.2 && sleep 0.1
uloop simulate-keyboard --action Press --key Enter --duration 0.2 && sleep 0.1
uloop simulate-keyboard --action Press --key Enter --duration 0.2 && sleep 0.3

# Phase 3: WASD + Shift sprint
uloop simulate-keyboard --action KeyDown --key W && sleep 0.5
uloop simulate-keyboard --action KeyDown --key LeftShift && sleep 1.0
uloop simulate-keyboard --action KeyUp --key W && sleep 0.3
uloop simulate-keyboard --action KeyUp --key LeftShift && sleep 0.3

# Phase 4: Function keys
uloop simulate-keyboard --action Press --key F1 --duration 0.5 && sleep 0.2
uloop simulate-keyboard --action Press --key F2 --duration 0.5 && sleep 0.2
uloop simulate-keyboard --action Press --key F3 --duration 0.5 && sleep 0.3

# Phase 5: Arrow keys simultaneous hold
uloop simulate-keyboard --action KeyDown --key UpArrow && sleep 0.3
uloop simulate-keyboard --action KeyDown --key LeftArrow && sleep 0.3
uloop simulate-keyboard --action KeyDown --key DownArrow && sleep 0.3
uloop simulate-keyboard --action KeyDown --key RightArrow && sleep 1.0
uloop simulate-keyboard --action KeyUp --key UpArrow && sleep 0.2
uloop simulate-keyboard --action KeyUp --key LeftArrow && sleep 0.2
uloop simulate-keyboard --action KeyUp --key DownArrow && sleep 0.2
uloop simulate-keyboard --action KeyUp --key RightArrow && sleep 0.3

# Phase 6: Modifier combo
uloop simulate-keyboard --action KeyDown --key LeftCtrl && sleep 0.3
uloop simulate-keyboard --action KeyDown --key LeftAlt && sleep 0.3
uloop simulate-keyboard --action KeyDown --key Space && sleep 1.0
uloop simulate-keyboard --action KeyUp --key Space && sleep 0.2
uloop simulate-keyboard --action KeyUp --key LeftAlt && sleep 0.2
uloop simulate-keyboard --action KeyUp --key LeftCtrl

echo "Demo complete."
