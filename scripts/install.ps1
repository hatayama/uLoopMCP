$ErrorActionPreference = "Stop"

$Repository = "hatayama/unity-cli-loop"
$LegacyNpmPackage = "uloop-cli"
$Version = if ($env:ULOOP_VERSION) { $env:ULOOP_VERSION } else { "latest" }
$InstallDir = if ($env:ULOOP_INSTALL_DIR) {
    $env:ULOOP_INSTALL_DIR
} else {
    Join-Path $env:LOCALAPPDATA "Programs\uloop\bin"
}
$AssetName = "uloop-windows-amd64.zip"
$LegacyCleanupFailed = $false

if ($Version -eq "latest") {
    $DownloadUrl = "https://github.com/$Repository/releases/latest/download/$AssetName"
} else {
    $DownloadUrl = "https://github.com/$Repository/releases/download/$Version/$AssetName"
}
$ChecksumUrl = "$DownloadUrl.sha256"

function Test-RemoveLegacyEnabled {
    if (-not $env:ULOOP_REMOVE_LEGACY) {
        return $false
    }

    $EnabledValues = @("1", "true", "yes")
    return $EnabledValues -contains $env:ULOOP_REMOVE_LEGACY.ToLowerInvariant()
}

function Get-NpmCommand {
    $CommandNames = @("npm.cmd", "npm.exe", "npm")
    foreach ($CommandName in $CommandNames) {
        $Command = Get-Command $CommandName -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($Command) {
            return $Command.Source
        }
    }

    return $null
}

function Test-LegacyNpmInstalled {
    $NpmCommand = Get-NpmCommand
    if (-not $NpmCommand) {
        return $false
    }

    & $NpmCommand list -g $LegacyNpmPackage --depth=0 > $null 2> $null
    return $LASTEXITCODE -eq 0
}

function Remove-LegacyNpmIfEnabled {
    if (-not (Test-LegacyNpmInstalled)) {
        return
    }

    if (Test-RemoveLegacyEnabled) {
        $NpmCommand = Get-NpmCommand
        Write-Host "Removing legacy npm installation: $LegacyNpmPackage"
        & $NpmCommand uninstall -g $LegacyNpmPackage
        if ($LASTEXITCODE -ne 0) {
            $script:LegacyCleanupFailed = $true
            Write-Warning "Could not remove legacy npm installation: $LegacyNpmPackage"
            Write-Host "To remove it manually, run:"
            Write-Host "  npm uninstall -g $LegacyNpmPackage"
        }
    }
}

function Confirm-ActiveUloopAfterLegacyCleanup {
    if (-not $script:LegacyCleanupFailed) {
        return
    }

    $ResolvedCommand = Get-Command uloop -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $ResolvedCommand) {
        return
    }

    $ExpectedUloop = Join-Path $InstallDir "uloop.exe"
    if ([string]::Equals($ResolvedCommand.Source, $ExpectedUloop, [System.StringComparison]::OrdinalIgnoreCase)) {
        return
    }

    throw "Failed to remove legacy npm installation, and PATH still resolves uloop to $($ResolvedCommand.Source). The native dispatcher was installed to $ExpectedUloop, but running uloop may still use the legacy command. Remove the legacy package manually, or move $InstallDir earlier in PATH."
}

function Write-LegacyNpmWarningIfPresent {
    if ((Test-RemoveLegacyEnabled) -or (-not (Test-LegacyNpmInstalled))) {
        return
    }

    Write-Host "Legacy npm installation detected: $LegacyNpmPackage"
    Write-Host "The native dispatcher was installed, but the npm package may still provide an older uloop command."
    Write-Host "To remove it, run:"
    Write-Host "  npm uninstall -g $LegacyNpmPackage"
    Write-Host "Or rerun this installer with:"
    Write-Host "  `$env:ULOOP_REMOVE_LEGACY = `"1`""
}

function Report-PathShadowing {
    $ResolvedCommand = Get-Command uloop -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $ResolvedCommand) {
        return
    }

    $ExpectedUloop = Join-Path $InstallDir "uloop.exe"
    if ([string]::Equals($ResolvedCommand.Source, $ExpectedUloop, [System.StringComparison]::OrdinalIgnoreCase)) {
        return
    }

    if (Test-RepairedLegacyShim -ShimPath $ResolvedCommand.Source -NativeUloopPath $ExpectedUloop) {
        Write-Host "PATH resolves uloop through a repaired legacy shim:"
        Write-Host "  $($ResolvedCommand.Source)"
        Write-Host "The shim forwards to:"
        Write-Host "  $ExpectedUloop"
        return
    }

    Write-Host "Installed uloop to $ExpectedUloop, but PATH resolves uloop to:"
    Write-Host "  $($ResolvedCommand.Source)"
    Write-Host "Move $InstallDir earlier in PATH, or remove the legacy installation if it owns that command."
}

function Test-LegacyUloopShimContent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Content
    )

    return $Content.IndexOf("node_modules/uloop-cli", [System.StringComparison]::OrdinalIgnoreCase) -ge 0 `
        -or $Content.IndexOf("node_modules\uloop-cli", [System.StringComparison]::OrdinalIgnoreCase) -ge 0
}

function ConvertTo-PowerShellSingleQuotedString {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    return "'" + $Value.Replace("'", "''") + "'"
}

function ConvertTo-PosixDoubleQuotedString {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    $EscapedValue = $Value.Replace("\", "\\").Replace("$", "\$").Replace("`"", "\`"")
    return "`"$EscapedValue`""
}

function New-LegacyShimForwarderContent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ShimPath,
        [Parameter(Mandatory = $true)]
        [string]$NativeUloopPath
    )

    $Extension = [System.IO.Path]::GetExtension($ShimPath)
    if ([string]::Equals($Extension, ".ps1", [System.StringComparison]::OrdinalIgnoreCase)) {
        $QuotedNativeUloopPath = ConvertTo-PowerShellSingleQuotedString -Value $NativeUloopPath
        return "#!/usr/bin/env pwsh`n& $QuotedNativeUloopPath @args`nexit `$LASTEXITCODE`n"
    }

    if ([string]::Equals($Extension, ".cmd", [System.StringComparison]::OrdinalIgnoreCase)) {
        return "@echo off`r`n`"$NativeUloopPath`" %*`r`nexit /b %ERRORLEVEL%`r`n"
    }

    $QuotedShellPath = ConvertTo-PosixDoubleQuotedString -Value $NativeUloopPath
    return "#!/bin/sh`nnative_path=$QuotedShellPath`nif command -v cygpath >/dev/null 2>&1; then`n  native_path=`"`$(cygpath -u `"`$native_path`")`"`nfi`nexec `"`$native_path`" `"`$@`"`n"
}

function Repair-LegacyUloopShims {
    param(
        [Parameter(Mandatory = $true)]
        [string]$NativeUloopPath
    )

    if (-not $env:APPDATA) {
        return
    }

    $LegacyBinDir = Join-Path $env:APPDATA "npm"
    if (-not (Test-Path $LegacyBinDir -PathType Container)) {
        return
    }

    $ShimNames = @("uloop", "uloop.cmd", "uloop.ps1")
    foreach ($ShimName in $ShimNames) {
        $ShimPath = Join-Path $LegacyBinDir $ShimName
        if (-not (Test-Path $ShimPath -PathType Leaf)) {
            continue
        }

        $ShimContent = [System.IO.File]::ReadAllText($ShimPath)
        if (-not (Test-LegacyUloopShimContent -Content $ShimContent)) {
            continue
        }

        $ForwarderContent = New-LegacyShimForwarderContent -ShimPath $ShimPath -NativeUloopPath $NativeUloopPath
        [System.IO.File]::WriteAllText($ShimPath, $ForwarderContent, [System.Text.Encoding]::UTF8)
        Write-Host "Repaired legacy uloop shim: $ShimPath"
    }
}

function Test-RepairedLegacyShim {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ShimPath,
        [Parameter(Mandatory = $true)]
        [string]$NativeUloopPath
    )

    if (-not (Test-Path $ShimPath -PathType Leaf)) {
        return $false
    }

    $ShimContent = [System.IO.File]::ReadAllText($ShimPath)
    return $ShimContent.IndexOf($NativeUloopPath, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
}

function Assert-UloopVersionSucceeds {
    param(
        [Parameter(Mandatory = $true)]
        [string]$UloopPath,
        [switch]$Quiet
    )

    if ($Quiet) {
        & $UloopPath --version > $null
    }
    else {
        & $UloopPath --version
    }

    if ($LASTEXITCODE -ne 0) {
        throw "uloop binary verification failed for $UloopPath"
    }
}

$TempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("uloop-install-" + [System.Guid]::NewGuid().ToString("N"))
$StagedUloopPath = $null
New-Item -ItemType Directory -Path $TempDir | Out-Null

try {
    $ArchivePath = Join-Path $TempDir $AssetName
    $ChecksumPath = Join-Path $TempDir "$AssetName.sha256"
    Invoke-WebRequest -Uri $DownloadUrl -OutFile $ArchivePath
    Invoke-WebRequest -Uri $ChecksumUrl -OutFile $ChecksumPath
    $ExpectedHash = ((Get-Content -Path $ChecksumPath -Raw) -split "\s+")[0].ToLowerInvariant()
    $ActualHash = (Get-FileHash -Path $ArchivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($ExpectedHash -ne $ActualHash) {
        throw "Checksum mismatch for $AssetName"
    }

    Expand-Archive -Path $ArchivePath -DestinationPath $TempDir -Force

    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    $StagedUloopPath = Join-Path $InstallDir ("uloop-install-" + [System.Guid]::NewGuid().ToString("N") + ".exe")
    Copy-Item -Path (Join-Path $TempDir "uloop.exe") -Destination $StagedUloopPath -Force
    Assert-UloopVersionSucceeds -UloopPath $StagedUloopPath -Quiet
    Remove-LegacyNpmIfEnabled
    $FinalUloopPath = Join-Path $InstallDir "uloop.exe"
    Copy-Item -Path $StagedUloopPath -Destination $FinalUloopPath -Force
    Remove-Item -Path $StagedUloopPath -Force
    $StagedUloopPath = $null

    $UserPath = [Environment]::GetEnvironmentVariable("Path", "User")
    $PathEntries = @()
    if ($UserPath) {
        $PathEntries = $UserPath -split ";"
    }

    if ($PathEntries -notcontains $InstallDir) {
        $NewUserPath = if ($UserPath) { "$UserPath;$InstallDir" } else { $InstallDir }
        [Environment]::SetEnvironmentVariable("Path", $NewUserPath, "User")
        $env:Path = "$env:Path;$InstallDir"
        Write-Host "Added $InstallDir to User PATH. Open a new terminal to use it everywhere."
    }

    Assert-UloopVersionSucceeds -UloopPath $FinalUloopPath
    Repair-LegacyUloopShims -NativeUloopPath $FinalUloopPath
    Confirm-ActiveUloopAfterLegacyCleanup
    Write-LegacyNpmWarningIfPresent
    Report-PathShadowing
}
finally {
    if ($StagedUloopPath -and (Test-Path $StagedUloopPath)) {
        Remove-Item -Path $StagedUloopPath -Force -ErrorAction SilentlyContinue
    }
    Remove-Item -Path $TempDir -Recurse -Force -ErrorAction SilentlyContinue
}
