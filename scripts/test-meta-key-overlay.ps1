# Test script for Meta key and modifier key overlay display
# Verifies that platform-specific symbols render correctly:
#   macOS: LeftMeta/RightMeta -> Command
#   Windows/Linux: LeftMeta/RightMeta -> Win
# Also tests Windows-specific keys and all modifier combinations.

$ErrorActionPreference = "Stop"

function Cleanup {
    uloop simulate-keyboard --action KeyUp --key LeftMeta 2>$null
    uloop simulate-keyboard --action KeyUp --key RightMeta 2>$null
    uloop simulate-keyboard --action KeyUp --key LeftShift 2>$null
    uloop simulate-keyboard --action KeyUp --key LeftCtrl 2>$null
    uloop simulate-keyboard --action KeyUp --key LeftAlt 2>$null
    uloop control-play-mode --action Stop 2>$null
}

function Hold {
    param([string]$Key)
    uloop simulate-keyboard --action KeyDown --key $Key
}

function Release {
    param([string]$Key)
    uloop simulate-keyboard --action KeyUp --key $Key
}

function Press {
    param(
        [string]$Key,
        [float]$Duration = 0.5
    )
    uloop simulate-keyboard --action Press --key $Key --duration $Duration
}

function Screenshot {
    uloop screenshot --capture-mode rendering
}

try {
    Write-Host "=== Meta Key & Modifier Overlay Test ==="

    uloop control-play-mode --action Play
    Start-Sleep -Seconds 2

    # --- Single modifier keys ---

    Write-Host "[1/7] LeftMeta"
    Hold -Key LeftMeta
    Start-Sleep -Seconds 1
    Screenshot
    Release -Key LeftMeta
    Start-Sleep -Seconds 1

    Write-Host "[2/7] RightMeta"
    Hold -Key RightMeta
    Start-Sleep -Seconds 1
    Screenshot
    Release -Key RightMeta
    Start-Sleep -Seconds 1

    Write-Host "[3/7] LeftShift"
    Hold -Key LeftShift
    Start-Sleep -Seconds 1
    Screenshot
    Release -Key LeftShift
    Start-Sleep -Seconds 1

    Write-Host "[4/7] LeftCtrl"
    Hold -Key LeftCtrl
    Start-Sleep -Seconds 1
    Screenshot
    Release -Key LeftCtrl
    Start-Sleep -Seconds 1

    Write-Host "[5/7] LeftAlt"
    Hold -Key LeftAlt
    Start-Sleep -Seconds 1
    Screenshot
    Release -Key LeftAlt
    Start-Sleep -Seconds 1

    # --- Multi-modifier combos ---

    Write-Host "[6/7] Ctrl + Alt + Meta (triple modifier)"
    Hold -Key LeftCtrl
    Hold -Key LeftAlt
    Hold -Key LeftMeta
    Start-Sleep -Seconds 1.5
    Screenshot
    Release -Key LeftMeta
    Release -Key LeftAlt
    Release -Key LeftCtrl
    Start-Sleep -Seconds 1

    # --- Special keys (Press) ---

    Write-Host "[7/7] Special keys: Tab Esc Enter Backspace Space Arrows"
    Press -Key Tab -Duration 0.3
    Start-Sleep -Seconds 1
    Press -Key Escape -Duration 0.3
    Start-Sleep -Seconds 1
    Press -Key Enter -Duration 0.3
    Start-Sleep -Seconds 1
    Press -Key Backspace -Duration 0.3
    Start-Sleep -Seconds 1
    Press -Key Space -Duration 0.3
    Start-Sleep -Seconds 1
    Press -Key UpArrow -Duration 0.3
    Start-Sleep -Seconds 0.8
    Press -Key DownArrow -Duration 0.3
    Start-Sleep -Seconds 0.8
    Press -Key LeftArrow -Duration 0.3
    Start-Sleep -Seconds 0.8
    Press -Key RightArrow -Duration 0.3
    Start-Sleep -Seconds 1.5

    Write-Host "=== Meta Key & Modifier Overlay Test Complete! ==="
    Write-Host "Check screenshots to verify platform-specific symbols."
}
finally {
    Cleanup
}
