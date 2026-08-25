# CustomHudProbeSW2

这是 `swift_menu_poc` 的第一阶段 SwiftlyS2 验证插件：它在服务器运行时动态创建
`custom_hud_layout`，不需要 Hammer，也不要求地图预先放置实体。

## 当前验证范围

- `!chud_spawn`：使用 SDK 通用基类的
  `CreateEntityByDesignerName<CEntityInstance>("custom_hud_layout", -1)` 创建实体，并在
  `DispatchSpawn` 时写入 `targetname=swift_menu_custom_hud` 与布局资源路径。
- `!chud_status`：报告当前插件跟踪的实体。
- `!chud_clear`：通过通用 `Kill` input 清理探针实体。
- 使用固定可见的布局文本，验证原生 HUD host 与资源解析；本阶段不使用 VScript、VJS 或 Hammer。

## 构建与部署

需要 .NET 10 SDK 和与服务器匹配的 SwiftlyS2 运行时。PowerShell 中执行：

```powershell
.\build_and_deploy.ps1 -ServerRoot F:\csgoserver_win\cs2
```

脚本只发布插件到：

```text
<ServerRoot>\game\csgo\addons\swiftlys2\plugins\CustomHudProbeSW2\
```

它不会复制、挂载 HUD VPK，也不会修改 `gameinfo.gi`。先在相邻的
`..\swift_menu_poc` 仓库中运行 `CustomHudCompile` / `CustomHudPack`，生成
`dist\swift_custom_hud_layout_probe.vpk`，再按你的
服务器资源分发方式让客户端能解析：

```text
panorama/layout/custom_game/swift_menu_custom_hud.vxml_c
panorama/styles/custom_game/swift_menu_custom_hud.vcss_c
```

## 将 HUD 资源发布到创意工坊

Workshop 发布的是 **HUD 资源 VPK**，不是这个 C# 插件。插件仍需用上面的
`build_and_deploy.ps1` 单独发布到运行 SwiftlyS2 的服务器；Workshop VPK 只负责让客户端
取得 `custom_hud_layout` 所引用的 Panorama 资源。

### 1. 编译与打包资源

在相邻的 `swift_menu_poc` 仓库运行：

```powershell
Set-Location ..\swift_menu_poc
.\swift-menu.ps1 -Action CustomHudValidate
.\swift-menu.ps1 -Action CustomHudBuild
```

`CustomHudBuild` 使用 CS2 的 ResourceCompiler，将源 `.xml` 和 `.css` 编译到：

```text
<CS2>\game\csgo_addons\swift_custom_hud_layout_probe\
  addoninfo.txt
  panorama/layout/custom_game/swift_menu_custom_hud.vxml_c
  panorama/styles/custom_game/swift_menu_custom_hud.vcss_c
```

随后打包出 `..\swift_menu_poc\dist\swift_custom_hud_layout_probe.vpk`。上传时必须是这类
编译后的 `*_c` 资源：Workshop Manager 不会把原始 `.xml` / `.css` 自动变成可运行的
Panorama 文件。这个包也刻意没有 `maps/` 根目录、Hammer 地图实体或 `vjs_c`。

### 2. 让 Workshop Manager 识别 `custom_game` 内容

在 CS2 Workshop Tools 中打开 Counter-Strike 2 Workshop Manager，创建新发布项，并选择
`swift_custom_hud_layout_probe` 这个 Addon 内容目录。正确时，内容预览至少应显示：

```text
[vxml_c]: 1 Files
[vcss_c]: 1 Files
```

若两个条目没有出现，先退出 Workshop Manager，然后在 `swift_menu_poc` 中运行下面的
一次性配置脚本，再重新打开工具：

```powershell
.\powershell\experiments\enable_custom_game_panorama_vpkdirs.ps1
```

该脚本会备份客户端 `game\csgo\gameinfo.gi`，并在
`AddonConfig/VpkDirectories` 中加入：

```text
panorama/layout/custom_game
panorama/styles/custom_game
panorama/scripts/custom_game
```

它只影响 Workshop Tools 的 Addon 内容收集范围；它不是运行时挂载 VPK 的设置。服务器和
客户端本地联调所需的 `FileSystem/SearchPaths` 挂载，仍应按你的服务器资源分发流程单独配置。

### 3. 填写元数据并提交

在 Workshop Manager 中填写标题、说明、预览图和可见性，确认内容预览仍列出上面的两个
编译文件后提交。发布完成后，用订阅该项的干净客户端做一次 `!chud_spawn` 验证；客户端能
下载 Workshop 项并不代替服务器安装 `CustomHudProbeSW2`。

`custom_hud_layout` 仍是新引入的实验性 CS2 功能。因此“工具显示可上传”和“本地 VPK 测试
通过”已经得到验证，但首次真实发布后仍应在订阅客户端下载、服务器资源分发及版本更新后分别
回归测试。

## 游戏内验证

1. 启动已加载 SwiftlyS2 与本插件的服务器，并进入任意可运行地图。
2. 确认 HUD 资源 VPK 已可被该服务器和客户端访问。
3. 控制台或聊天输入 `!chud_spawn`。
4. 预期 HUD 立即显示 `SWIFTLYS2 ENTITY PROBE`，不需要按 `ESC`，也不需要 Hammer。
5. 输入 `!chud_clear`，HUD 应消失。
6. 资源不显示时，保留 `dev_report_info_hud_layout` 输出，并确认 VPK 的实际挂载状态。

## 已知边界（第二阶段）

当前 SwiftlyS2 SDK 尚未生成 `CCSCustomHudLayout`、其网络状态类和
`CCSUsrMsg_CustomHudClicked`。因此本插件故意只验证“动态生成实体 + 解析 HUD 资源”。
每玩家的输入捕获、样式/对话变量和按钮点击回传，需要先为本次 CS2 更新补上 SDK schema/protobuf
绑定，不能用不稳定的裸内存偏移来假装完成。
