# CustomHudProbeSW2 Development Guide

[中文开发文档](DEVELOPMENT_CN.md) · [Project overview](../README.md)

This guide covers the technical workflow for the standalone SwiftlyS2 plugin and
the companion HUD-resource Workshop item.

## Architecture and responsibilities

There are two separately deployed artifacts:

- **CustomHudProbeSW2 plugin**: runs on the SwiftlyS2 server and dynamically
  creates `custom_hud_layout` when `!chud_spawn` is used.
- **HUD resource Workshop item**: supplies the Panorama resource required by that
  entity on clients.

Publishing the Workshop item does not deploy the plugin. Deploying the plugin does
not distribute the HUD resource to subscribers.

## Build and deploy the plugin

Requirements:

- .NET 10 SDK;
- SwiftlyS2 runtime compatible with the target server;
- the companion `swift_menu_poc` repository beside this repository.

Build and deploy the plugin from the repository root:

```powershell
.\build_and_deploy.ps1 -ServerRoot F:\csgoserver_win\cs2
```

It publishes the plugin to:

```text
<ServerRoot>\game\csgo\addons\swiftlys2\plugins\CustomHudProbeSW2\
```

The script intentionally does not mount a VPK or modify any `gameinfo.gi` file.

## Build the HUD resource package

From this repository, enter the sibling `swift_menu_poc` repository:

```powershell
Set-Location ..\swift_menu_poc
.\swift-menu.ps1 -Action CustomHudValidate
.\swift-menu.ps1 -Action CustomHudBuild
```

This creates the uploadable Addon `swift_custom_hud_layout_probe` and its local
VPK. Workshop Manager consumes the compiled Addon output; it does not compile raw
Panorama sources for you.

## Publish to the Steam Workshop

### Create the Workshop item

1. Start **Counter-Strike 2 Workshop Manager** from CS2 Workshop Tools.
2. Click **New**.
3. Select `swift_custom_hud_layout_probe` as the addon folder.
4. Inspect the contents preview. It must show at least `[vxml_c]: 1 Files` and
   `[vcss_c]: 1 Files`.
5. Enter title, description, preview image, and visibility, then submit.

The preview in step 4 is the decisive check. When those two compiled resources are
listed, Workshop Manager has found the HUD content. Do not manually select or
upload a `.vpk` file in this dialog.

### Configure `VpkDirectories` when the contents are missing

Close Workshop Manager first; it reads its configuration when it starts. The default
client configuration file is:

```text
F:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo\gameinfo.gi
```

From `swift_menu_poc`, run:

```powershell
.\powershell\experiments\enable_custom_game_panorama_vpkdirs.ps1
```

For a CS2 installation in another Steam library, pass the actual path:

```powershell
.\powershell\experiments\enable_custom_game_panorama_vpkdirs.ps1 `
  -GameInfoPath "D:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive\game\csgo\gameinfo.gi"
```

On its first run, the script creates this backup beside the original file:

```text
gameinfo.gi.swift_custom_game_probe.bak
```

It finds the following existing entry inside `AddonConfig` → `VpkDirectories`:

```text
"include"       "panorama/images/map_icons"
```

It appends only missing entries immediately after it. This is a simplified result;
all other existing `include` entries remain unchanged:

```text
"VpkDirectories"
{
    // Existing entries are unchanged.
    "include"       "panorama/images/map_icons"
    "include"       "panorama/layout/custom_game"
    "include"       "panorama/styles/custom_game"
    "include"       "panorama/scripts/custom_game"
}
```

The script is idempotent: existing entries are reported but not duplicated. To
revert, close Workshop Manager, restore the `.bak` file as `gameinfo.gi`, and then
reopen the tool.

`AddonConfig/VpkDirectories` controls the files that Workshop Tools collects. It
is different from `FileSystem/SearchPaths`, which mounts VPKs for local
server/client tests.

## Test after publishing

1. Connect a clean client subscribed to the Workshop item to the test server.
2. Confirm the server has `CustomHudProbeSW2` deployed.
3. Run `!chud_spawn` and confirm the HUD appears.
4. Run `!chud_status` and confirm an active entity is reported.
5. Run `!chud_clear` and confirm the HUD disappears.

If the entity is created but no HUD appears, first confirm that the resource VPK is
available to both server and client, then preserve the output of
`dev_report_info_hud_layout` for diagnosis.

## Experimental limitations

`custom_hud_layout` is newly introduced and experimental. The local VPK path and
the Workshop Manager preview are verified, but an actual Workshop upload should be
tested separately for subscription download, server resource delivery, and future
CS2 updates. Per-player dialog state and click callbacks additionally require
SwiftlyS2 bindings that do not exist yet.
