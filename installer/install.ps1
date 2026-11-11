# Card Tier Overlay installer for Slay the Spire 2.
# Run through Install.bat / Uninstall.bat; no need to open it by hand.
param([switch]$Uninstall)

$ErrorActionPreference = 'Stop'
$ModId = 'StsTierOverlay'
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path

function Test-GameDir([string]$dir) {
    return $dir -and (Test-Path -LiteralPath (Join-Path $dir 'SlayTheSpire2.exe'))
}

function Find-GameDir {
    $candidates = New-Object System.Collections.Generic.List[string]
    # 1) Next to this installer (unzipped inside or beside the game folder)
    $candidates.Add($Here)
    $candidates.Add((Split-Path -Parent $Here))
    $candidates.Add((Join-Path $Here 'Slay the Spire 2'))
    $candidates.Add((Join-Path (Split-Path -Parent $Here) 'Slay the Spire 2'))

    # 2) Steam libraries
    $steamRoots = @()
    foreach ($key in 'HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam', 'HKLM:\SOFTWARE\Valve\Steam') {
        try {
            $p = Get-ItemProperty -Path $key -ErrorAction Stop
            if ($p.SteamPath) { $steamRoots += $p.SteamPath }
            if ($p.InstallPath) { $steamRoots += $p.InstallPath }
        } catch { }
    }
    foreach ($root in ($steamRoots | Select-Object -Unique)) {
        $libs = @($root)
        $vdf = Join-Path $root 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf) {
            foreach ($m in [regex]::Matches((Get-Content -LiteralPath $vdf -Raw), '"path"\s+"([^"]+)"')) {
                $libs += $m.Groups[1].Value -replace '\\\\', '\'
            }
        }
        foreach ($lib in $libs) { $candidates.Add((Join-Path $lib 'steamapps\common\Slay the Spire 2')) }
    }

    foreach ($c in $candidates) { if (Test-GameDir $c) { return (Resolve-Path -LiteralPath $c).Path } }
    return $null
}

$game = Find-GameDir
while (-not $game) {
    Write-Host ''
    Write-Host 'Could not find Slay the Spire 2 automatically.'
    $answer = Read-Host 'Drag the game folder (the one with SlayTheSpire2.exe) here and press Enter'
    $answer = $answer.Trim().Trim('"')
    if (-not $answer) { Write-Host 'Cancelled.'; exit 1 }
    if ((Test-Path -LiteralPath $answer -PathType Leaf)) { $answer = Split-Path -Parent $answer }
    if (Test-GameDir $answer) { $game = $answer }
    elseif (Test-GameDir (Join-Path $answer 'Slay the Spire 2')) { $game = Join-Path $answer 'Slay the Spire 2' }
    else { Write-Host "SlayTheSpire2.exe is not in: $answer" -ForegroundColor Yellow }
}

Write-Host "Game: $game"
$target = Join-Path $game "mods\$ModId"

if (Get-Process -Name 'SlayTheSpire2' -ErrorAction SilentlyContinue) {
    Write-Host ''
    Write-Host 'The game is running. Close it and run the installer again.' -ForegroundColor Yellow
    exit 1
}

if ($Uninstall) {
    if (Test-Path -LiteralPath $target) {
        Remove-Item -LiteralPath $target -Recurse -Force
        Write-Host 'Mod uninstalled.' -ForegroundColor Green
    } else {
        Write-Host 'The mod was not installed.'
    }
    exit 0
}

$source = Join-Path $Here $ModId
if (-not (Test-Path -LiteralPath (Join-Path $source "$ModId.dll"))) {
    Write-Host "The '$ModId' folder is missing next to the installer. Extract the whole zip." -ForegroundColor Red
    exit 1
}

# Keep what the player already has (newer downloaded tiers, settings, update history).
$keep = 'tiers.txt', 'no_update.txt', 'bookmark_ok.txt', 'last_import.txt'
$saved = @{}
foreach ($f in $keep) {
    $p = Join-Path $target $f
    if (Test-Path -LiteralPath $p) { $saved[$f] = [IO.File]::ReadAllBytes($p) }
}

if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $target -Recurse -Force
function Get-TiersDate([byte[]]$bytes) {
    try { return [string](([Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json)._updated) } catch { return '' }
}
foreach ($f in $saved.Keys) {
    $dest = Join-Path $target $f
    # Tier data: keep the player's copy only if it is at least as new as the bundled one.
    if ($f -eq 'tiers.txt' -and (Test-Path -LiteralPath $dest) -and
        ([string]::CompareOrdinal((Get-TiersDate $saved[$f]), (Get-TiersDate ([IO.File]::ReadAllBytes($dest)))) -lt 0)) { continue }
    [IO.File]::WriteAllBytes($dest, $saved[$f])
}

Write-Host ''
Write-Host "Installed to: $target" -ForegroundColor Green
Write-Host 'Start the game. If it asks whether to load mods, say yes.'
