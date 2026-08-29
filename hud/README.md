# Custom HUD resources

This directory owns the source layouts and shared stylesheet for `CustomHudProbeSW2`:

```text
layout/swift_menu_custom_hud.xml
layout/cyber_card_custom_hud.xml
layout/hover3d_gallery_custom_hud.xml
layout/flip_card_custom_hud.xml
images/hover3d/card_1.png + card_1.vtex
images/hover3d/card_2.png + card_2.vtex
images/hover3d/card_3.png + card_3.vtex
styles/swift_menu_custom_hud.css
```

The layouts and stylesheet include adaptations from MIT-licensed reference
implementations, while the gallery demo images have separate media-rights
considerations. See [`../THIRD_PARTY_NOTICES.md`](../THIRD_PARTY_NOTICES.md)
before redistributing the HUD resource package.

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

The flip-card layout adapts
[Uiverse.io `little-goat-24` by `joe-watson-sbf`](https://uiverse.io/joe-watson-sbf/little-goat-24)
to the declarative Custom HUD surface. Panorama does not expose the browser combination of
`transform-style: preserve-3d` and `backface-visibility` here, so the front and
back faces use synchronized opposite Y rotations on padded, unclipped wrappers.
The visible face switches at the 90-degree midpoint instead of cross-fading, so
the rounded surface does not produce a rectangular alpha overlay or clip during
the turn. This retains the 190-by-254 footprint, 800 ms hover flip, coral border,
shadow, warm face colors, and lower-right corner accents without adding VJS or
disallowed layout attributes.

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
