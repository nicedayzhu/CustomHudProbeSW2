param(
    [ValidateSet("Validate", "Compile", "Pack", "Build", "Install")]
    [string]$Action = "Build",
    [string]$Cs2Root = "F:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive",
    [string]$AddonName = "swift_custom_hud_layout_probe",
    [string]$VpkEditCli = "F:\cs2dev\SkinTools\VPKEdit-Windows-Standalone-msvc-Release\vpkeditcli.exe",
    [string]$CsgoPath = "",
    [string]$ServerRoot = "F:\csgoserver_win\cs2"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$sourceRoot = Join-Path $projectRoot "hud"
$layoutPath = Join-Path $sourceRoot "layout\swift_menu_custom_hud.xml"
$cardLayoutPath = Join-Path $sourceRoot "layout\cyber_card_custom_hud.xml"
$galleryLayoutPath = Join-Path $sourceRoot "layout\hover3d_gallery_custom_hud.xml"
$galleryImageRoot = Join-Path $sourceRoot "images\hover3d"
$galleryImagePaths = @(1..3 | ForEach-Object { Join-Path $galleryImageRoot "card_$_.png" })
$galleryTexturePaths = @(1..3 | ForEach-Object { Join-Path $galleryImageRoot "card_$_.vtex" })
$stylePath = Join-Path $sourceRoot "styles\swift_menu_custom_hud.css"
$pluginPath = Join-Path $projectRoot "src\CustomHudProbeSW2.cs"
$bridgePath = Join-Path $projectRoot "src\CustomHudNative.cs"
$gameDataPath = Join-Path $projectRoot "resources\gamedata\signatures.jsonc"
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
    $requiredSources = @($layoutPath, $cardLayoutPath, $galleryLayoutPath, $stylePath, $pluginPath, $bridgePath, $gameDataPath)
    $requiredSources += $galleryImagePaths
    $requiredSources += $galleryTexturePaths
    foreach ($path in $requiredSources) {
        Assert-FileExists -Path $path -Message "Required Custom HUD source is missing: $path"
    }

    [xml]$menuLayout = Get-Content -Raw -LiteralPath $layoutPath
    [xml]$cardLayout = Get-Content -Raw -LiteralPath $cardLayoutPath
    [xml]$galleryLayout = Get-Content -Raw -LiteralPath $galleryLayoutPath
    $allowedAttributes = @{
        "root" = @()
        "styles" = @()
        "include" = @("src")
        "Panel" = @("id", "class", "hittest")
        "Label" = @("id", "class", "hittest", "text")
        "Image" = @("id", "class", "hittest", "src", "texturewidth", "textureheight")
        "Button" = @("id", "class")
    }

    foreach ($layout in @($menuLayout, $cardLayout, $galleryLayout)) {
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
            throw "Each Custom HUD layout must include the compiled custom_game stylesheet."
        }
    }

    if (-not $menuLayout.SelectSingleNode("//Panel[@id='dialog']")) {
        throw "Menu Custom HUD layout is missing the dialog panel."
    }
    if (-not $cardLayout.SelectSingleNode("//Panel[@id='card_dialog']")) {
        throw "Card Custom HUD layout is missing the card_dialog panel."
    }
    if (-not $galleryLayout.SelectSingleNode("//Panel[@id='gallery_dialog']")) {
        throw "Hover 3D gallery layout is missing the gallery_dialog panel."
    }
    if ($cardLayout.SelectSingleNode("/root/scripts") -or $galleryLayout.SelectSingleNode("/root/scripts")) {
        throw "CCSCustomHudLayout validation disallows a scripts node."
    }

    $buttonIds = @($menuLayout.SelectNodes("//Button") | ForEach-Object { $_.GetAttribute("id") })
    foreach ($buttonId in @("swift_menu_primary", "swift_menu_secondary", "swift_menu_close")) {
        if ($buttonIds -notcontains $buttonId) {
            throw "Custom HUD layout is missing button id: $buttonId"
        }
    }
    if ($cardLayout.SelectNodes("//Button").Count -ne 0) {
        throw "Standalone card layout must not contain menu Buttons."
    }
    if ($galleryLayout.SelectNodes("//Button").Count -ne 0) {
        throw "Hover 3D gallery layout must not contain menu Buttons."
    }
    $trackerCount = $cardLayout.SelectNodes("//Panel[contains(concat(' ', normalize-space(@class), ' '), ' cyber-tracker ')]").Count
    if ($trackerCount -ne 25) {
        throw "Standalone card layout must contain exactly 25 hover trackers; found $trackerCount."
    }
    $sensorCount = $cardLayout.SelectNodes("//Panel[contains(concat(' ', normalize-space(@class), ' '), ' cyber-hover-sensor ')]").Count
    if ($sensorCount -ne 25) {
        throw "Standalone card layout must contain exactly 25 hover sensors; found $sensorCount."
    }
    for ($trackerIndex = 1; $trackerIndex -le 25; $trackerIndex++) {
        $tracker = $cardLayout.SelectSingleNode("//Panel[@id='CyberTracker$trackerIndex']")
        if (-not $tracker) {
            throw "Standalone card layout is missing CyberTracker$trackerIndex."
        }
        $directSensors = $tracker.SelectNodes("./Panel[contains(concat(' ', normalize-space(@class), ' '), ' cyber-hover-sensor ')]").Count
        if ($directSensors -ne 1) {
            throw "CyberTracker$trackerIndex must own exactly one hover sensor."
        }
        if ($trackerIndex -lt 25 -and -not $tracker.SelectSingleNode("./Panel[@id='CyberTracker$($trackerIndex + 1)']")) {
            throw "CyberTracker$trackerIndex must directly contain the next tracker in the shared-card chain."
        }
        if ($trackerIndex -eq 25 -and -not $tracker.SelectSingleNode("./Panel[@id='CyberCard']")) {
            throw "CyberTracker25 must directly contain the shared CyberCard panel."
        }
    }
    $cyberCard = $cardLayout.SelectSingleNode("//Panel[@id='CyberCard']")
    if ($cardLayout.SelectNodes("//Panel[@id='CyberCard']").Count -ne 1 -or -not $cyberCard) {
        throw "Standalone card layout must contain exactly one shared CyberCard panel."
    }
    $cardTrackerAncestors = $cyberCard.SelectNodes("ancestor::Panel[contains(concat(' ', normalize-space(@class), ' '), ' cyber-tracker ')]").Count
    if ($cardTrackerAncestors -ne 25) {
        throw "The shared CyberCard must be a descendant of all 25 trackers; found $cardTrackerAncestors ancestors."
    }

    $galleryStageCount = $galleryLayout.SelectNodes("//Panel[contains(concat(' ', normalize-space(@class), ' '), ' hover3d-card-stage ')]").Count
    if ($galleryStageCount -ne 3) {
        throw "Hover 3D gallery must contain exactly 3 card stages; found $galleryStageCount."
    }
    $galleryTrackerCount = $galleryLayout.SelectNodes("//Panel[contains(concat(' ', normalize-space(@class), ' '), ' hover3d-tracker ')]").Count
    if ($galleryTrackerCount -ne 24) {
        throw "Hover 3D gallery must contain exactly 24 trackers; found $galleryTrackerCount."
    }
    $gallerySensorCount = $galleryLayout.SelectNodes("//Panel[contains(concat(' ', normalize-space(@class), ' '), ' hover3d-sensor ')]").Count
    if ($gallerySensorCount -ne 24) {
        throw "Hover 3D gallery must contain exactly 24 sensors; found $gallerySensorCount."
    }
    $galleryContentCount = $galleryLayout.SelectNodes("//Panel[contains(concat(' ', normalize-space(@class), ' '), ' hover3d-card-content ')]").Count
    if ($galleryContentCount -ne 3) {
        throw "Hover 3D gallery must contain exactly 3 shared card-content panels; found $galleryContentCount."
    }
    $gallerySurfaceCount = $galleryLayout.SelectNodes("//Panel[contains(concat(' ', normalize-space(@class), ' '), ' hover3d-card-surface ')]").Count
    if ($gallerySurfaceCount -ne 3) {
        throw "Hover 3D gallery must contain exactly 3 independently scaled card surfaces; found $gallerySurfaceCount."
    }
    $galleryFrameCount = $galleryLayout.SelectNodes("//Panel[contains(concat(' ', normalize-space(@class), ' '), ' hover3d-card-frame ')]").Count
    if ($galleryFrameCount -ne 3) {
        throw "Hover 3D gallery must contain exactly 3 inner clipping frames; found $galleryFrameCount."
    }
    if ($galleryLayout.SelectNodes("//Panel[contains(concat(' ', normalize-space(@class), ' '), ' hover3d-card-shine ')]").Count -ne 3) {
        throw "Hover 3D gallery must contain exactly 3 shine overlays."
    }
    if ($galleryLayout.SelectNodes("//Image[contains(concat(' ', normalize-space(@class), ' '), ' hover3d-card-image ')]").Count -ne 3) {
        throw "Hover 3D gallery must contain exactly 3 card images."
    }
    foreach ($cardIndex in 1..3) {
        foreach ($trackerIndex in 1..8) {
            $trackerId = "Hover3DCard${cardIndex}Tracker${trackerIndex}"
            $tracker = $galleryLayout.SelectSingleNode("//Panel[@id='$trackerId']")
            if (-not $tracker) {
                throw "Hover 3D gallery is missing $trackerId."
            }
            $directSensors = $tracker.SelectNodes("./Panel[contains(concat(' ', normalize-space(@class), ' '), ' hover3d-sensor ')]").Count
            if ($directSensors -ne 1) {
                throw "$trackerId must own exactly one hover sensor."
            }
            if ($trackerIndex -lt 8) {
                $nextTrackerId = "Hover3DCard${cardIndex}Tracker$($trackerIndex + 1)"
                if (-not $tracker.SelectSingleNode("./Panel[@id='$nextTrackerId']")) {
                    throw "$trackerId must directly contain $nextTrackerId."
                }
            }
            else {
                $contentId = "Hover3DCard${cardIndex}Content"
                if (-not $tracker.SelectSingleNode("./Panel[@id='$contentId']")) {
                    throw "$trackerId must directly contain $contentId."
                }
            }
        }

        $contentId = "Hover3DCard${cardIndex}Content"
        $content = $galleryLayout.SelectSingleNode("//Panel[@id='$contentId']")
        if (-not $content -or $content.SelectNodes("ancestor::Panel[contains(concat(' ', normalize-space(@class), ' '), ' hover3d-tracker ')]").Count -ne 8) {
            throw "$contentId must be a descendant of all 8 trackers for its card."
        }
        $surface = $content.SelectSingleNode("./Panel[contains(concat(' ', normalize-space(@class), ' '), ' hover3d-card-surface ')]")
        if (-not $surface -or $content.SelectNodes("./Panel[contains(concat(' ', normalize-space(@class), ' '), ' hover3d-card-surface ')]").Count -ne 1) {
            throw "$contentId must directly own exactly one independently scaled card surface."
        }
        $frame = $surface.SelectSingleNode("./Panel[contains(concat(' ', normalize-space(@class), ' '), ' hover3d-card-frame ')]")
        if (-not $frame -or $surface.SelectNodes("./Panel[contains(concat(' ', normalize-space(@class), ' '), ' hover3d-card-frame ')]").Count -ne 1) {
            throw "$contentId must keep scaling separate from exactly one inner clipping frame."
        }
        $expectedImageSource = "s2r://panorama/images/custom_game/hover3d/card_${cardIndex}.vtex"
        $image = $frame.SelectSingleNode("./Image[contains(concat(' ', normalize-space(@class), ' '), ' hover3d-card-image ')]")
        if (-not $image -or $image.GetAttribute("src") -ne $expectedImageSource) {
            throw "$contentId must reference $expectedImageSource."
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
    foreach ($api in @("SetDialogVariableStringForPlayer", "SetHasClassForPlayer", "SetInputCaptureEnabled", "HookCustomHudClicks", "IGameDataService", "TryGetSignature")) {
        if ($bridgeSource -notmatch [regex]::Escape($api)) {
            throw "Custom HUD native bridge is missing: $api"
        }
    }
    if ($bridgeSource -match [regex]::Escape("GetAddressBySignature")) {
        throw "Custom HUD bridge must resolve signatures through SwiftlyS2 GameData, not IMemoryService."
    }
    $gameDataSource = Get-Content -Raw -LiteralPath $gameDataPath
    foreach ($signatureName in @(
        "CustomHudProbeSW2::SetDialogVariableStringForPlayer",
        "CustomHudProbeSW2::SetHasClassForPlayer",
        "CustomHudProbeSW2::SetInputCaptureEnabled",
        "CustomHudProbeSW2::CustomHudClickedReceiver")) {
        if ($gameDataSource -notmatch [regex]::Escape($signatureName)) {
            throw "Custom HUD GameData is missing signature: $signatureName"
        }
    }
    foreach ($api in @("OnNativeCustomHudClicked", "ProcessNativeCustomHudClick", "CreateEntityByDesignerName<CCSCustomHudLayout>")) {
        if ($pluginSource -notmatch [regex]::Escape($api)) {
            throw "Custom HUD probe does not use required native click API: $api"
        }
    }
    foreach ($galleryBinding in @("GalleryTargetName", "GalleryLayoutResource", "GalleryDialogPanelId", "HudMode.Gallery", "<menu|card|gallery>")) {
        if ($pluginSource -notmatch [regex]::Escape($galleryBinding)) {
            throw "Custom HUD probe is missing Hover 3D gallery routing: $galleryBinding"
        }
    }
    foreach ($forbiddenApi in @("CCSPointScriptEntity", "point_script", "cs_script", "OnCustomHudClicked")) {
        if ($pluginSource -match [regex]::Escape($forbiddenApi) -or $bridgeSource -match [regex]::Escape($forbiddenApi)) {
            throw "The native receiver implementation must not retain a CScript bridge: $forbiddenApi"
        }
    }

    $styleSource = Get-Content -Raw -LiteralPath $stylePath
    if ($styleSource -notmatch [regex]::Escape("#dialog.SwiftHudHidden") -or
        $styleSource -notmatch [regex]::Escape("#gallery_dialog.SwiftHudHidden") -or
        $styleSource -notmatch [regex]::Escape("visibility: collapse")) {
        throw "Custom HUD stylesheet does not provide the non-interactive hidden state."
    }
    if ($styleSource.Contains("~")) {
        throw "Custom HUD stylesheet must not use the unsupported Panorama sibling combinator '~'."
    }
    if ($styleSource -match [regex]::Escape("CyberHovered")) {
        throw "Custom HUD stylesheet must not depend on script-applied CyberHovered classes."
    }
    if ($styleSource -match [regex]::Escape("cyber-card-hover")) {
        throw "Custom HUD stylesheet must not switch between duplicated hover-card trees."
    }
    if ($styleSource -notmatch [regex]::Escape(".cyber-tracker:hover #CyberCard")) {
        throw "Custom HUD stylesheet is missing the shared-card hover selector."
    }
    foreach ($trackerIndex in 1..25) {
        if ($styleSource -notmatch [regex]::Escape("#CyberTracker${trackerIndex}:hover #CyberCard")) {
            throw "Custom HUD stylesheet is missing the tilt selector for CyberTracker$trackerIndex."
        }
    }
    foreach ($directionIndex in 1..8) {
        if ($styleSource -notmatch [regex]::Escape(".hover3d-direction-${directionIndex}:hover .hover3d-card-content")) {
            throw "Custom HUD stylesheet is missing the Hover 3D tilt selector for direction $directionIndex."
        }
        if ($styleSource -notmatch [regex]::Escape(".hover3d-direction-${directionIndex}:hover .hover3d-card-shine")) {
            throw "Custom HUD stylesheet is missing the Hover 3D shine selector for direction $directionIndex."
        }
    }
    if ($styleSource -notmatch "(?s)\.hover3d-card-content\s*\{[^}]*width:\s*272px;[^}]*height:\s*361px;[^}]*overflow:\s*noclip;") {
        throw "Hover 3D transformed content must reserve the full 272x361 stage as an unclipped render surface."
    }
    if ($styleSource -notmatch "(?s)\.hover3d-card-surface\s*\{[^}]*width:\s*240px;[^}]*height:\s*329px;[^}]*horizontal-align:\s*center;[^}]*vertical-align:\s*center;[^}]*overflow:\s*noclip;") {
        throw "Hover 3D scaled surface must remain a centered, unclipped 240x329 card inside the padded render surface."
    }
    if ($styleSource -match "translateZ|scale3d") {
        throw "Custom HUD card tilt must match the reference transform without added translateZ or scale3d motion."
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
    $contentImageDir = Join-Path $paths.ContentAddon "panorama\images\custom_game\hover3d"
    $gameLayoutDir = Join-Path $paths.GameAddon "panorama\layout\custom_game"
    $gameStyleDir = Join-Path $paths.GameAddon "panorama\styles\custom_game"
    $gameImageDir = Join-Path $paths.GameAddon "panorama\images\custom_game\hover3d"
    New-Item -ItemType Directory -Force -Path $contentLayoutDir, $contentStyleDir, $contentImageDir, $gameLayoutDir, $gameStyleDir, $gameImageDir | Out-Null

    Set-Content -LiteralPath (Join-Path $paths.ContentAddon "panorama\preprocessor_config.txt") -Encoding ASCII -Value @'
"PanzipCfg"
{
    "BlockDefs"
    {
    }
}
'@
    Write-TextNoBom -Path (Join-Path $contentLayoutDir "swift_menu_custom_hud.vxml") -Value (Get-Content -Raw -LiteralPath $layoutPath)
    Write-TextNoBom -Path (Join-Path $contentLayoutDir "cyber_card_custom_hud.vxml") -Value (Get-Content -Raw -LiteralPath $cardLayoutPath)
    Write-TextNoBom -Path (Join-Path $contentLayoutDir "hover3d_gallery_custom_hud.vxml") -Value (Get-Content -Raw -LiteralPath $galleryLayoutPath)
    Write-TextNoBom -Path (Join-Path $contentStyleDir "swift_menu_custom_hud.vcss") -Value (Get-Content -Raw -LiteralPath $stylePath)
    foreach ($sourceImage in $galleryImagePaths) {
        Copy-Item -LiteralPath $sourceImage -Destination (Join-Path $contentImageDir (Split-Path -Leaf $sourceImage)) -Force
    }
    foreach ($sourceTexture in $galleryTexturePaths) {
        Copy-Item -LiteralPath $sourceTexture -Destination (Join-Path $contentImageDir (Split-Path -Leaf $sourceTexture)) -Force
    }
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

    & $paths.ResourceCompiler -game $paths.GameDir `
        -i (Join-Path $contentStyleDir "swift_menu_custom_hud.vcss") `
        -i (Join-Path $contentLayoutDir "swift_menu_custom_hud.vxml") `
        -i (Join-Path $contentLayoutDir "cyber_card_custom_hud.vxml") `
        -i (Join-Path $contentLayoutDir "hover3d_gallery_custom_hud.vxml") `
        -i (Join-Path $contentImageDir "card_1.vtex") `
        -i (Join-Path $contentImageDir "card_2.vtex") `
        -i (Join-Path $contentImageDir "card_3.vtex") `
        -f -nop4 -v
    if ($LASTEXITCODE -ne 0) {
        throw "resourcecompiler failed with exit code $LASTEXITCODE"
    }

    $expectedOutputs = @(
        (Join-Path $gameLayoutDir "swift_menu_custom_hud.vxml_c"),
        (Join-Path $gameLayoutDir "cyber_card_custom_hud.vxml_c"),
        (Join-Path $gameLayoutDir "hover3d_gallery_custom_hud.vxml_c"),
        (Join-Path $gameStyleDir "swift_menu_custom_hud.vcss_c"),
        (Join-Path $gameImageDir "card_1.vtex_c"),
        (Join-Path $gameImageDir "card_2.vtex_c"),
        (Join-Path $gameImageDir "card_3.vtex_c")
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
        foreach ($relativePath in @(
            "addoninfo.txt",
            "panorama\layout\custom_game\swift_menu_custom_hud.vxml_c",
            "panorama\layout\custom_game\cyber_card_custom_hud.vxml_c",
            "panorama\layout\custom_game\hover3d_gallery_custom_hud.vxml_c",
            "panorama\styles\custom_game\swift_menu_custom_hud.vcss_c",
            "panorama\images\custom_game\hover3d\card_1.vtex_c",
            "panorama\images\custom_game\hover3d\card_2.vtex_c",
            "panorama\images\custom_game\hover3d\card_3.vtex_c")) {
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
        foreach ($fileName in @(
            "addoninfo.txt",
            "swift_menu_custom_hud.vxml_c",
            "cyber_card_custom_hud.vxml_c",
            "hover3d_gallery_custom_hud.vxml_c",
            "swift_menu_custom_hud.vcss_c",
            "card_1.vtex_c",
            "card_2.vtex_c",
            "card_3.vtex_c")) {
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

function Install-HudOverrideTarget {
    param(
        [string]$TargetCsgoPath,
        [string]$TargetLabel
    )

    Assert-FileExists -Path $outVpk -Message "Missing VPK: $outVpk. Run -Action Build first."
    Assert-DirectoryExists -Path $TargetCsgoPath -Message "$TargetLabel csgo directory not found: $TargetCsgoPath"
    $gameInfoPath = Join-Path $TargetCsgoPath "gameinfo.gi"
    Assert-FileExists -Path $gameInfoPath -Message "Missing $TargetLabel gameinfo.gi: $gameInfoPath"

    $targetName = "$AddonName.vpk"
    $overrideDir = Join-Path $TargetCsgoPath "overrides"
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

    $sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $outVpk).Hash
    $targetHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $targetVpk).Hash
    if ($sourceHash -ne $targetHash) {
        throw "$TargetLabel VPK hash mismatch after installation: $targetVpk"
    }

    Write-Host "Installed $TargetLabel Custom HUD override: $targetVpk"
    Write-Host "$TargetLabel SHA-256: $targetHash"
    Write-Host "Mounted by: $searchPathLine"
    Write-Host "Backup: $backupPath"
}

function Install-HudOverride {
    Install-HudOverrideTarget -TargetCsgoPath $CsgoPath -TargetLabel "client"

    if (-not [string]::IsNullOrWhiteSpace($ServerRoot)) {
        $serverCsgoPath = Join-Path $ServerRoot "game\csgo"
        $resolvedClient = (Resolve-Path -LiteralPath $CsgoPath).Path.TrimEnd('\', '/')
        $resolvedServer = (Resolve-Path -LiteralPath $serverCsgoPath).Path.TrimEnd('\', '/')
        if (-not $resolvedClient.Equals($resolvedServer, [System.StringComparison]::OrdinalIgnoreCase)) {
            Install-HudOverrideTarget -TargetCsgoPath $serverCsgoPath -TargetLabel "server"
        }
    }
}

switch ($Action) {
    "Validate" { Test-HudSources }
    "Compile" { Compile-HudResources }
    "Pack" { Pack-HudVpk }
    "Build" { Compile-HudResources; Pack-HudVpk }
    "Install" { Install-HudOverride }
}
