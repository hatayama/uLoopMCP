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

### Step 2: Click buttons and drag boxes — fire everything at once

Launch **all 13 commands below as background tasks in a single message** — do not wait for any command to finish before launching the next. The goal is maximum parallelism with zero gaps between clicks and drags.

**Clicks** — alternate ClickButton1 and ClickButton2, 10 times total:

```bash
uloop simulate-mouse --action Click --x <ClickButton1.SimX> --y <ClickButton1.SimY>
uloop simulate-mouse --action Click --x <ClickButton2.SimX> --y <ClickButton2.SimY>
uloop simulate-mouse --action Click --x <ClickButton1.SimX> --y <ClickButton1.SimY>
uloop simulate-mouse --action Click --x <ClickButton2.SimX> --y <ClickButton2.SimY>
uloop simulate-mouse --action Click --x <ClickButton1.SimX> --y <ClickButton1.SimY>
uloop simulate-mouse --action Click --x <ClickButton2.SimX> --y <ClickButton2.SimY>
uloop simulate-mouse --action Click --x <ClickButton1.SimX> --y <ClickButton1.SimY>
uloop simulate-mouse --action Click --x <ClickButton2.SimX> --y <ClickButton2.SimY>
uloop simulate-mouse --action Click --x <ClickButton1.SimX> --y <ClickButton1.SimY>
uloop simulate-mouse --action Click --x <ClickButton2.SimX> --y <ClickButton2.SimY>
```

**Drags** — drag each box to the DropZone at `--drag-speed 1000`. Offset drop X by -50/0/+50 so the boxes converge slightly toward center:

```bash
uloop simulate-mouse --action Drag \
    --x <RedBox.SimX> --y <RedBox.SimY> \
    --end-x <DropZone.SimX - 50> --end-y <DropZone.SimY> \
    --drag-speed 1000

uloop simulate-mouse --action Drag \
    --x <GreenBox.SimX> --y <GreenBox.SimY> \
    --end-x <DropZone.SimX> --end-y <DropZone.SimY> \
    --drag-speed 1000

uloop simulate-mouse --action Drag \
    --x <BlueBox.SimX> --y <BlueBox.SimY> \
    --end-x <DropZone.SimX + 50> --end-y <DropZone.SimY> \
    --drag-speed 1000
```

