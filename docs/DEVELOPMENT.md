# CustomHudProbeSW2 Development Guide

[中文开发文档](DEVELOPMENT_CN.md) · [Project overview](../README.md)

This guide covers the technical workflow for the standalone SwiftlyS2 plugin and
the companion HUD-resource Workshop item.

The HUD source files and all Custom HUD build helpers are owned by this project:
`hud/layout`, `hud/styles`, and `tools/build_hud_resources.ps1`.

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
- a CS2 installation containing `game\bin\win64\resourcecompiler.exe`;
- [VPKEdit CLI](https://github.com/craftablescience/VPKEdit) for VPK packing.

Build and deploy the plugin from the repository root:

```powershell
.\build_and_deploy.ps1 -ServerRoot F:\csgoserver_win\cs2
```

It publishes the plugin to:

```text
<ServerRoot>\game\csgo\addons\swiftlys2\plugins\CustomHudProbeSW2\
```

The script intentionally does not mount a VPK or modify any `gameinfo.gi` file.
Reload the plugin in the server console after deployment, or restart the server:

```text
sw plugins reload CustomHudProbeSW2
```

## Build the HUD resource package

Set paths for the local CS2 installation and VPKEdit executable, then run the
resource tool:

```powershell
$cs2Root = 'F:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive'
$vpkEditCli = 'D:\Tools\VPKEdit\vpkeditcli.exe'

.\tools\build_hud_resources.ps1 -Action Validate
.\tools\build_hud_resources.ps1 -Action Build -Cs2Root $cs2Root -VpkEditCli $vpkEditCli
```

This creates the uploadable Addon `swift_custom_hud_layout_probe` and the local
VPK at `dist\swift_custom_hud_layout_probe.vpk`. Workshop Manager consumes the
compiled Addon output; it does not compile raw Panorama sources for you.

`Validate` checks the layout, stylesheet, plugin integration contract, and required
GameData entries without compiling. `Compile` only runs ResourceCompiler, `Pack`
only writes the VPK from a compiled Addon, and `Build` runs `Compile` then `Pack`.
The default `-Cs2Root` and `-VpkEditCli` values are developer-machine paths, so
pass the paths above on another machine. Use `-AddonName` only when intentionally
building a differently named Addon.

## Local override development test

This route is useful before publishing to Workshop. It changes only the local CS2
installation specified by `-Cs2Root`; deploy the server plugin separately as shown
above. After a successful `Build`, install its VPK:

```powershell
.\tools\build_hud_resources.ps1 -Action Install -Cs2Root $cs2Root
```

`Install` copies `dist\swift_custom_hud_layout_probe.vpk` to
`<Cs2Root>\game\csgo\overrides\` and inserts its VPK search path into
`<Cs2Root>\game\csgo\gameinfo.gi`. The first install creates this backup:

```text
gameinfo.gi.swift_custom_hud_layout_probe.bak
```

Stop the relevant CS2 process before restoring the backup to revert the mount;
remove the copied VPK as well if it is no longer needed. Restart the affected CS2
client/server after installing a new override. `-CsgoPath` can be supplied instead
when the target `game\csgo` directory is not under `-Cs2Root`.

## Publish to the Steam Workshop

### Create the Workshop item

1. Start **Counter-Strike 2 Workshop Manager** from CS2 Workshop Tools.
2. Click **New**.
3. Select `swift_custom_hud_layout_probe` as the addon folder.
4. Inspect the contents preview. It must show at least `[vxml_c]: 4 Files` and
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

From this project, run:

```powershell
.\tools\enable_workshop_vpk_directories.ps1
```

For a CS2 installation in another Steam library, pass the actual path:

```powershell
.\tools\enable_workshop_vpk_directories.ps1 `
  -GameInfoPath "D:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive\game\csgo\gameinfo.gi"
```

On its first run, the script creates this backup beside the original file:

```text
gameinfo.gi.swift_custom_hud_layout_probe.vpkdirs.bak
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
}
```

The script is idempotent: existing entries are reported but not duplicated. To
revert, close Workshop Manager, restore the `.bak` file as `gameinfo.gi`, and then
reopen the tool.

`AddonConfig/VpkDirectories` controls the files that Workshop Tools collects. It
is different from `FileSystem/SearchPaths`, which mounts VPKs for local
server/client tests.

## Distribute the published resource with AddonsManager

For a production server, use the upstream
[SwiftlyS2 AddonsManager](https://github.com/SwiftlyS2-Plugins/AddonsManager) to
download and mount the published Workshop resource rather than relying on the
local-development VPK installation route. This project's Workshop item is:

```text
https://steamcommunity.com/sharedfiles/filedetails/?id=3789924061
```

Install and deploy AddonsManager according to its upstream README. Then open its
configuration, normally located at:

```text
<ServerRoot>\game\csgo\addons\swiftlys2\configs\plugins\AddonsManager\config.jsonc
```

Add this project's Workshop ID to `Main.Addons`, preserving any IDs already in the
file:

```jsonc
{
  "Main": {
    "Addons": [
      "3789924061"
    ]
  }
}
```

Restart or reload AddonsManager after changing its configuration. The upstream
plugin also exposes these server-console commands:

```text
sw_downloadaddon 3789924061  // request a download of this Workshop Addon
sw_searchpath                // list the VPK search paths currently mounted
```

Use `sw_searchpath` to confirm the resource is mounted before running
`!chud_spawn`. The AddonsManager workflow is the intended production route; its
end-to-end delivery should be verified on the target server after the Workshop
item is published or updated.

## In-game command reference

Use these commands in player chat after the plugin has loaded:

| Command | Result |
| --- | --- |
| `!chud_spawn` | Creates one dynamic `custom_hud_layout` probe and opens a menu for each connected human player. |
| `!chud_spawn menu` | Loads the button menu; this is the default mode. |
| `!chud_spawn card` | Loads the standalone 25-zone cyber card. |
| `!chud_spawn gallery` | Loads the three-image Hover 3D gallery. Aliases: `hover3d`, `images`. |
| `!chud_spawn flip` | Loads the two-sided flip card. Aliases: `flipcard`, `turn`. |
| `!chud_open` | Reopens the current HUD for the player who issues it. The probe must already be active. |
| `!chud_close` | Hides the current HUD and releases input capture for the issuing player. |
| `!chud_status` | Reports whether the native bridge is ready and whether a probe is active. |
| `!chud_clear` | Removes the active probe and releases its captured input. |

If `!chud_spawn` reports that the native bridge is unavailable, inspect the server
log before troubleshooting the resource VPK: the plugin intentionally refuses to
spawn on an unverified `server.dll` build.

## Test after publishing

1. Connect a clean client subscribed to the Workshop item to the test server.
2. Confirm the server has `CustomHudProbeSW2` deployed.
3. Run `!chud_spawn menu`, `card`, `gallery`, and `flip`; confirm each HUD replaces
   the previous mode and appears for the connected client.
4. Test menu buttons, both hover-card effects, gallery directions, and the flip
   card's front/back transition; use `!chud_open` after `!chud_close`.
5. Run `!chud_status` and confirm an active entity is reported.
6. Run `!chud_clear` and confirm the HUD disappears.

If the entity is created but no HUD appears, first confirm that the resource VPK is
available to both server and client, then preserve the output of
`dev_report_info_hud_layout` for diagnosis.

## Experimental limitations

`custom_hud_layout` is newly introduced and experimental. The local VPK path and
the Workshop Manager preview are verified, but an actual Workshop upload should be
tested separately for subscription download, server resource delivery, and future
CS2 updates. Per-player dialog state, input capture, and button callbacks resolve
their build-specific addresses through
`resources/gamedata/signatures.jsonc`. If startup logging or `!chud_status`
reports that the native bridge is unavailable after a CS2 update, do not use the
old signatures on a new build. Revalidate all four signatures against that
server's `server.dll`, update the GameData file, run `-Action Validate`, deploy,
and reload the plugin. A verified signature-only update does not require changing
C#.
