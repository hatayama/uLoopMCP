# Checklist — uloop-simulate-mouse-ui

## Scenario A
- [critical] Issues `uloop simulate-mouse-ui --action Click --x 400 --y 300`
- [critical] Includes verification screenshot step (`uloop screenshot --capture-mode rendering --annotate-elements`)
- [critical] Reports response using ONLY documented fields (`Success`, `Message`, `Action`, `HitGameObjectName`, `PositionX`, `PositionY`, `EndPositionX`, `EndPositionY`)
- Reads `HitGameObjectName` to confirm the button was hit, NOT inventing a `RaycastHit` array

## Scenario B
- [critical] Uses `--action Drag --from-x 200 --from-y 400 --x 600 --y 400 --drag-speed 200` (low speed for visibility per body)
- Documents that `--drag-speed 2000` is the default fast speed

## Scenario H (hold-out)
- [critical] Uses split-drag sequence: `DragStart` → optional screenshot → `DragMove` → optional screenshot → `DragEnd`
- [critical] Recognizes that `DragEnd` MUST be called to release drag state (per the documented rule)
- Does not attempt to use `--button Right` for any drag action (drag actions are Left only)
