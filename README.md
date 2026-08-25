# CustomHudProbeSW2

[中文说明 / Chinese documentation](README_CN.md)

This SwiftlyS2 validation plugin dynamically creates CS2's `custom_hud_layout`
entity at runtime. It needs neither Hammer nor a pre-placed map entity.

## Current scope

- `!chud_spawn`: creates and shows the HUD probe.
- `!chud_status`: reports the entity currently tracked by the plugin.
- `!chud_clear`: removes the HUD probe.

The validated path is “dynamically create the entity, resolve the resource VPK,
and display the HUD.” Per-player state, mouse input, and button callbacks are
intentionally deferred until SwiftlyS2 exposes bindings for the new CS2 schema and
messages.

## Build and deploy the plugin

Install the .NET 10 SDK and a SwiftlyS2 runtime that matches the server. From this
directory, run:

```powershell
.\build_and_deploy.ps1 -ServerRoot F:\csgoserver_win\cs2
```

The script publishes only to:

```text
<ServerRoot>\game\csgo\addons\swiftlys2\plugins\CustomHudProbeSW2\
```

It does not copy or mount the resource VPK, and it does not modify `gameinfo.gi`.

## Publish the HUD resources to the Steam Workshop

The Workshop item and this C# plugin are separate deliverables:

- the Workshop item delivers the HUD resources to clients;
- this plugin must still be deployed separately to the SwiftlyS2 server.

### 1. Generate the uploadable Addon

From this repository, enter the sibling `swift_menu_poc` repository and run:

```powershell
Set-Location ..\swift_menu_poc
.\swift-menu.ps1 -Action CustomHudValidate
.\swift-menu.ps1 -Action CustomHudBuild
```

This creates the uploadable Addon named `swift_custom_hud_layout_probe` and a local
VPK. Workshop Manager consumes the compiled result in the CS2 Addon directory; it
does not compile raw UI source files for you.

### 2. Create the Workshop item in Workshop Manager

1. Start **Counter-Strike 2 Workshop Manager** from CS2 Workshop Tools.
2. Click **New**.
3. Select `swift_custom_hud_layout_probe` as the addon folder.
4. Inspect the contents preview: it must show at least `[vxml_c]: 1 Files` and
   `[vcss_c]: 1 Files`.
5. Enter the title, description, preview image, and visibility, then submit.

Step 4 is the key check. Once those two compiled resources are listed, Workshop
Manager has found the HUD content. Do not manually select or upload a `.vpk` file
in this window.

### 3. If `vxml_c` or `vcss_c` is missing

Close Workshop Manager first. It reads this configuration at launch, so it must be
reopened after the change. The default client `gameinfo.gi` path is:

```text
F:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo\gameinfo.gi
```

From `swift_menu_poc`, run:

```powershell
.\powershell\experiments\enable_custom_game_panorama_vpkdirs.ps1
```

For a CS2 install in another Steam library, provide the actual path:

```powershell
.\powershell\experiments\enable_custom_game_panorama_vpkdirs.ps1 `
  -GameInfoPath "D:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive\game\csgo\gameinfo.gi"
```

On its first run, the script creates this backup beside the original file:

```text
gameinfo.gi.swift_custom_game_probe.bak
```

It locates this existing entry inside `AddonConfig` → `VpkDirectories`:

```text
"include"       "panorama/images/map_icons"
```

It then appends **only missing entries** immediately after it. The following is a
simplified result; all other existing `include` entries remain untouched:

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

The script is safe to run again: if the three entries already exist, it reports
that they are enabled and does not duplicate them. To revert, close Workshop
Manager, restore the `.bak` file as `gameinfo.gi`, and reopen the tool.

`VpkDirectories` controls which Addon content Workshop Tools collects. It is
separate from the `FileSystem/SearchPaths` VPK mount used by local server/client
testing.

### 4. Minimal post-publish acceptance test

1. Connect a clean client subscribed to the Workshop item to the test server.
2. Confirm that the server has `CustomHudProbeSW2` deployed.
3. Run `!chud_spawn` and confirm the HUD appears; run `!chud_clear` and confirm it
   disappears.

`custom_hud_layout` is a newly introduced experimental CS2 feature. A positive
Workshop Manager preview does not replace regression testing of Workshop download,
server resource delivery, and future CS2 updates.
