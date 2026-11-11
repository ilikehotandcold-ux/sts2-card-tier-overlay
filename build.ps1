# Builds the mod and packages a ready-to-share zip in out/.
#   .\build.ps1                                  # finds the game via Steam / asks
#   .\build.ps1 -GameDir "C:\Games\Slay the Spire 2"
param([string]$GameDir)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$manifest = Get-Content -LiteralPath (Join-Path $root 'src\mod_manifest.json') -Raw | ConvertFrom-Json
$version = $manifest.version

if (-not $GameDir) { $GameDir = $env:STS2_GAME_DIR }
if (-not $GameDir) {
    foreach ($key in 'HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam') {
        try {
            $steam = (Get-ItemProperty -Path $key -ErrorAction Stop)
            $steamRoot = if ($steam.SteamPath) { $steam.SteamPath } else { $steam.InstallPath }
            $c = Join-Path $steamRoot 'steamapps\common\Slay the Spire 2'
            if (Test-Path -LiteralPath (Join-Path $c 'SlayTheSpire2.exe')) { $GameDir = $c; break }
        } catch { }
    }
}
if (-not $GameDir) { $GameDir = (Read-Host 'Slay the Spire 2 folder (contains SlayTheSpire2.exe)').Trim('"') }

dotnet build (Join-Path $root 'src') -c Release "-p:GameDir=$GameDir" | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

$name = "StsTierOverlay-v$version"
$stage = Join-Path $root "out\$name"
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
$modDir = Join-Path $stage 'StsTierOverlay'
New-Item -ItemType Directory -Force -Path $modDir | Out-Null

Copy-Item (Join-Path $root 'src\bin\Release\net8.0\StsTierOverlay.dll') $modDir
Copy-Item (Join-Path $root 'src\mod_manifest.json') $modDir
# .txt on purpose: the game treats every *.json in a mod folder as a mod manifest.
Copy-Item (Join-Path $root 'data\tiers.json') (Join-Path $modDir 'tiers.txt')
Copy-Item (Join-Path $root 'installer\*') $stage

$zip = Join-Path $root "out\$name.zip"
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path $stage -DestinationPath $zip
Write-Host "Package: $zip" -ForegroundColor Green
