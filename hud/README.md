# Custom HUD resources

This directory owns the source layout and stylesheet for `CustomHudProbeSW2`:

```text
layout/swift_menu_custom_hud.xml
styles/swift_menu_custom_hud.css
```

The layout keeps the Custom HUD allowlist (`Panel`, `Label`, and `Button`) and
uses fixed button IDs. Per-player text, CSS classes, input capture, and native
click dispatch are owned by the plugin; no VJS or `onactivate` handler is used.

Use the project-owned build entry point to validate, compile, pack, or install
these resources:

```powershell
.\tools\build_hud_resources.ps1 -Action Validate
.\tools\build_hud_resources.ps1 -Action Build -Cs2Root <CS2-root> -VpkEditCli <vpkeditcli-path>
.\tools\build_hud_resources.ps1 -Action Install -Cs2Root <CS2-root>
```

`Validate` does not compile files. `Build` compiles and packs the VPK;
`Install` must therefore be run only after a successful `Build`. It installs the
VPK into `<CS2-root>\game\csgo\overrides\`, mounts it through the local
`gameinfo.gi`, and creates a one-time backup beside that file. This is a local
development route, not the production distribution method. See the
[development guide](../docs/DEVELOPMENT.md) for exact parameters, rollback, and
the Workshop/AddonsManager workflow.
