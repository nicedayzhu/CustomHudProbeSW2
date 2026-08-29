# Third-party notices / 第三方声明

## License scope / 授权范围

The root [MIT License](LICENSE) applies to the original source code, scripts,
configuration, and documentation created for `CustomHudProbeSW2`, unless a file
or section states otherwise. It does not replace the licenses, copyright, or
trademark terms of third-party work identified below.

根目录的 [MIT License](LICENSE) 仅适用于 `CustomHudProbeSW2` 的原创源代码、脚本、
配置与文档（文件或章节另有说明的除外），不会替代下列第三方作品原有的许可证、版权或商标条款。

Attribution does not imply endorsement by any third party.

## MIT-licensed reference implementations

The following references are licensed under the MIT License. Their copyright
notices are retained here, and the common MIT permission and warranty text is
reproduced at the end of this file.

### PanoramaLayout

- Source: [laper32/PanoramaLayout](https://github.com/laper32/PanoramaLayout)
- Use: implementation reference for the native `CS_UM_CustomHudClicked` receiver flow
- Copyright: `Copyright (c) 2026 laper32`

### Uiverse Cyber Card

- Source: [`cowardly-eagle-56` by `00Kubi`](https://uiverse.io/00Kubi/cowardly-eagle-56)
- Adapted files: `hud/layout/cyber_card_custom_hud.xml` and the corresponding
  Cyber Card rules in `hud/styles/swift_menu_custom_hud.css`
- Copyright: `Copyright (c) 2026 kennyotsu (kotsu)`
- Copyright: `Copyright (c) 2026 00Kubi (Kubi)`

### Uiverse Flip Card

- Source: [`little-goat-24` by `joe-watson-sbf`](https://uiverse.io/joe-watson-sbf/little-goat-24)
- Adapted files: `hud/layout/flip_card_custom_hud.xml` and the corresponding
  Flip Card rules in `hud/styles/swift_menu_custom_hud.css`
- Copyright: `Copyright (c) 2026 joe-watson-sbf (Joseph Watzson)`

### daisyUI Hover 3D Card

- Source: [daisyUI Hover 3D Card](https://daisyui.com/components/hover-3d/)
- Upstream code: [hover3d.css](https://github.com/saadeghi/daisyui/blob/master/packages/daisyui/src/components/hover3d.css)
- Adapted files: `hud/layout/hover3d_gallery_custom_hud.xml` and the corresponding
  Hover 3D rules in `hud/styles/swift_menu_custom_hud.css`
- Copyright: `Copyright (c) 2020 Pouya Saadeghi`

## Media and game resources excluded from the project MIT license

The following media are not relicensed under the root project license:

- `hud/images/hover3d/card_1.png`, `card_2.png`, and `card_3.png` are converted
  copies of the stock demo images referenced by the daisyUI example at
  `https://img.daisyui.com/images/stock/card-{1,2,3}.webp`. The component page
  does not state a separate license for the image content. Preserve this
  attribution and verify the applicable image rights, or replace the images,
  before redistributing them independently.
- `assets/*.png` are documentation screenshots. They contain rendered
  Counter-Strike 2 content and, in some cases, the gallery demo images above.
  They are provided as visual documentation and are not covered by the
  project's MIT grant.
- `s2r://` images and the `Stratum2` fonts named by the Panorama stylesheet are
  Counter-Strike 2 runtime resources supplied by Valve; they are referenced but
  not bundled by this repository. Counter-Strike, Source 2, Steam, and related
  names and assets remain the property of their respective owners.

下列媒体资源不随项目根目录的 MIT 协议重新授权：Gallery 的三张演示图片、`assets/`
中的游戏截图，以及运行时引用的 Valve 图片和字体。若要独立再分发这些资源，请先核对其原始授权，
或替换为拥有明确再分发权的素材。

## MIT License text for the third-party software above

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
