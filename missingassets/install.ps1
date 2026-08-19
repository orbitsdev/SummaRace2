# SummaRace - install generated art from missingassets/dropin into Assets/
#
# Copies each file in dropin/ over the SAME filename already in the project.
# It never creates a new asset and never touches a .meta file, because the .meta
# holds the GUID and the "Texture Type = Sprite" setting the game depends on.
#
# Run from anywhere:  powershell -ExecutionPolicy Bypass -File missingassets/install.ps1
# Add -WhatIf to see what it would do without copying.

param([switch]$WhatIf)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

$map = @{
    'Stories_Art' = 'Assets\_Game\Resources\Stories\Art'
    'UI'          = 'Assets\_Game\Resources\UI'
}

$installed = 0; $skipped = 0; $rejected = 0

foreach ($folder in $map.Keys) {
    $src = Join-Path $repo "missingassets\dropin\$folder"
    $dst = Join-Path $repo $map[$folder]
    if (-not (Test-Path $src)) { continue }
    if (-not (Test-Path $dst)) { Write-Host "MISSING TARGET $dst" -ForegroundColor Red; continue }

    Get-ChildItem $src -File | Where-Object { $_.Extension -eq '.png' } | ForEach-Object {
        $target = Join-Path $dst $_.Name
        if (-not (Test-Path $target)) {
            # New filename = nothing in the game asks for it. Almost always a typo.
            Write-Host "REJECTED  $($_.Name) - no file of that name exists in $($map[$folder])" -ForegroundColor Red
            $script:rejected++
            return
        }
        if (-not (Test-Path "$target.meta")) {
            Write-Host "REJECTED  $($_.Name) - its .png.meta is missing, do not overwrite" -ForegroundColor Red
            $script:rejected++
            return
        }
        if ($WhatIf) {
            Write-Host "would copy $($_.Name) -> $($map[$folder])" -ForegroundColor Yellow
            $script:skipped++
        } else {
            Copy-Item $_.FullName $target -Force
            Write-Host "installed $($_.Name)" -ForegroundColor Green
            $script:installed++
        }
    }
}

Write-Host ""
Write-Host "installed $installed - planned $skipped - rejected $rejected"
if ($rejected -gt 0) {
    Write-Host "A rejected file is a filename the game never asks for. Check it against" -ForegroundColor Yellow
    Write-Host "missingassets/CHECKLIST.md and rename it, do not force it in." -ForegroundColor Yellow
}
if ($installed -gt 0) {
    Write-Host "Now switch to Unity and let it reimport (it happens on focus)." -ForegroundColor Cyan
}
