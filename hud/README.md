# Custom HUD resources

This directory owns the source layouts and shared stylesheet for `CustomHudProbeSW2`:

```text
layout/swift_menu_custom_hud.xml
layout/cyber_card_custom_hud.xml
layout/hover3d_gallery_custom_hud.xml
images/hover3d/card_1.png + card_1.vtex
images/hover3d/card_2.png + card_2.vtex
images/hover3d/card_3.png + card_3.vtex
styles/swift_menu_custom_hud.css
```

The menu layout keeps fixed button IDs. Per-player text, CSS classes, input
capture, and native click dispatch are owned by the plugin; no VJS
`onactivate` click bridge is used. `CCSCustomHudLayout` rejects `<scripts>` and
this Panorama runtime rejects the CSS sibling combinator (`~`), so the
standalone card uses a nested chain of 25 non-hit-testable trackers. Each
tracker owns one small hit-testable sensor, while the single shared card is a
descendant of the complete chain. The deepest hovered tracker wins the CSS
cascade and updates that same card panel, preserving the reference's 5-by-5
cursor mapping and 125 ms transform interpolation without cloning or swapping
the animated visual tree.

The Hover 3D gallery adapts the
[daisyUI Hover 3D Card](https://daisyui.com/components/hover-3d/) example to the
same script-free Custom HUD constraints. Each image uses an eight-tracker
nested chain around one shared content panel, corresponding to the original
3-by-3 hover grid with its center omitted. The CSS applies directional tilt,
shadow displacement, a small hover scale, and a moving radial shine to the
shared panel. The three source images are the stock demo images linked by the
daisyUI example, converted to local PNG/VTEX assets so the HUD has no runtime
network dependency.

Use the project-owned build entry point to validate, compile, pack, or install
these resources:

```powershell
.\tools\build_hud_resources.ps1 -Action Validate
.\tools\build_hud_resources.ps1 -Action Build -Cs2Root <CS2-root> -VpkEditCli <vpkeditcli-path>
.\tools\build_hud_resources.ps1 -Action Install -Cs2Root <CS2-root> -ServerRoot <server-root>
```

`Validate` does not compile files. `Build` compiles and packs the VPK;
`Install` must therefore be run only after a successful `Build`. It installs the
same VPK into both client and server `game\csgo\overrides\` directories,
mounts it through each `gameinfo.gi`, verifies both hashes, and creates a
one-time backup beside each file. This is a local development route, not the
production distribution method. See the
[development guide](../docs/DEVELOPMENT.md) for exact parameters, rollback, and
the Workshop/AddonsManager workflow.
