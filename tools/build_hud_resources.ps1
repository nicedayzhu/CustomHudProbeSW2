param(
    [ValidateSet("Validate", "Compile", "Pack", "Build", "Install")]
    [string]$Action = "Build",
    [string]$Cs2Root = "F:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive",
    [string]$AddonName = "swift_custom_hud_layout_probe",
    [string]$VpkEditCli = "F:\cs2dev\SkinTools\VPKEdit-Windows-Standalone-msvc-Release\vpkeditcli.exe",
    [string]$CsgoPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$sourceRoot = Join-Path $projectRoot "hud"
$layoutPath = Join-Path $sourceRoot "layout\swift_menu_custom_hud.xml"
$stylePath = Join-Path $sourceRoot "styles\swift_menu_custom_hud.css"
$pluginPath = Join-Path $projectRoot "src\CustomHudProbeSW2.cs"
$bridgePath = Join-Path $projectRoot "src\CustomHudNative.cs"
$distRoot = Join-Path $projectRoot "dist"
$outVpk = Join-Path $distRoot "$AddonName.vpk"

if ([string]::IsNullOrWhiteSpace($CsgoPath)) {
    $CsgoPath = Join-Path $Cs2Root "game\csgo"
}

function Assert-FileExists {
    param([string]$Path, [string]$Message)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw $Message
    }
}

function Assert-DirectoryExists {
    param([string]$Path, [string]$Message)

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw $Message
    }
}

function Assert-SafeAddonName {
    param([string]$Name)

    if ([string]::IsNullOrWhiteSpace($Name) -or $Name -match '[\\/:*?"<>|]') {
        throw "AddonName must be a simple directory name: $Name"
    }
}

function Test-PathIsChild {
    param([string]$Path, [string]$Parent)

    $resolvedPath = (Resolve-Path -LiteralPath $Path).Path.TrimEnd('\', '/')
    $resolvedParent = (Resolve-Path -LiteralPath $Parent).Path.TrimEnd('\', '/')
    return $resolvedPath.StartsWith($resolvedParent + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -or
        $resolvedPath.StartsWith($resolvedParent + [System.IO.Path]::AltDirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)
}

function Remove-ChildDirectory {
    param([string]$Path, [string]$Parent)

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }
    if (-not (Test-PathIsChild -Path $Path -Parent $Parent)) {
        throw "Refusing to remove path outside expected parent: $((Resolve-Path -LiteralPath $Path).Path)"
    }

    Get-ChildItem -LiteralPath $Path -Recurse -Force | ForEach-Object {
        $_.Attributes = [System.IO.FileAttributes]::Normal
    }
    Remove-Item -LiteralPath (Resolve-Path -LiteralPath $Path).Path -Recurse -Force
}

function Write-TextNoBom {
    param([string]$Path, [string]$Value)

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Value, $encoding)
}

function Test-HudSources {
    foreach ($path in @($layoutPath, $stylePath, $pluginPath, $bridgePath)) {
        Assert-FileExists -Path $path -Message "Required Custom HUD source is missing: $path"
    }

    [xml]$layout = Get-Content -Raw -LiteralPath $layoutPath
    $allowedAttributes = @{
        "root" = @()
        "styles" = @()
        "include" = @("src")
        "Panel" = @("id", "class", "hittest")
        "Label" = @("id", "class", "hittest", "text")
        "Image" = @("id", "class", "hittest", "src", "texturewidth", "textureheight")
        "Button" = @("id", "class")
    }

    foreach ($node in $layout.SelectNodes("//*")) {
        if (-not $allowedAttributes.ContainsKey($node.Name)) {
            throw "Custom HUD layout contains disallowed node: $($node.Name)"
        }
        foreach ($attribute in $node.Attributes) {
            if ($allowedAttributes[$node.Name] -notcontains $attribute.Name) {
                throw "Custom HUD layout contains disallowed attribute '$($attribute.Name)' on <$($node.Name)>"
            }
        }
    }

    $stylesheet = $layout.SelectSingleNode("/root/styles/include")
    if (-not $stylesheet -or $stylesheet.GetAttribute("src") -ne "s2r://panorama/styles/custom_game/swift_menu_custom_hud.vcss_c") {
        throw "Custom HUD layout must include the compiled custom_game stylesheet."
    }
    if (-not $layout.SelectSingleNode("//Panel[@id='dialog']")) {
        throw "Custom HUD layout is missing the native-state dialog panel."
    }

    $buttonIds = @($layout.SelectNodes("//Button") | ForEach-Object { $_.GetAttribute("id") })
    foreach ($buttonId in @("swift_menu_primary", "swift_menu_secondary", "swift_menu_close")) {
        if ($buttonIds -notcontains $buttonId) {
            throw "Custom HUD layout is missing button id: $buttonId"
        }
    }

    $layoutSource = Get-Content -Raw -LiteralPath $layoutPath
    foreach ($variableName in @("kicker", "title", "status", "primary-action", "secondary-action", "close-action")) {
        if ($layoutSource -notmatch [regex]::Escape("{s:$variableName}")) {
            throw "Custom HUD layout does not bind dialog variable: $variableName"
        }
    }

    $pluginSource = Get-Content -Raw -LiteralPath $pluginPath
    $bridgeSource = Get-Content -Raw -LiteralPath $bridgePath
    foreach ($api in @("SetDialogVariableStringForPlayer", "SetHasClassForPlayer", "SetInputCaptureEnabled", "HookCustomHudClicks", "CustomHudClickedReceiverSignature")) {
        if ($bridgeSource -notmatch [regex]::Escape($api)) {
            throw "Custom HUD native bridge is missing: $api"
        }
    }
    foreach ($api in @("OnNativeCustomHudClicked", "ProcessNativeCustomHudClick", "CreateEntityByDesignerName<CCSCustomHudLayout>")) {
        if ($pluginSource -notmatch [regex]::Escape($api)) {
            throw "Custom HUD probe does not use required native click API: $api"
        }
    }
    foreach ($forbiddenApi in @("CCSPointScriptEntity", "point_script", "cs_script", "OnCustomHudClicked")) {
        if ($pluginSource -match [regex]::Escape($forbiddenApi) -or $bridgeSource -match [regex]::Escape($forbiddenApi)) {
            throw "The native receiver implementation must not retain a CScript bridge: $forbiddenApi"
        }
    }

    $styleSource = Get-Content -Raw -LiteralPath $stylePath
    if ($styleSource -notmatch [regex]::Escape("#dialog.SwiftHudHidden") -or $styleSource -notmatch [regex]::Escape("visibility: collapse")) {
        throw "Custom HUD stylesheet does not provide the non-interactive hidden state."
    }

    Write-Host "Custom HUD source validation passed."
    Write-Host "Verified buttons: $($buttonIds -join ', ')"
}

function Get-AddonPaths {
    Assert-SafeAddonName -Name $AddonName

    $contentAddonsRoot = Join-Path $Cs2Root "content\csgo_addons"
    $gameAddonsRoot = Join-Path $Cs2Root "game\csgo_addons"
    return [pscustomobject]@{
        ContentAddonsRoot = $contentAddonsRoot
        GameAddonsRoot = $gameAddonsRoot
        ContentAddon = Join-Path $contentAddonsRoot $AddonName
        GameAddon = Join-Path $gameAddonsRoot $AddonName
        GameDir = Join-Path $Cs2Root "game\csgo"
        ResourceCompiler = Join-Path $Cs2Root "game\bin\win64\resourcecompiler.exe"
    }
}

function Compile-HudResources {
    Test-HudSources
    $paths = Get-AddonPaths
    Assert-FileExists -Path $paths.ResourceCompiler -Message "resourcecompiler.exe not found: $($paths.ResourceCompiler)"

    New-Item -ItemType Directory -Force -Path $paths.ContentAddonsRoot, $paths.GameAddonsRoot | Out-Null
    Remove-ChildDirectory -Path $paths.ContentAddon -Parent $paths.ContentAddonsRoot
    Remove-ChildDirectory -Path $paths.GameAddon -Parent $paths.GameAddonsRoot

    $contentLayoutDir = Join-Path $paths.ContentAddon "panorama\layout\custom_game"
    $contentStyleDir = Join-Path $paths.ContentAddon "panorama\styles\custom_game"
    $gameLayoutDir = Join-Path $paths.GameAddon "panorama\layout\custom_game"
    $gameStyleDir = Join-Path $paths.GameAddon "panorama\styles\custom_game"
    New-Item -ItemType Directory -Force -Path $contentLayoutDir, $contentStyleDir, $gameLayoutDir, $gameStyleDir | Out-Null

    Set-Content -LiteralPath (Join-Path $paths.ContentAddon "panorama\preprocessor_config.txt") -Encoding ASCII -Value @'
"PanzipCfg"
{
    "BlockDefs"
    {
    }
}
'@
    Write-TextNoBom -Path (Join-Path $contentLayoutDir "swift_menu_custom_hud.vxml") -Value (Get-Content -Raw -LiteralPath $layoutPath)
    Write-TextNoBom -Path (Join-Path $contentStyleDir "swift_menu_custom_hud.vcss") -Value (Get-Content -Raw -LiteralPath $stylePath)
    Set-Content -LiteralPath (Join-Path $paths.ContentAddon "addoninfo.txt") -Encoding ASCII -Value @'
<!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} format:generic:version{7412167c-06e9-4698-aff2-e63eb59037e7} -->
{
    IsPlayable = false
    Panorama =
    {
        AllowCustomGameUI = true
        AddonLayoutPath = "panorama/layout/custom_game/"
    }
}
'@

    & $paths.ResourceCompiler -game $paths.GameDir -i (Join-Path $contentStyleDir "swift_menu_custom_hud.vcss") -i (Join-Path $contentLayoutDir "swift_menu_custom_hud.vxml") -f -nop4 -v
    if ($LASTEXITCODE -ne 0) {
        throw "resourcecompiler failed with exit code $LASTEXITCODE"
    }

    $expectedOutputs = @(
        (Join-Path $gameLayoutDir "swift_menu_custom_hud.vxml_c"),
        (Join-Path $gameStyleDir "swift_menu_custom_hud.vcss_c")
    )
    foreach ($output in $expectedOutputs) {
        Assert-FileExists -Path $output -Message "Expected compiled resource not found: $output"
    }

    $strippedDir = Join-Path $paths.GameAddon "panorama_stripped"
    Remove-ChildDirectory -Path $strippedDir -Parent $paths.GameAddon
    Copy-Item -LiteralPath (Join-Path $paths.ContentAddon "addoninfo.txt") -Destination (Join-Path $paths.GameAddon "addoninfo.txt") -Force

    Write-Host "Compiled Custom HUD resources: $($paths.GameAddon)"
}

function Pack-HudVpk {
    $paths = Get-AddonPaths
    Assert-DirectoryExists -Path $paths.GameAddon -Message "Compiled Custom HUD addon not found: $($paths.GameAddon). Run -Action Compile first."
    Assert-FileExists -Path $VpkEditCli -Message "VPKEdit CLI not found: $VpkEditCli"

    New-Item -ItemType Directory -Force -Path $distRoot | Out-Null
    $stageRoot = Join-Path $distRoot ("hud_vpk_stage_" + [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())
    $stageAddon = Join-Path $stageRoot $AddonName
    try {
        New-Item -ItemType Directory -Force -Path $stageRoot | Out-Null
        Copy-Item -LiteralPath $paths.GameAddon -Destination $stageAddon -Recurse -Force
        foreach ($relativePath in @("addoninfo.txt", "panorama\layout\custom_game\swift_menu_custom_hud.vxml_c", "panorama\styles\custom_game\swift_menu_custom_hud.vcss_c")) {
            Assert-FileExists -Path (Join-Path $stageAddon $relativePath) -Message "VPK staging is missing: $relativePath"
        }
        if (Test-Path -LiteralPath (Join-Path $stageAddon "maps")) {
            throw "VPK staging must not contain a maps root."
        }

        & $VpkEditCli --output $outVpk --type vpk --version 2 --single-file $stageAddon
        if ($LASTEXITCODE -ne 0) {
            throw "vpkeditcli failed with exit code $LASTEXITCODE"
        }
        Assert-FileExists -Path $outVpk -Message "Expected VPK was not created: $outVpk"

        $vpkTree = (& $VpkEditCli --file-tree $outVpk | Out-String)
        if ($LASTEXITCODE -ne 0) {
            throw "vpkeditcli file-tree failed with exit code $LASTEXITCODE"
        }
        foreach ($fileName in @("addoninfo.txt", "swift_menu_custom_hud.vxml_c", "swift_menu_custom_hud.vcss_c")) {
            if ($vpkTree -notmatch [regex]::Escape($fileName)) {
                throw "Packed VPK is missing: $fileName"
            }
        }
        if ($vpkTree -match "(?im)\bmaps\b") {
            throw "Packed VPK unexpectedly contains a maps root."
        }
    }
    finally {
        Remove-ChildDirectory -Path $stageRoot -Parent $distRoot
    }

    Write-Host "Built Custom HUD VPK: $outVpk"
}

function Install-HudOverride {
    Assert-FileExists -Path $outVpk -Message "Missing VPK: $outVpk. Run -Action Build first."
    $gameInfoPath = Join-Path $CsgoPath "gameinfo.gi"
    Assert-FileExists -Path $gameInfoPath -Message "Missing gameinfo.gi: $gameInfoPath"

    $targetName = "$AddonName.vpk"
    $overrideDir = Join-Path $CsgoPath "overrides"
    $targetVpk = Join-Path $overrideDir $targetName
    $searchPathLine = "`t`t`tGame`tcsgo/overrides/$targetName"
    $backupPath = "$gameInfoPath.$AddonName.bak"

    New-Item -ItemType Directory -Force -Path $overrideDir | Out-Null
    Copy-Item -LiteralPath $outVpk -Destination $targetVpk -Force
    if (-not (Test-Path -LiteralPath $backupPath)) {
        Copy-Item -LiteralPath $gameInfoPath -Destination $backupPath
    }

    $content = Get-Content -LiteralPath $gameInfoPath
    if ($content -notcontains $searchPathLine) {
        $inserted = $false
        $content = foreach ($line in $content) {
            if (-not $inserted -and $line -match '^\s*Game\s+csgo\s*$') {
                $searchPathLine
                $inserted = $true
            }
            $line
        }
        if (-not $inserted) {
            throw "Could not find 'Game csgo' SearchPath in gameinfo.gi"
        }
        Set-Content -LiteralPath $gameInfoPath -Value $content
    }

    Write-Host "Installed Custom HUD override: $targetVpk"
    Write-Host "Mounted by: $searchPathLine"
    Write-Host "Backup: $backupPath"
}

switch ($Action) {
    "Validate" { Test-HudSources }
    "Compile" { Compile-HudResources }
    "Pack" { Pack-HudVpk }
    "Build" { Compile-HudResources; Pack-HudVpk }
    "Install" { Install-HudOverride }
}
