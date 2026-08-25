param(
    [Parameter(Mandatory = $true)]
    [string]$ServerRoot
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $projectRoot "CustomHudProbeSW2.csproj"
$publishDirectory = Join-Path $projectRoot "build\publish\CustomHudProbeSW2"
$targetDirectory = Join-Path $ServerRoot "game\csgo\addons\swiftlys2\plugins\CustomHudProbeSW2"

if (-not (Test-Path -LiteralPath (Join-Path $ServerRoot "game\csgo"))) {
    throw "ServerRoot does not look like a CS2 server root: $ServerRoot"
}

& dotnet publish $projectFile -c Release
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

New-Item -ItemType Directory -Force -Path $targetDirectory | Out-Null
Copy-Item -Path (Join-Path $publishDirectory "*") -Destination $targetDirectory -Recurse -Force

Write-Host "CustomHudProbeSW2 deployed to: $targetDirectory"
Write-Host "This script intentionally does not mount the HUD resource VPK or edit gameinfo.gi."
