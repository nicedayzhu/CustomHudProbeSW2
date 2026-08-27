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
.\tools\build_hud_resources.ps1 -Action Build
.\tools\build_hud_resources.ps1 -Action Install
```
