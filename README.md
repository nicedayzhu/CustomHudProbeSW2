# CustomHudProbeSW2

[中文说明](README_CN.md)

`CustomHudProbeSW2` is a SwiftlyS2 proof-of-concept for CS2's experimental
`custom_hud_layout` entity. It creates the HUD entity dynamically on a running
server, with no Hammer workflow or pre-placed map entity.

## What it validates

- `!chud_spawn` creates and displays the HUD probe.
- `!chud_status` reports the entity tracked by the plugin.
- `!chud_clear` removes the HUD probe.
- The client resolves and displays the HUD resources delivered by the resource VPK.

## Quick start

Install .NET 10 SDK and a SwiftlyS2 runtime compatible with your server, then run:

```powershell
.\build_and_deploy.ps1 -ServerRoot F:\csgoserver_win\cs2
```

The plugin is published to
`<ServerRoot>\game\csgo\addons\swiftlys2\plugins\CustomHudProbeSW2\`.

## Development documentation

See the [English development guide](docs/DEVELOPMENT.md) for plugin deployment,
Workshop publishing, `gameinfo.gi` configuration, local testing, and troubleshooting.

## Status

The dynamic entity and HUD-resource path are verified. Per-player state, input
capture, and click callbacks are deferred until SwiftlyS2 exposes bindings for the
new CS2 schema and messages.
