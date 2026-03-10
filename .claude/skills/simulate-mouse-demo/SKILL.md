---
name: simulate-mouse-demo
description: "Run the SimulateMouse demo scenario on SimulateMouseDemoScene. Clicks a button 10 times, then drags 3 colored boxes to a drop zone. Use when the user asks to run the simulate-mouse demo, test mouse simulation, or exercise the demo scene."
context: fork
---

# Task

Run the SimulateMouse demo scenario: $ARGUMENTS

## Prerequisites

- Unity must be running with **SimulateMouseDemoScene** loaded
- **PlayMode** must be active

If PlayMode is not active, start it with `uloop control-play-mode --action Play` and wait a moment for the scene to initialize.

## Scenario

### Step 1: Discover UI element coordinates

Take an annotated screenshot to get exact coordinates for each interactive element:

```bash
uloop screenshot --capture-mode rendering --annotate-elements
```

From the `AnnotatedElements` array in the response, extract `SimX` and `SimY` for:
- **ClickButton1** — the button to click
- **DropZone** — the drag target area
- **RedBox** — red draggable box
- **GreenBox** — green draggable box
- **BlueBox** — blue draggable box

### Step 2: Click the button 10 times

Click **ClickButton1** 10 times:

```bash
uloop simulate-mouse --action Click --x <ClickButton1.SimX> --y <ClickButton1.SimY>
```

### Step 3: Drag each colored box to the DropZone

Drag each box to the DropZone at `--drag-speed 500`:

```bash
uloop simulate-mouse --action Drag \
    --x <RedBox.SimX> --y <RedBox.SimY> \
    --end-x <DropZone.SimX> --end-y <DropZone.SimY> \
    --drag-speed 500

uloop simulate-mouse --action Drag \
    --x <GreenBox.SimX> --y <GreenBox.SimY> \
    --end-x <DropZone.SimX> --end-y <DropZone.SimY> \
    --drag-speed 500

uloop simulate-mouse --action Drag \
    --x <BlueBox.SimX> --y <BlueBox.SimY> \
    --end-x <DropZone.SimX> --end-y <DropZone.SimY> \
    --drag-speed 500
```

### Step 4: Report

Report the results from each command's response (hit element names, success/failure).
