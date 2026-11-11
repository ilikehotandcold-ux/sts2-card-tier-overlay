# Instalador de Card Tier Overlay para Slay the Spire 2.
# Lo lanzan Instalar.bat / Desinstalar.bat; no hace falta abrirlo a mano.
param([switch]$Uninstall)

$ErrorActionPreference = 'Stop'
$ModId = 'StsTierOverlay'
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path

function Test-GameDir([string]$dir) {
    return $dir -and (Test-Path -LiteralPath (Join-Path $dir 'SlayTheSpire2.exe'))
}

function Find-GameDir {
    $candidates = New-Object System.Collections.Generic.List[string]
    # 1) Junto a este instalador (por si se descomprime dentro o al lado del juego)
    $candidates.Add($Here)
    $candidates.Add((Split-Path -Parent $Here))
    $candidates.Add((Join-Path $Here 'Slay the Spire 2'))
    $candidates.Add((Join-Path (Split-Path -Parent $Here) 'Slay the Spire 2'))

    # 2) Bibliotecas de Steam
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
                $libs += $m.Groups[1].Value -replace '\\', '\'
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
    Write-Host 'No he encontrado Slay the Spire 2 automáticamente.'
    $answer = Read-Host 'Arrastra aquí la carpeta del juego (la que tiene SlayTheSpire2.exe) y pulsa Enter'
    $answer = $answer.Trim().Trim('"')
    if (-not $answer) { Write-Host 'Cancelado.'; exit 1 }
    if ((Test-Path -LiteralPath $answer -PathType Leaf)) { $answer = Split-Path -Parent $answer }
    if (Test-GameDir $answer) { $game = $answer }
    elseif (Test-GameDir (Join-Path $answer 'Slay the Spire 2')) { $game = Join-Path $answer 'Slay the Spire 2' }
    else { Write-Host "Ahí no está SlayTheSpire2.exe: $answer" -ForegroundColor Yellow }
}

Write-Host "Juego: $game"
$target = Join-Path $game "mods\$ModId"

if (Get-Process -Name 'SlayTheSpire2' -ErrorAction SilentlyContinue) {
    Write-Host ''
    Write-Host 'El juego está abierto. Ciérralo y vuelve a ejecutar el instalador.' -ForegroundColor Yellow
    exit 1
}

if ($Uninstall) {
    if (Test-Path -LiteralPath $target) {
        Remove-Item -LiteralPath $target -Recurse -Force
        Write-Host 'Mod desinstalado.' -ForegroundColor Green
    } else {
        Write-Host 'El mod no estaba instalado.'
    }
    exit 0
}

$source = Join-Path $Here $ModId
if (-not (Test-Path -LiteralPath (Join-Path $source "$ModId.dll"))) {
    Write-Host "Falta la carpeta '$ModId' junto al instalador. Descomprime el zip entero." -ForegroundColor Red
    exit 1
}

if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $target -Recurse -Force

Write-Host ''
Write-Host "Instalado en: $target" -ForegroundColor Green
Write-Host 'Abre el juego. Si te pregunta si quieres cargar mods, di que sí.'
