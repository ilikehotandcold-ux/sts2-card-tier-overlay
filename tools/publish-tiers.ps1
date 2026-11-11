# Publishes a tier list saved by tools/extract-tiers.js to data/tiers.json and pushes it to GitHub.
# Every player's mod downloads it the next time they start the game.
#
#   .\tools\publish-tiers.ps1                 # newest sts2-tiers*.json in Downloads
#   .\tools\publish-tiers.ps1 -Path file.json # a specific file
#   .\tools\publish-tiers.ps1 -Yes            # no confirmation prompt
#   .\tools\publish-tiers.ps1 -NoPush         # update and commit, but do not push
param(
    [string]$Path,
    [switch]$Yes,
    [switch]$NoPush
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$target = Join-Path $repo 'data\tiers.json'

function Get-DownloadsDir {
    try {
        $p = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders' -ErrorAction Stop).'{374DE290-123F-4565-9164-39C4925E467B}'
        if ($p) { return [Environment]::ExpandEnvironmentVariables($p) }
    } catch { }
    return Join-Path $env:USERPROFILE 'Downloads'
}

# Flattens { "Character": { "S": [cards] } } into @{ card = "Character/S" }.
function Get-CardMap($data) {
    $map = @{}
    foreach ($character in $data.PSObject.Properties) {
        if ($character.Name.StartsWith('_')) { continue }
        foreach ($tier in $character.Value.PSObject.Properties) {
            foreach ($card in $tier.Value) { if (-not $map.ContainsKey($card)) { $map[$card] = "$($character.Name)/$($tier.Name)" } }
        }
    }
    return $map
}

# 1) Pick the file
if (-not $Path) {
    $downloads = Get-DownloadsDir
    $file = Get-ChildItem -LiteralPath $downloads -Filter 'sts2-tiers*.json' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $file) { throw "No sts2-tiers*.json in $downloads. Run tools/extract-tiers.js on the Mobalytics page first." }
    $Path = $file.FullName
}
Write-Host "Source: $Path"

# 2) Validate
$new = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
$old = Get-Content -LiteralPath $target -Raw -Encoding UTF8 | ConvertFrom-Json
$newCards = Get-CardMap $new
$oldCards = Get-CardMap $old
if ($newCards.Count -lt 300) { throw "Only $($newCards.Count) cards in the file (expected ~400+). Did the page finish loading?" }
if (-not $new._updated) { throw 'The file has no _updated date.' }

# 3) Show what changed
$added   = @($newCards.Keys | Where-Object { -not $oldCards.ContainsKey($_) } | Sort-Object)
$removed = @($oldCards.Keys | Where-Object { -not $newCards.ContainsKey($_) } | Sort-Object)
$changed = @($newCards.Keys | Where-Object { $oldCards.ContainsKey($_) -and $oldCards[$_] -ne $newCards[$_] } | Sort-Object)

Write-Host ''
Write-Host "Cards: $($oldCards.Count) -> $($newCards.Count)   (data date $($old._updated) -> $($new._updated))"
if ($added)   { Write-Host "Added ($($added.Count)):"     -ForegroundColor Green;  $added   | ForEach-Object { Write-Host "  + $_  [$($newCards[$_])]" } }
if ($removed) { Write-Host "Removed ($($removed.Count)):" -ForegroundColor Red;    $removed | ForEach-Object { Write-Host "  - $_  [$($oldCards[$_])]" } }
if ($changed) { Write-Host "Moved ($($changed.Count)):"   -ForegroundColor Yellow; $changed | ForEach-Object { Write-Host "  ~ $_  $($oldCards[$_]) -> $($newCards[$_])" } }
if (-not ($added -or $removed -or $changed)) {
    Write-Host 'No tier changes compared to the repo. Nothing to publish.' -ForegroundColor Cyan
    exit 0
}

if (-not $Yes) {
    $answer = Read-Host "`nPublish these changes to GitHub? (y/N)"
    if ($answer -notmatch '^(y|yes)$') { Write-Host 'Cancelled.'; exit 1 }
}

# 4) Write data/tiers.json, keeping the repo's own metadata (screen patterns) and the new date/source
$out = [ordered]@{
    _source  = if ($new._source) { $new._source } else { 'https://mobalytics.gg/slay-the-spire-2/tier-lists/cards' }
    _updated = $new._updated
    _screens = if ($old._screens) { @($old._screens) } else { @('CardReward', 'ChooseACard', 'Merchant') }
}
foreach ($p in $new.PSObject.Properties) { if (-not $p.Name.StartsWith('_')) { $out[$p.Name] = $p.Value } }
$json = $out | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText($target, $json + "`n", (New-Object Text.UTF8Encoding $false))

# 5) Commit and push
$summary = "+$($added.Count) / -$($removed.Count) / ~$($changed.Count)"
git -C $repo add data/tiers.json
git -C $repo commit -q -m "Update tier list from Mobalytics ($($new._updated), $summary)"
if ($LASTEXITCODE -ne 0) { throw 'git commit failed' }

if ($NoPush) { Write-Host 'Committed. Not pushed (-NoPush).' -ForegroundColor Yellow; exit 0 }
git -C $repo push -q
if ($LASTEXITCODE -ne 0) {
    # Fallback for machines where plain git push has no credentials but the GitHub CLI is logged in.
    git -C $repo -c credential.helper= -c 'credential.helper=!gh auth git-credential' push -q
    if ($LASTEXITCODE -ne 0) { throw 'git push failed' }
}
Write-Host "Published ($summary). Players get it the next time they start the game." -ForegroundColor Green
