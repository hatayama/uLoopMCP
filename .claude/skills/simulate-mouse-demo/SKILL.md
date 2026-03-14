---
name: simulate-mouse-demo
description: "Run the SimulateMouse demo scenario on SimulateMouseDemoScene. Clicks buttons, drags boxes with one-shot Drag and split DragStart/DragMove/DragEnd. Use when the user asks to run the simulate-mouse demo, test mouse simulation, or exercise the demo scene."
context: fork
---

# Task

Run the SimulateMouse demo scenario: $ARGUMENTS

## What

Automate the SimulateMouse demo scene by clicking buttons, one-shot dragging boxes, and split-dragging a box through multiple waypoints to the drop zone. Exercises Click, Drag, DragStart, DragMove, and DragEnd.

## When

Use when you need to:
1. Run the simulate-mouse demo to verify click, drag, and split-drag functionality
2. Test mouse simulation on the demo scene after code changes
3. Exercise the demo scene end-to-end (buttons + one-shot drag + split drag with DragMove)

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

### Step 2: Click buttons, one-shot drag, and split drag — chain all in one Bash call

**IMPORTANT**: Chain all commands with `&&` in a **single Bash tool call** to eliminate round-trip latency between operations. The simulate-mouse tool uses a single-pointer model, so commands must run sequentially (not in parallel), but chaining avoids AI round-trip delays.

**Phase 1 — Button clicks**: Alternate ClickButton1 and ClickButton2 four times.

**Phase 2 — One-shot Drag**: Drag RedBox directly to DropZone (offset X -50).

**Phase 3 — Split drag with DragMove**: Use DragStart on GreenBox, then DragMove through 3 waypoints that zigzag across the screen, and finally DragEnd at the DropZone. Use `--drag-speed 400` for DragMove/DragEnd so the movement is slow enough to observe the cursor overlay and trail line.

Waypoint design (relative to the midpoint between GreenBox and DropZone):
- Waypoint 1: far right, slightly above — tests horizontal sweep
- Waypoint 2: far left, below — tests direction reversal
- Waypoint 3: above DropZone — tests vertical approach
- DragEnd: at DropZone center

**Phase 4 — One-shot Drag**: Drag BlueBox directly to DropZone (offset X +50).

```bash
uloop simulate-mouse --action Click --x <ClickButton1.SimX> --y <ClickButton1.SimY> && sleep 0.3 && \
uloop simulate-mouse --action Click --x <ClickButton2.SimX> --y <ClickButton2.SimY> && sleep 0.3 && \
uloop simulate-mouse --action Click --x <ClickButton1.SimX> --y <ClickButton1.SimY> && sleep 0.3 && \
uloop simulate-mouse --action Click --x <ClickButton2.SimX> --y <ClickButton2.SimY> && sleep 0.3 && \
uloop simulate-mouse --action Drag --from-x <RedBox.SimX> --from-y <RedBox.SimY> --x <DropZone.SimX - 50> --y <DropZone.SimY> --drag-speed 700 && sleep 0.3 && \
uloop simulate-mouse --action DragStart --x <GreenBox.SimX> --y <GreenBox.SimY> && sleep 0.3 && \
uloop simulate-mouse --action DragMove --x <DropZone.SimX + 150> --y <GreenBox.SimY - 50> --drag-speed 400 && sleep 0.3 && \
uloop simulate-mouse --action DragMove --x <DropZone.SimX - 150> --y <DropZone.SimY + 50> --drag-speed 400 && sleep 0.3 && \
uloop simulate-mouse --action DragMove --x <DropZone.SimX> --y <DropZone.SimY - 80> --drag-speed 400 && sleep 0.3 && \
uloop simulate-mouse --action DragEnd --x <DropZone.SimX> --y <DropZone.SimY> --drag-speed 400 && sleep 0.3 && \
uloop simulate-mouse --action Drag --from-x <BlueBox.SimX> --from-y <BlueBox.SimY> --x <DropZone.SimX + 50> --y <DropZone.SimY> --drag-speed 700
```

### Step 3: Verify results

Take a final screenshot to confirm all boxes reached the drop zone:

```bash
uloop screenshot --capture-mode rendering
```
