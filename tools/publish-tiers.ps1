# Publishes tier lists saved by tools/extract-tiers.js to data/tiers.json and pushes them to GitHub.
# Every player's mod downloads the result the next time they start the game.
#
#   .\tools\publish-tiers.ps1                       # newest sts2-cards*.json and sts2-relics*.json in Downloads
#   .\tools\publish-tiers.ps1 -Path a.json, b.json  # specific files
#   .\tools\publish-tiers.ps1 -Yes                  # no confirmation prompt
#   .\tools\publish-tiers.ps1 -NoPush               # update and commit, but do not push
param(
    [string[]]$Path,
    [switch]$Yes,
    [switch]$NoPush
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$target = Join-Path $repo 'data\tiers.json'
$minCount = @{ cards = 300; relics = 100 }

function Get-DownloadsDir {
    try {
        $p = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders' -ErrorAction Stop).'{374DE290-123F-4565-9164-39C4925E467B}'
        if ($p) { return [Environment]::ExpandEnvironmentVariables($p) }
    } catch { }
    return Join-Path $env:USERPROFILE 'Downloads'
}

# Flattens any nesting into @{ slug = "Group/Tier" } (cards: "Ironclad/S", relics: "S").
function Get-Map($node, [string]$prefix = '') {
    $map = @{}
    if ($null -eq $node) { return $map }
    foreach ($p in $node.PSObject.Properties) {
        if ($p.Name.StartsWith('_')) { continue }
        $label = if ($prefix) { "$prefix/$($p.Name)" } else { $p.Name }
        if ($p.Value -is [System.Array]) {
            foreach ($slug in $p.Value) { if (-not $map.ContainsKey($slug)) { $map[$slug] = $label } }
        } else {
            $inner = Get-Map $p.Value $label
            foreach ($k in $inner.Keys) { if (-not $map.ContainsKey($k)) { $map[$k] = $inner[$k] } }
        }
    }
    return $map
}

# 1) Pick the files: newest of each kind ("a.json,b.json" also works when called through powershell -File)
$Path = @($Path | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
if (-not $Path) {
    $downloads = Get-DownloadsDir
    $Path = foreach ($kind in 'cards', 'relics') {
        Get-ChildItem -LiteralPath $downloads -Filter "sts2-$kind*.json" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
    }
    if (-not $Path) { throw "No sts2-cards*.json or sts2-relics*.json in $downloads. Run tools/extract-tiers.js on the Mobalytics pages first." }
}

$current = Get-Content -LiteralPath $target -Raw -Encoding UTF8 | ConvertFrom-Json
$sections = [ordered]@{ cards = $current.cards; relics = $current.relics }
$sources = [ordered]@{ cards = $current._sources.cards; relics = $current._sources.relics }
$newest = $current._updated
$summary = @()
$anyChange = $false

# 2) Validate each file and show what changed
foreach ($file in $Path) {
    $in = Get-Content -LiteralPath $file -Raw -Encoding UTF8 | ConvertFrom-Json
    $kind = @('cards', 'relics') | Where-Object { $in.PSObject.Properties.Name -contains $_ } | Select-Object -First 1
    if (-not $kind) { throw "$file has no 'cards' or 'relics' section." }
    if (-not $in._updated) { throw "$file has no _updated date." }

    $new = Get-Map $in.$kind
    $old = Get-Map $sections[$kind]
    if ($new.Count -lt $minCount[$kind]) { throw "Only $($new.Count) $kind in $file (expected more). Did the page finish loading?" }

    $added   = @($new.Keys | Where-Object { -not $old.ContainsKey($_) } | Sort-Object)
    $removed = @($old.Keys | Where-Object { -not $new.ContainsKey($_) } | Sort-Object)
    $moved   = @($new.Keys | Where-Object { $old.ContainsKey($_) -and $old[$_] -ne $new[$_] } | Sort-Object)

    Write-Host ''
    Write-Host "$($kind.ToUpper()) from $(Split-Path -Leaf $file): $($old.Count) -> $($new.Count)"
    if ($added)   { Write-Host "  Added ($($added.Count)):"     -ForegroundColor Green;  $added   | ForEach-Object { Write-Host "    + $_  [$($new[$_])]" } }
    if ($removed) { Write-Host "  Removed ($($removed.Count)):" -ForegroundColor Red;    $removed | ForEach-Object { Write-Host "    - $_  [$($old[$_])]" } }
    if ($moved)   { Write-Host "  Moved ($($moved.Count)):"     -ForegroundColor Yellow; $moved   | ForEach-Object { Write-Host "    ~ $_  $($old[$_]) -> $($new[$_])" } }

    if ($added -or $removed -or $moved) {
        $anyChange = $true
        $sections[$kind] = $in.$kind
        if ($in._source) { $sources[$kind] = $in._source }
        if ([string]::CompareOrdinal($in._updated, $newest) -gt 0) { $newest = $in._updated }
        $summary += "$kind +$($added.Count)/-$($removed.Count)/~$($moved.Count)"
    } else {
        Write-Host '  No changes.' -ForegroundColor Cyan
    }
}

if (-not $anyChange) { Write-Host "`nNothing to publish." -ForegroundColor Cyan; exit 0 }
if (-not $Yes) {
    $answer = Read-Host "`nPublish these changes to GitHub? (y/N)"
    if ($answer -notmatch '^(y|yes)$') { Write-Host 'Cancelled.'; exit 1 }
}

# 3) Write data/tiers.json, keeping the repo's screen patterns
$out = [ordered]@{
    _updated = $newest
    _sources = $sources
    _screens = $current._screens
    cards    = $sections.cards
    relics   = $sections.relics
}
[IO.File]::WriteAllText($target, ($out | ConvertTo-Json -Depth 8) + "`n", (New-Object Text.UTF8Encoding $false))

# 4) Commit and push
$message = "Update tier lists from Mobalytics ($newest; $($summary -join ', '))"
git -C $repo add data/tiers.json
git -C $repo commit -q -m $message
if ($LASTEXITCODE -ne 0) { throw 'git commit failed' }

if ($NoPush) { Write-Host 'Committed. Not pushed (-NoPush).' -ForegroundColor Yellow; exit 0 }
git -C $repo push -q
if ($LASTEXITCODE -ne 0) {
    # Fallback for machines where plain git push has no credentials but the GitHub CLI is logged in.
    git -C $repo -c credential.helper= -c 'credential.helper=!gh auth git-credential' push -q
    if ($LASTEXITCODE -ne 0) { throw 'git push failed' }
}
Write-Host "Published: $($summary -join ', '). Players get it the next time they start the game." -ForegroundColor Green
