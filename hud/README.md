# Custom HUD resources

This directory owns the source layout and stylesheet for `CustomHudProbeSW2`:

```text
layout/swift_menu_custom_hud.xml
styles/swift_menu_custom_hud.css
```

The layout keeps the Custom HUD allowlist (`Panel`, `Label`, and `Button`) and
uses fixed button IDs. Per-player text, CSS classes, input capture, and native
click dispatch are owned by the plugin; no VJS or `onactivate` handler is used.

The `swift_menu_poc` build helpers compile these sources into the
`swift_custom_hud_layout_probe` addon and VPK. Run its `CustomHudValidate` action
before compiling so the cross-project source and native-bridge contracts are
checked together.
