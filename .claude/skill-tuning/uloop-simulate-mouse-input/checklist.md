# Checklist — uloop-simulate-mouse-input

## Scenario A
- [critical] Issues `uloop simulate-mouse-input --action Click --x 400 --y 300 --button Right`
- [critical] Includes a verification screenshot step (`uloop screenshot --capture-mode rendering`)
- [critical] Reports response using ONLY documented fields (`Success`, `Message`, `Action`, `Button`, `PositionX`, `PositionY`)

## Scenario B
- [critical] Uses `--action SmoothDelta --delta-x 300 --delta-y 0 --duration 0.5` (NOT MoveDelta which is one-shot)
- Notes that `Button`/`PositionX`/`PositionY` will be null in the response since SmoothDelta doesn't use them

## Scenario H (hold-out)
- [critical] Recognizes the New Input System prerequisite and recommends `uloop execute-dynamic-code` as the project-specific workaround per skill body, NOT changing project settings
- Does not pretend to invoke a non-existent legacy-input mode of this tool
