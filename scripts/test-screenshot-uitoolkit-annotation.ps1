<#
E2E verification that screenshot --annotate-elements lists UI Toolkit controls, using the uloop CLI against
UIToolkitAnnotationDemoScene. Safe to run repeatedly: it stops PlayMode and restores the Game View size on exit,
and never saves the scene.

Usage:
  powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-screenshot-uitoolkit-annotation.ps1
  powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-screenshot-uitoolkit-annotation.ps1 -ProjectPath C:\path\to\project

Prerequisites:
  - Unity Editor is running
  - uloop is available on PATH
#>

[CmdletBinding()]
param(
    [string]$ProjectPath = "",
    [int]$UnityWaitAttempts = 15,
    [int]$PlayModeWaitSeconds = 20,
    [int]$ControlWaitSeconds = 20
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ScenePath = "Assets/Scenes/UIToolkitAnnotationDemoScene.unity"
$TempDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "unity-cli-loop-uitk-annotation"

function Get-UloopArguments {
    param(
        [string[]]$CommandArguments
    )

    if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
        return $CommandArguments
    }

    return @($CommandArguments + @("--project-path", $ProjectPath))
}

function ConvertTo-WindowsPowerShellNativeArgument {
    param(
        [string]$Argument
    )

    # Windows argv rules treat backslashes specially only before a quote or the closing quote.
    # Do not double every backslash: ordinary paths such as C:\Users must stay unchanged.
    [string]$escapedArgument = [regex]::Replace($Argument, '(\\*)"', {
        param($match)

        [string]$backslashes = $match.Groups[1].Value
        return $backslashes + $backslashes + '\"'
    })

    if ($Argument -notmatch '\s') {
        return $escapedArgument
    }

    return [regex]::Replace($escapedArgument, '(\\*)\z', {
        param($match)

        [string]$backslashes = $match.Groups[1].Value
        return $backslashes + $backslashes
    })
}

function Invoke-UloopCapture {
    param(
        [string[]]$CommandArguments
    )

    [string[]]$arguments = Get-UloopArguments -CommandArguments $CommandArguments
    if ($PSVersionTable.PSVersion.Major -lt 6) {
        $arguments = @($arguments | ForEach-Object { ConvertTo-WindowsPowerShellNativeArgument -Argument $_ })
    }

    [string]$previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        [object[]]$output = & uloop @arguments 2>&1
        [int]$exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    [string]$text = ($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine

    return [pscustomobject]@{
        ExitCode = $exitCode
        Text = $text
        Arguments = $arguments
    }
}

function Invoke-UloopJson {
    param(
        [string[]]$CommandArguments
    )

    [pscustomobject]$result = Invoke-UloopCapture -CommandArguments $CommandArguments
    if ($result.ExitCode -ne 0) {
        if (-not [string]::IsNullOrWhiteSpace($result.Text)) {
            Write-Host $result.Text
        }

        [string]$commandText = "uloop " + ($result.Arguments -join " ")
        throw "$commandText failed with exit code $($result.ExitCode)"
    }

    return $result.Text | ConvertFrom-Json
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

function Wait-PlayMode {
    for ([int]$attempt = 0; $attempt -lt $PlayModeWaitSeconds; $attempt++) {
        [pscustomobject]$probe = Invoke-UloopCapture -CommandArguments @(
            "execute-dynamic-code",
            "--code",
            "using UnityEngine; return Application.isPlaying;"
        )

        if ($probe.ExitCode -ne 0) {
            Start-Sleep -Seconds 1
            continue
        }

        [pscustomobject]$result = $probe.Text | ConvertFrom-Json
        if ($result.Success -eq $true -and $result.Result -eq "True") {
            return
        }

        Start-Sleep -Seconds 1
    }

    throw "Unity did not enter PlayMode within ${PlayModeWaitSeconds}s"
}

function Assert-EqualText {
    param(
        [string]$Actual,
        [string]$Expected,
        [string]$Context
    )

    if ($Actual -eq $Expected) {
        return
    }

    throw "${Context}: expected '$Expected', got '$Actual'"
}

function Select-FullHdGameView {
    [pscustomobject]$response = Invoke-UloopJson -CommandArguments @(
        "set-game-view-size",
        "--width",
        "1920",
        "--height",
        "1080"
    )

    if ($response.Success -ne $true) {
        throw "Set Full HD Game View failed: $($response.Message)"
    }

    Assert-EqualText -Actual ([string]$response.CurrentWidth) -Expected "1920" -Context "Game View width"
    Assert-EqualText -Actual ([string]$response.CurrentHeight) -Expected "1080" -Context "Game View height"
    Write-Host "    $($response.CurrentWidth)x$($response.CurrentHeight) (previous: $($response.PreviousWidth)x$($response.PreviousHeight))"
    Start-Sleep -Seconds 1

    return [pscustomobject]@{
        Width = [int]$response.PreviousWidth
        Height = [int]$response.PreviousHeight
    }
}

function Restore-GameViewSize {
    param(
        [pscustomobject]$OriginalSize
    )

    if ($null -eq $OriginalSize) {
        return
    }

    Invoke-UloopCapture -CommandArguments @(
        "set-game-view-size",
        "--width",
        $OriginalSize.Width.ToString([Globalization.CultureInfo]::InvariantCulture),
        "--height",
        $OriginalSize.Height.ToString([Globalization.CultureInfo]::InvariantCulture)
    ) | Out-Null
}

# Opening the scene in Single mode closes every loaded scene, so unsaved changes anywhere stop the run instead
# of being discarded.
function Initialize-DemoScene {
    [pscustomobject]$stopResult = Invoke-UloopCapture -CommandArguments @("control-play-mode", "--action", "Stop")
    if ($stopResult.ExitCode -ne 0 -and -not [string]::IsNullOrWhiteSpace($stopResult.Text)) {
        Write-Host $stopResult.Text
    }

    [string]$code = @"
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
for (int i = 0; i < SceneManager.sceneCount; i++)
{
    Scene loaded = SceneManager.GetSceneAt(i);
    if (loaded.isLoaded && loaded.isDirty) { return "DIRTY: " + loaded.path; }
}
PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
if (prefabStage != null && prefabStage.scene.isDirty) { return "DIRTY: " + prefabStage.assetPath; }
string scenePath = "$ScenePath";
if (SceneManager.GetActiveScene().path != scenePath)
{
    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
}
return SceneManager.GetActiveScene().path;
"@

    [pscustomobject]$result = Invoke-UloopJson -CommandArguments @("execute-dynamic-code", "--code", $code)
    if ($result.Success -ne $true) {
        throw "Failed to load ${ScenePath}: $($result.ErrorMessage)"
    }

    [string]$scene = [string]$result.Result
    if ($scene.StartsWith("DIRTY: ")) {
        throw "Unsaved changes would be lost by opening the demo scene ($($scene.Substring(7))). Save or discard them first."
    }

    Assert-EqualText -Actual $scene -Expected $ScenePath -Context "Active scene"
}

function Get-UiToolkitElements {
    param(
        [pscustomobject]$Response
    )

    return ,@($Response.Screenshots[0].AnnotatedElements | Where-Object { $_.UiSystem -eq "UIToolkit" })
}

function Invoke-ElementsOnlyCapture {
    [pscustomobject]$response = Invoke-UloopJson -CommandArguments @(
        "screenshot",
        "--capture-mode",
        "rendering",
        "--annotate-elements",
        "--elements-only"
    )

    if ($response.Success -ne $true -or $response.ScreenshotCount -ne 1) {
        throw "Elements-only capture failed: $($response.Message)"
    }

    return $response
}

# The demo builds its controls in Start, so the first captures after entering PlayMode can miss them.
function Wait-UiToolkitPlay {
    for ([int]$attempt = 0; $attempt -lt $ControlWaitSeconds; $attempt++) {
        [pscustomobject]$response = Invoke-ElementsOnlyCapture
        [object[]]$elements = Get-UiToolkitElements -Response $response
        if (@($elements | Where-Object { $_.Name -eq "play" }).Count -gt 0) {
            return
        }

        Start-Sleep -Seconds 1
    }

    throw "UI Toolkit element 'play' did not appear"
}

function Assert-Condition {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Near {
    param(
        [double]$Actual,
        [double]$Expected,
        [string]$Context
    )

    Assert-Condition -Condition ([Math]::Abs($Actual - $Expected) -le 1) -Message "${Context}: expected $Expected (+/-1), got $Actual"
}

function Get-SingleUiToolkitElement {
    param(
        [object[]]$Elements,
        [string]$Name,
        [string]$Type,
        [string]$Interaction
    )

    [object[]]$found = @($Elements | Where-Object { $_.Name -eq $Name })
    Assert-Condition -Condition ($found.Count -eq 1) -Message "Expected exactly one UI Toolkit element named '$Name', found $($found.Count)"
    Assert-EqualText -Actual $found[0].Type -Expected $Type -Context "$Name Type"
    Assert-EqualText -Actual $found[0].Interaction -Expected $Interaction -Context "$Name Interaction"
    return $found[0]
}

function Test-AnnotatedCapture {
    $null = New-Item -ItemType Directory -Path $TempDirectory -Force
    [pscustomobject]$response = Invoke-UloopJson -CommandArguments @(
        "screenshot",
        "--capture-mode",
        "rendering",
        "--annotate-elements",
        "--output-directory",
        $TempDirectory
    )
    Assert-Condition -Condition ($response.Success -eq $true -and $response.ScreenshotCount -eq 1) -Message "Annotated capture failed: $($response.Message)"

    [object[]]$elements = Get-UiToolkitElements -Response $response
    [pscustomobject]$play = Get-SingleUiToolkitElement -Elements $elements -Name "play" -Type "Button" -Interaction "Click"
    Get-SingleUiToolkitElement -Elements $elements -Name "sound" -Type "Toggle" -Interaction "Click" | Out-Null
    Get-SingleUiToolkitElement -Elements $elements -Name "volume" -Type "Slider" -Interaction "Drag" | Out-Null
    Get-SingleUiToolkitElement -Elements $elements -Name "player-name" -Type "InputField" -Interaction "Text" | Out-Null
    Get-SingleUiToolkitElement -Elements $elements -Name "quality" -Type "Dropdown" -Interaction "Click" | Out-Null
    [pscustomobject]$partlyCovered = Get-SingleUiToolkitElement -Elements $elements -Name "partly-covered" -Type "Button" -Interaction "Click"
    Assert-Condition -Condition (@($elements | Where-Object { $_.Type -eq "Scrollbar" }).Count -eq 1) -Message "Expected exactly one UI Toolkit Scrollbar"
    # The six named controls plus the list's vertical Scroller; parts inside a control would add more.
    Assert-EqualText -Actual ([string]$elements.Count) -Expected "7" -Context "UI Toolkit element count"
    Assert-EqualText -Actual $play.Path -Expected "UIToolkitAnnotationDemo/BackDocument#play" -Context "UI Toolkit element 'play' path"

    [object[]]$allElements = @($response.Screenshots[0].AnnotatedElements)
    Assert-Condition -Condition (@($allElements | Where-Object { $_.Name -eq "locked" -or $_.Name -eq "ghost" }).Count -eq 0) -Message "Disabled or hidden UI Toolkit buttons were annotated"

    # Top-left Game View coordinates: a wrong Y flip moves these away from the layout in the demo.
    Assert-Near -Actual $play.BoundsMinX -Expected 100 -Context "play BoundsMinX"
    Assert-Near -Actual $play.BoundsMaxX -Expected 300 -Context "play BoundsMaxX"
    Assert-Near -Actual $play.BoundsMinY -Expected 100 -Context "play BoundsMinY"
    Assert-Near -Actual $play.BoundsMaxY -Expected 150 -Context "play BoundsMaxY"

    [double]$coverEdge = $partlyCovered.BoundsMinX + 0.6 * ($partlyCovered.BoundsMaxX - $partlyCovered.BoundsMinX)
    Assert-Condition -Condition ($partlyCovered.SimX -gt $coverEdge) -Message "UI Toolkit element 'partly-covered' should be clicked right of the panel covering its left 60%"

    foreach ($element in $elements) {
        [bool]$inside = $element.Path.Contains("#") `
            -and $element.BoundsMinX -le $element.SimX -and $element.SimX -le $element.BoundsMaxX `
            -and $element.BoundsMinY -le $element.SimY -and $element.SimY -le $element.BoundsMaxY
        Assert-Condition -Condition $inside -Message "UI Toolkit element '$($element.Path)' needs a '#' path and a click point inside its bounds"
    }

    Assert-Condition -Condition (@($allElements | Where-Object { $_.UiSystem -eq "UGUI" -and $_.Name -eq "ugui-button" }).Count -eq 1) -Message "Expected exactly one uGUI element named 'ugui-button'"

    [string]$imagePath = $response.Screenshots[0].ImagePath
    Assert-Condition -Condition ((Test-Path -LiteralPath $imagePath) -and (Get-Item -LiteralPath $imagePath).Length -gt 0) -Message "Annotated screenshot was not written: $imagePath"

    return $response
}

# Compares the path set first so an element missing only from the elements-only capture is not overlooked.
function Test-ElementsOnlyCapture {
    param(
        [pscustomobject]$AnnotatedResponse
    )

    [object[]]$first = Get-UiToolkitElements -Response $AnnotatedResponse
    [object[]]$second = Get-UiToolkitElements -Response (Invoke-ElementsOnlyCapture)
    [string]$firstPaths = (@($first | ForEach-Object { $_.Path }) | Sort-Object) -join "|"
    [string]$secondPaths = (@($second | ForEach-Object { $_.Path }) | Sort-Object) -join "|"
    Assert-EqualText -Actual $secondPaths -Expected $firstPaths -Context "UI Toolkit element paths in the elements-only capture"

    foreach ($element in $second) {
        [pscustomobject]$match = @($first | Where-Object { $_.Path -eq $element.Path })[0]
        foreach ($field in @("SimX", "SimY", "BoundsMinX", "BoundsMaxX", "BoundsMinY", "BoundsMaxY")) {
            Assert-Near -Actual $element.$field -Expected $match.$field -Context "$($element.Path) $field"
        }
    }
}

function Get-PlayClickCount {
    [string]$code = @"
UIToolkitAnnotationDemo demo = UnityEngine.Object.FindObjectOfType<UIToolkitAnnotationDemo>();
if (demo == null) { return "ERROR: UIToolkitAnnotationDemo not found"; }
return demo.GetClickCount("play");
"@

    [pscustomobject]$result = Invoke-UloopJson -CommandArguments @("execute-dynamic-code", "--code", $code)
    Assert-Condition -Condition ($result.Success -eq $true) -Message "Read play click count failed: $($result.ErrorMessage)"
    [string]$count = [string]$result.Result
    if ($count.StartsWith("ERROR:")) {
        throw "Read play click count failed: $($count.Substring(6).Trim())"
    }

    return $count
}

# simulate-mouse-input reaches UI Toolkit buttons through the EventSystem; simulate-mouse-ui does not.
function Test-Click {
    param(
        [pscustomobject]$AnnotatedResponse
    )

    [object[]]$elements = Get-UiToolkitElements -Response $AnnotatedResponse
    [pscustomobject]$play = @($elements | Where-Object { $_.Name -eq "play" })[0]
    [int]$before = [int](Get-PlayClickCount)

    [pscustomobject]$response = Invoke-UloopJson -CommandArguments @(
        "simulate-mouse-input",
        "--action",
        "Click",
        "--x",
        ([double]$play.SimX).ToString([Globalization.CultureInfo]::InvariantCulture),
        "--y",
        ([double]$play.SimY).ToString([Globalization.CultureInfo]::InvariantCulture)
    )
    Assert-Condition -Condition ($response.Success -eq $true) -Message "Click play failed: $($response.Message)"
    Start-Sleep -Seconds 1

    Assert-EqualText -Actual (Get-PlayClickCount) -Expected ([string]($before + 1)) -Context "play click count"
}

if (Test-Path -LiteralPath $TempDirectory) {
    Remove-Item -LiteralPath $TempDirectory -Recurse -Force
}

Write-Host ""
Write-Host "========================================="
Write-Host "  Screenshot UI Toolkit Annotation E2E"
Write-Host "========================================="

Wait-UnityReady

[pscustomobject]$originalGameViewSize = $null

try {
    Write-Host "[1/6] Loading UI Toolkit annotation demo scene..."
    Initialize-DemoScene

    Write-Host "[2/6] Selecting Full HD Game View resolution..."
    $originalGameViewSize = Select-FullHdGameView

    Write-Host "[3/6] Starting PlayMode and waiting for UI Toolkit controls..."
    Invoke-UloopJson -CommandArguments @("control-play-mode", "--action", "Play") | Out-Null
    Wait-PlayMode
    Wait-UiToolkitPlay

    Write-Host "[4/6] Checking the annotated capture..."
    [pscustomobject]$annotated = Test-AnnotatedCapture

    Write-Host "[5/6] Checking the elements-only capture matches..."
    Test-ElementsOnlyCapture -AnnotatedResponse $annotated

    Write-Host "[6/6] Clicking a UI Toolkit button at its annotated point..."
    Test-Click -AnnotatedResponse $annotated

    Write-Host ""
    Write-Host "========================================="
    Write-Host "  RESULT: PASS"
    Write-Host "========================================="
    exit 0
}
finally {
    Invoke-UloopCapture -CommandArguments @("control-play-mode", "--action", "Stop") | Out-Null
    Restore-GameViewSize -OriginalSize $originalGameViewSize
}
