<#
E2E verification: human plays freely, then CLI replays and verifies.

Usage:
  powershell -ExecutionPolicy Bypass -File .\Assets\Tests\Demo\scripts\verify-replay-via-cli.ps1
  powershell -ExecutionPolicy Bypass -File .\Assets\Tests\Demo\scripts\verify-replay-via-cli.ps1 -ProjectPath C:\path\to\project

Prerequisites:
  - Unity Editor running with InputReplayVerificationScene loaded
  - PlayMode is not running because this script starts it
#>

[CmdletBinding()]
param(
    [string]$ProjectPath = "",
    [int]$UnityWaitAttempts = 15,
    [int]$ReplayTimeoutSeconds = 60
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RecordingLogPath = ".uloop/outputs/InputRecordings/recording-event-log.txt"
$ReplayLogPath = ".uloop/outputs/InputRecordings/replay-event-log.txt"

function Get-UloopArguments {
    param(
        [string[]]$CommandArguments
    )

    if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
        return $CommandArguments
    }

    return @($CommandArguments + @("--project-path", $ProjectPath))
}

function Invoke-UloopCapture {
    param(
        [string[]]$CommandArguments
    )

    [string[]]$arguments = Get-UloopArguments -CommandArguments $CommandArguments
    [object[]]$output = & uloop @arguments 2>&1
    [int]$exitCode = $LASTEXITCODE
    [string]$text = ($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine

    return [pscustomobject]@{
        ExitCode = $exitCode
        Text = $text
        Arguments = $arguments
    }
}

function Invoke-Uloop {
    param(
        [string[]]$CommandArguments
    )

    [pscustomobject]$result = Invoke-UloopCapture -CommandArguments $CommandArguments
    if ($result.ExitCode -eq 0) {
        return $result.Text
    }

    if (-not [string]::IsNullOrWhiteSpace($result.Text)) {
        Write-Host $result.Text
    }

    [string]$commandText = "uloop " + ($result.Arguments -join " ")
    throw "$commandText failed with exit code $($result.ExitCode)"
}

function Wait-UnityReady {
    for ([int]$attempt = 0; $attempt -lt $UnityWaitAttempts; $attempt++) {
        [pscustomobject]$result = Invoke-UloopCapture -CommandArguments @("get-logs", "--max-count", "1")
        if ($result.ExitCode -eq 0) {
            return
        }

        Start-Sleep -Seconds 2
    }

    throw "Unity not responding"
}

function Invoke-ActivateForRecord {
    Invoke-Uloop -CommandArguments @(
        "execute-dynamic-code",
        "--code",
        @'
var cube = GameObject.Find("VerificationCube");
if (cube == null) return "ERROR: VerificationCube not found";
cube.SendMessage("ActivateForExternalControl");
return "OK: activated for recording";
'@
    ) | Out-Null
}

function Invoke-ActivateForReplay {
    Invoke-Uloop -CommandArguments @(
        "execute-dynamic-code",
        "--code",
        @'
var cube = GameObject.Find("VerificationCube");
if (cube == null) return "ERROR: VerificationCube not found";
cube.SendMessage("ActivateForExternalReplay");
return "OK: activated for replay";
'@
    ) | Out-Null
}

function Save-EventLog {
    param(
        [string]$Path
    )

    [string]$code = @"
var cube = GameObject.Find("VerificationCube");
if (cube == null) return "ERROR: VerificationCube not found";
cube.SendMessage("SaveLog", "$Path");
return "OK: log saved";
"@

    Invoke-Uloop -CommandArguments @("execute-dynamic-code", "--code", $code) | Out-Null
}

function Get-ReplayStatus {
    [pscustomobject]$result = Invoke-UloopCapture -CommandArguments @("replay-input", "--action", "Status")
    if ($result.ExitCode -ne 0) {
        return [pscustomobject]@{
            IsReplaying = $null
            Progress = $null
            RawText = $result.Text
        }
    }

    [pscustomobject]$status = $result.Text | ConvertFrom-Json
    return [pscustomobject]@{
        IsReplaying = $status.IsReplaying
        Progress = $status.Progress
        RawText = $result.Text
    }
}

function Wait-ReplayCompleted {
    [string]$lastStatusText = ""

    for ([int]$waitedSeconds = 0; $waitedSeconds -lt $ReplayTimeoutSeconds; $waitedSeconds++) {
        [pscustomobject]$status = Get-ReplayStatus
        $lastStatusText = $status.RawText

        if ($status.IsReplaying -eq $false) {
            Write-Host "  Replay completed."
            return
        }

        if (($waitedSeconds % 5) -eq 0) {
            [string]$progressText = "..."
            if ($null -ne $status.Progress) {
                $progressText = $status.Progress.ToString()
            }

            Write-Host "  Progress: $progressText"
        }

        Start-Sleep -Seconds 1
    }

    Write-Host "ERROR: Replay did not complete within ${ReplayTimeoutSeconds}s"
    Write-Host "  Last status: $lastStatusText"
    throw "Replay timeout"
}

function Get-NormalizedFrames {
    param(
        [string]$Path
    )

    [string[]]$lines = Get-Content -LiteralPath $Path
    if ($lines.Count -eq 0) {
        throw "Log file is empty: $Path"
    }

    [System.Text.RegularExpressions.Match]$baseMatch = [regex]::Match($lines[0], "^Frame ([0-9]+): (.*)$")
    if (-not $baseMatch.Success) {
        throw "Log line does not start with a frame prefix: $($lines[0])"
    }

    [int]$baseFrame = [int]$baseMatch.Groups[1].Value
    [System.Collections.Generic.List[string]]$normalizedLines = [System.Collections.Generic.List[string]]::new()

    foreach ($line in $lines) {
        [System.Text.RegularExpressions.Match]$lineMatch = [regex]::Match($line, "^Frame ([0-9]+): (.*)$")
        if (-not $lineMatch.Success) {
            throw "Log line does not start with a frame prefix: $line"
        }

        [int]$frame = [int]$lineMatch.Groups[1].Value
        [string]$rest = $lineMatch.Groups[2].Value
        $normalizedLines.Add("Frame $($frame - $baseFrame): $rest")
    }

    return ,$normalizedLines.ToArray()
}

function Compare-NormalizedLogs {
    param(
        [string[]]$RecordingLines,
        [string[]]$ReplayLines
    )

    [int]$maxCount = [Math]::Max($RecordingLines.Count, $ReplayLines.Count)
    [System.Collections.Generic.List[string]]$diffLines = [System.Collections.Generic.List[string]]::new()

    for ([int]$index = 0; $index -lt $maxCount; $index++) {
        [string]$recordingLine = ""
        if ($index -lt $RecordingLines.Count) {
            $recordingLine = $RecordingLines[$index]
        }

        [string]$replayLine = ""
        if ($index -lt $ReplayLines.Count) {
            $replayLine = $ReplayLines[$index]
        }

        if ($recordingLine -eq $replayLine) {
            continue
        }

        if ($index -lt $RecordingLines.Count) {
            $diffLines.Add("< $recordingLine")
        }

        if ($index -lt $ReplayLines.Count) {
            $diffLines.Add("> $replayLine")
        }
    }

    return ,$diffLines.ToArray()
}

Write-Host ""
Write-Host "========================================="
Write-Host "  Input Record/Replay E2E Verification"
Write-Host "========================================="

Write-Host ""
Write-Host "[1/8] Starting PlayMode..."
Invoke-Uloop -CommandArguments @("control-play-mode", "--action", "Play") | Out-Null
Write-Host "  Waiting for Unity..."
Start-Sleep -Seconds 6
Wait-UnityReady

Write-Host "[2/8] Activating controller..."
Invoke-ActivateForRecord

Write-Host "[3/8] Starting recording via CLI..."
Invoke-Uloop -CommandArguments @("record-input", "--action", "Start") | Out-Null

Write-Host ""
Write-Host "========================================="
Write-Host "  Recording is active!"
Write-Host "  Go to the Unity Game View and play."
Write-Host ""
Write-Host "  WASD: move | Mouse: rotate"
Write-Host "  Left click: red | Right click: blue"
Write-Host "  Scroll: scale"
Write-Host ""
Write-Host "  Press ENTER here when done."
Write-Host "========================================="
Write-Host ""
Read-Host | Out-Null

Write-Host "[4/8] Saving event log + deactivating controller..."
Invoke-Uloop -CommandArguments @(
    "execute-dynamic-code",
    "--code",
    @'
var cube = GameObject.Find("VerificationCube");
if (cube == null) return "ERROR: VerificationCube not found";
cube.SendMessage("SaveLog", ".uloop/outputs/InputRecordings/recording-event-log.txt");
cube.SendMessage("ClearLog");
return "OK: log saved, controller deactivated";
'@
) | Out-Null

Write-Host "  Stopping recording via CLI..."
Invoke-Uloop -CommandArguments @("record-input", "--action", "Stop") | Out-Null

Write-Host "[5/8] Restarting PlayMode..."
Invoke-Uloop -CommandArguments @("control-play-mode", "--action", "Stop") | Out-Null
Start-Sleep -Seconds 3
Invoke-Uloop -CommandArguments @("control-play-mode", "--action", "Play") | Out-Null
Write-Host "  Waiting for Unity..."
Start-Sleep -Seconds 6
Wait-UnityReady

Write-Host "[6/8] Activating controller + starting replay via CLI..."
Invoke-ActivateForReplay
Write-Host "  Starting replay..."
[pscustomobject]$replayResult = Invoke-UloopCapture -CommandArguments @("replay-input", "--action", "Start")
Write-Host "  $($replayResult.Text)"

Write-Host "  Waiting for replay to finish..."
Start-Sleep -Seconds 2
Wait-ReplayCompleted
Write-Host ""

Start-Sleep -Seconds 1

Write-Host "[7/8] Saving replay event log..."
Save-EventLog -Path $ReplayLogPath

Write-Host ""
Write-Host "[8/8] Comparing logs..."
Write-Host ""

[string[]]$recordingNormalized = Get-NormalizedFrames -Path $RecordingLogPath
[string[]]$replayNormalized = Get-NormalizedFrames -Path $ReplayLogPath
[string[]]$diffLines = Compare-NormalizedLogs -RecordingLines $recordingNormalized -ReplayLines $replayNormalized

if ($diffLines.Count -eq 0) {
    Write-Host "========================================="
    Write-Host "  RESULT: MATCH ($($recordingNormalized.Count) events identical)"
    Write-Host "  Relative frame timing verified."
    Write-Host "========================================="
    Write-Host ""
    exit 0
}

Write-Host "========================================="
Write-Host "  RESULT: MISMATCH ($($diffLines.Count) differences)"
Write-Host "========================================="
Write-Host ""
$diffLines | Select-Object -First 20 | ForEach-Object { Write-Host $_ }
Write-Host ""
exit 1
