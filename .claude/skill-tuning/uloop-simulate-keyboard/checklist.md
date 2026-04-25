# Checklist — uloop-simulate-keyboard

## Scenario A (Sprint Shift+W for 3s)
- [critical] Uses `KeyDown` for both keys (NOT Press), waits via `sleep 3` (the documented mechanism for multi-key holds), then `KeyUp` for both
- [critical] Reports response using ONLY documented fields (`Success`, `Message`, `Action`, `KeyName`)
- [critical] Includes screenshot step from workflow

## Scenario B (Tap Space)
- [critical] Uses `--action Press --key Space` (not KeyDown/KeyUp pair)
- Includes screenshot from workflow

## Scenario H (KeyUp on unheld key)
- [critical] Predicts `Success: false` (per documented "KeyUp fails if the key is not currently held"), surfaces `Message`
- Does not invent recovery logic
