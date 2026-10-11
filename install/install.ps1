# Installs JSynth on Windows: the latest release, a Start menu shortcut, and Julia if it's missing.
#
#   irm https://raw.githubusercontent.com/ozanbora/JSynth/master/install/install.ps1 | iex
#
# Or, downloaded, with options:
#   .\install.ps1 [-Version 1.0.0] [-InstallDir <folder>] [-NoJulia] [-Uninstall]
#
# For this user only, so no administrator rights are needed. Settings, the library and designs are left alone when
# uninstalling (they are in %AppData%\JSynth), and so is Julia.
param(
    [string]$Version,
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\JSynth'),
    [switch]$NoJulia,
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'   # Invoke-WebRequest's progress bar slows downloads a lot.

# Windows PowerShell 5.1 may not offer TLS 1.2, which GitHub requires.
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

$Repository = 'ozanbora/JSynth'
$Shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'JSynth.lnk'

function Stop-RunningJSynth {
    $running = Get-Process -Name JSynth -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$InstallDir\*" }
    if ($running) {
        throw "JSynth is running from $InstallDir. Close it, then run this again."
    }
}

if ($Uninstall) {
    Stop-RunningJSynth
    if (Test-Path $InstallDir) { Remove-Item -Recurse -Force $InstallDir }
    if (Test-Path $Shortcut) { Remove-Item -Force $Shortcut }
    Write-Host "JSynth is uninstalled. Your settings and designs are still in $env:APPDATA\JSynth."
    return
}

# An x64 build: it also runs on Windows 11 on ARM, which emulates x64.
if (-not [Environment]::Is64BitOperatingSystem) {
    throw 'JSynth needs 64-bit Windows.'
}

# The release: the latest, or the one asked for.
try {
    $release = if ($Version) {
        Invoke-RestMethod "https://api.github.com/repos/$Repository/releases/tags/v$($Version.TrimStart('v'))"
    } else {
        Invoke-RestMethod "https://api.github.com/repos/$Repository/releases/latest"
    }
}
catch {
    throw "There is no JSynth $(if ($Version) { $Version } else { 'release' }) to download (see https://github.com/$Repository/releases): $($_.Exception.Message)"
}
$asset = $release.assets | Where-Object { $_.name -like '*-win-x64.zip' } | Select-Object -First 1
if (-not $asset) {
    throw "Release $($release.tag_name) has no Windows download."
}

Write-Host "Installing JSynth $($release.tag_name.TrimStart('v')) in $InstallDir..."
$temp = Join-Path ([IO.Path]::GetTempPath()) "jsynth-install-$([Guid]::NewGuid())"
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    $zip = Join-Path $temp $asset.name
    Invoke-WebRequest $asset.browser_download_url -OutFile $zip -UseBasicParsing
    Expand-Archive $zip -DestinationPath $temp

    # The zip holds one JSynth folder; it replaces the one installed before.
    Stop-RunningJSynth
    if (Test-Path $InstallDir) { Remove-Item -Recurse -Force $InstallDir }
    New-Item -ItemType Directory -Path (Split-Path $InstallDir) -Force | Out-Null
    Move-Item (Join-Path $temp 'JSynth') $InstallDir
}
finally {
    Remove-Item -Recurse -Force $temp -ErrorAction SilentlyContinue
}

# Downloaded files are marked as coming from the internet; the program is ours to run now.
Get-ChildItem $InstallDir -Recurse -File | Unblock-File

$exe = Join-Path $InstallDir 'JSynth.exe'
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($Shortcut)
$link.TargetPath = $exe
$link.WorkingDirectory = $InstallDir
$link.Description = 'JSynth: a visual editor for Julia'
$link.Save()
Write-Host "Added JSynth to the Start menu."

# Julia: JSynth runs designs with it. juliaup is the installer Julia recommends; JSynth finds the julia it installs.
$julia = Get-Command julia -ErrorAction SilentlyContinue
if ($julia) {
    Write-Host "Julia is installed: $($julia.Source)"
} elseif ($NoJulia) {
    Write-Host 'Julia is not installed: JSynth needs it to run designs. See https://julialang.org/downloads/'
} elseif (Get-Command winget -ErrorAction SilentlyContinue) {
    Write-Host 'Installing Julia (juliaup, from the Microsoft Store)...'
    winget install --name Julia --id 9NJNWW8PVKMN --exact --source msstore --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -ne 0) {
        Write-Warning 'Julia could not be installed. Install it from https://julialang.org/downloads/ before running a design.'
    }
} else {
    Write-Warning 'Julia is not installed and winget is not available. Install Julia from https://julialang.org/downloads/ before running a design.'
}

Write-Host ''
Write-Host "JSynth is installed. Start it from the Start menu, or run: $exe"
