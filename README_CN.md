# CustomHudProbeSW2

[English documentation](README.md)

这是一个 SwiftlyS2 验证插件。它在服务器运行时动态创建 CS2 的
`custom_hud_layout` 实体，因此不需要 Hammer，也不要求地图预先放置实体。

## 当前验证范围

- `!chud_spawn`：创建并显示 HUD 探针。
- `!chud_status`：显示插件当前跟踪的实体。
- `!chud_clear`：移除 HUD 探针。

当前验证已确认：动态创建的实体能从资源 VPK 正确解析并显示 HUD。本阶段不包含每玩家状态、
鼠标输入或按钮回传；这些能力要等 SwiftlyS2 补齐新 CS2 架构/消息绑定后再实现。

## 构建和部署插件

需要 .NET 10 SDK 和与服务器匹配的 SwiftlyS2 运行时。在本目录运行：

```powershell
.\build_and_deploy.ps1 -ServerRoot F:\csgoserver_win\cs2
```

脚本只会将插件发布到：

```text
<ServerRoot>\game\csgo\addons\swiftlys2\plugins\CustomHudProbeSW2\
```

它不会复制资源 VPK、挂载 VPK 或修改 `gameinfo.gi`。

## 上传 HUD 资源到创意工坊

创意工坊项与本 C# 插件是两个独立产物：

- 创意工坊项提供客户端所需的 HUD 资源；
- 本插件仍必须单独部署到运行 SwiftlyS2 的服务器。

### 1. 生成待上传的 Addon

从本仓库进入相邻的 `swift_menu_poc`，执行：

```powershell
Set-Location ..\swift_menu_poc
.\swift-menu.ps1 -Action CustomHudValidate
.\swift-menu.ps1 -Action CustomHudBuild
```

这会创建名为 `swift_custom_hud_layout_probe` 的可上传 Addon，并生成本地 VPK。
Workshop Manager 使用的是 CS2 Addon 目录中的已编译结果，不会自动把原始 UI 源文件编译成
可发布内容。

### 2. 在 Workshop Manager 中创建发布项

1. 在 CS2 Workshop Tools 中启动 **Counter-Strike 2 Workshop Manager**。
2. 点击 **New** 创建发布项。
3. 在“加载项文件夹 / Addon folder”中选择 `swift_custom_hud_layout_probe`。
4. 检查内容预览：至少应看见 `[vxml_c]: 1 Files` 与 `[vcss_c]: 1 Files`。
5. 填写标题、说明、预览图和可见性，然后提交。

第 4 步是最重要的检查：只要预览显示两项已编译资源，Workshop Manager 就已正确找到 HUD
内容；不需要在窗口中手工选择或上传某个 `.vpk` 文件。

### 3. 看不到 `vxml_c` 或 `vcss_c` 时

先关闭 Workshop Manager，再在 `swift_menu_poc` 仓库中运行：

```powershell
.\powershell\experiments\enable_custom_game_panorama_vpkdirs.ps1
```

脚本会备份客户端 `game\csgo\gameinfo.gi`，并为 Workshop Tools 的
`AddonConfig/VpkDirectories` 增加 `custom_game` Panorama 目录。完成后重新打开
Workshop Manager，再重复上一步。

`VpkDirectories` 只决定 Workshop Tools 收集哪些 Addon 内容；它与服务器/客户端本地测试时的
`FileSystem/SearchPaths` VPK 挂载是两件不同的事。

### 4. 发布后的最小验收

1. 用订阅了该 Workshop 项的干净客户端连接测试服务器。
2. 确认服务器已部署 `CustomHudProbeSW2`。
3. 输入 `!chud_spawn`，确认 HUD 显示；再输入 `!chud_clear`，确认 HUD 消失。

`custom_hud_layout` 是新引入的实验性 CS2 功能。Workshop Manager 显示资源并不替代实际的
订阅下载、服务器资源分发和 CS2 版本更新后的回归测试。
