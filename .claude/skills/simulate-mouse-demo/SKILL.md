---
name: simulate-mouse-demo
description: "Run the SimulateMouse demo scenario on SimulateMouseDemoScene. Clicks a button 4 times, then drags 3 colored boxes to a drop zone. Use when the user asks to run the simulate-mouse demo, test mouse simulation, or exercise the demo scene."
context: fork
---

# Task

Run the SimulateMouse demo scenario: $ARGUMENTS

## What

Automate the SimulateMouse demo scene by clicking buttons and dragging colored boxes to a drop zone, exercising both click and drag capabilities of the simulate-mouse tool.

## When

Use when you need to:
1. Run the simulate-mouse demo to verify click and drag functionality
2. Test mouse simulation on the demo scene after code changes
3. Exercise the demo scene end-to-end (buttons + drag-and-drop)

## How

### Prerequisites

- Unity must be running with **SimulateMouseDemoScene** loaded
- **PlayMode** must be active

If PlayMode is not active, start it with `uloop control-play-mode --action Play` and wait a moment for the scene to initialize.

### Step 1: Discover UI element coordinates

Take an annotated screenshot to get exact coordinates for each interactive element:

```bash
uloop screenshot --capture-mode rendering --annotate-elements true
```

From the `AnnotatedElements` array in the response, extract `SimX` and `SimY` for:
- **ClickButton1** — red button
- **ClickButton2** — blue button
- **DropZone** — the drag target area
- **RedBox** — red draggable box
- **GreenBox** — green draggable box
- **BlueBox** — blue draggable box

### Step 2: Click buttons and drag boxes — chain all in one Bash call

**IMPORTANT**: Chain all commands with `&&` in a **single Bash tool call** to eliminate round-trip latency between operations. The simulate-mouse tool uses a single-pointer model, so commands must run sequentially (not in parallel), but chaining avoids AI round-trip delays.

Alternate ClickButton1 and ClickButton2 4 times, then drag each box to the DropZone at `--drag-speed 1000`. Offset drop X by -50/0/+50 so the boxes converge slightly toward center:

```bash
uloop simulate-mouse --action Click --x <ClickButton1.SimX> --y <ClickButton1.SimY> && sleep 0.3 && \
uloop simulate-mouse --action Click --x <ClickButton2.SimX> --y <ClickButton2.SimY> && sleep 0.3 && \
uloop simulate-mouse --action Click --x <ClickButton1.SimX> --y <ClickButton1.SimY> && sleep 0.3 && \
uloop simulate-mouse --action Click --x <ClickButton2.SimX> --y <ClickButton2.SimY> && sleep 0.3 && \
uloop simulate-mouse --action Drag --x <RedBox.SimX> --y <RedBox.SimY> --end-x <DropZone.SimX - 50> --end-y <DropZone.SimY> --drag-speed 700 && sleep 0.3 && \
uloop simulate-mouse --action Drag --x <GreenBox.SimX> --y <GreenBox.SimY> --end-x <DropZone.SimX> --end-y <DropZone.SimY> --drag-speed 700 && sleep 0.3 && \
uloop simulate-mouse --action Drag --x <BlueBox.SimX> --y <BlueBox.SimY> --end-x <DropZone.SimX + 50> --end-y <DropZone.SimY> --drag-speed 700
```
