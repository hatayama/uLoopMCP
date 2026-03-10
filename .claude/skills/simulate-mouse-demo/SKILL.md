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
uloop screenshot --capture-mode rendering --annotate-elements true
```

From the `AnnotatedElements` array in the response, extract `SimX` and `SimY` for:
- **ClickButton1** — red button
- **ClickButton2** — blue button
- **DropZone** — the drag target area
- **RedBox** — red draggable box
- **GreenBox** — green draggable box
- **BlueBox** — blue draggable box

### Step 2: Alternate-click both buttons 10 times total

Click **ClickButton1** and **ClickButton2** alternately (5 times each, 10 total). Fire all clicks as fast as possible — launch each click command immediately without waiting for output or adding delays:

```bash
uloop simulate-mouse --action Click --x <ClickButton1.SimX> --y <ClickButton1.SimY>
uloop simulate-mouse --action Click --x <ClickButton2.SimX> --y <ClickButton2.SimY>
uloop simulate-mouse --action Click --x <ClickButton1.SimX> --y <ClickButton1.SimY>
uloop simulate-mouse --action Click --x <ClickButton2.SimX> --y <ClickButton2.SimY>
...
```

### Step 3: Drag each colored box to the DropZone

Drag each box to the DropZone at `--drag-speed 2000` (fast). Fire each drag immediately without waiting. Offset the drop position by -100/0/+100 pixels in X so the boxes don't stack on top of each other:

```bash
uloop simulate-mouse --action Drag \
    --x <RedBox.SimX> --y <RedBox.SimY> \
    --end-x <DropZone.SimX - 100> --end-y <DropZone.SimY> \
    --drag-speed 2000

uloop simulate-mouse --action Drag \
    --x <GreenBox.SimX> --y <GreenBox.SimY> \
    --end-x <DropZone.SimX> --end-y <DropZone.SimY> \
    --drag-speed 2000

uloop simulate-mouse --action Drag \
    --x <BlueBox.SimX> --y <BlueBox.SimY> \
    --end-x <DropZone.SimX + 100> --end-y <DropZone.SimY> \
    --drag-speed 2000
```

### Step 4: Report

Report the results from each command's response (hit element names, success/failure).
