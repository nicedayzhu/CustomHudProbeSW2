param(
    [string]$GameInfoPath = "F:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo\gameinfo.gi"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if (-not (Test-Path -LiteralPath $GameInfoPath -PathType Leaf)) {
    throw "gameinfo.gi not found: $GameInfoPath"
}

$backupPath = "$GameInfoPath.swift_custom_hud_layout_probe.vpkdirs.bak"
if (-not (Test-Path -LiteralPath $backupPath)) {
    Copy-Item -LiteralPath $GameInfoPath -Destination $backupPath
}

$lines = Get-Content -LiteralPath $GameInfoPath
$requiredIncludes = @(
    "`t`t`t`t`"include`"       `"panorama/layout/custom_game`"",
    "`t`t`t`t`"include`"       `"panorama/styles/custom_game`""
)

$missingIncludes = @()
foreach ($include in $requiredIncludes) {
    $path = ($include -replace '.*"([^"]+)"$', '$1')
    if (-not ($lines | Where-Object { $_ -match '^\s*"include"\s+"' + [regex]::Escape($path) + '"\s*$' })) {
        $missingIncludes += $include
    }
}

if ($missingIncludes.Count -eq 0) {
    Write-Host "Custom HUD Panorama VpkDirectories are already enabled."
    Write-Host "Backup: $backupPath"
    exit 0
}

$inserted = $false
$updated = foreach ($line in $lines) {
    $line
    if (-not $inserted -and $line -match '^\s*"include"\s+"panorama/images/map_icons"\s*$') {
        foreach ($include in $missingIncludes) {
            $include
        }
        $inserted = $true
    }
}

if (-not $inserted) {
    throw "Could not find panorama/images/map_icons include in AddonConfig/VpkDirectories."
}

Set-Content -LiteralPath $GameInfoPath -Value $updated
Write-Host "Enabled Custom HUD Panorama VpkDirectories:"
foreach ($include in $missingIncludes) {
    Write-Host "  $include"
}
Write-Host "Backup: $backupPath"
