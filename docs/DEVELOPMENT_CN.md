# CustomHudProbeSW2 开发文档

[English development guide](DEVELOPMENT.md) · [项目介绍](../README_CN.md)

本文档说明独立 SwiftlyS2 插件及其配套 HUD 资源创意工坊项的技术工作流。

## 架构与职责

项目有两个需要分别部署的产物：

- **CustomHudProbeSW2 插件**：运行于 SwiftlyS2 服务器；执行 `!chud_spawn` 时动态创建
  `custom_hud_layout`。
- **HUD 资源创意工坊项**：向客户端提供该实体所需的 Panorama 资源。

上传创意工坊不会部署插件；部署插件也不会把 HUD 资源分发给订阅者。

## 构建和部署插件

前置条件：

- .NET 10 SDK；
- 与目标服务器兼容的 SwiftlyS2 运行时；
- 与本仓库并列的 `swift_menu_poc` 仓库。

在仓库根目录构建并部署插件：

```powershell
.\build_and_deploy.ps1 -ServerRoot F:\csgoserver_win\cs2
```

插件会发布到：

```text
<ServerRoot>\game\csgo\addons\swiftlys2\plugins\CustomHudProbeSW2\
```

脚本刻意不会挂载 VPK，也不会修改任何 `gameinfo.gi` 文件。

## 构建 HUD 资源包

从本仓库进入相邻的 `swift_menu_poc` 仓库：

```powershell
Set-Location ..\swift_menu_poc
.\swift-menu.ps1 -Action CustomHudValidate
.\swift-menu.ps1 -Action CustomHudBuild
```

这会创建可上传 Addon `swift_custom_hud_layout_probe` 及其本地 VPK。Workshop Manager 使用
Addon 目录中的已编译输出，不会替你编译原始 Panorama 源文件。

## 发布到创意工坊

### 创建 Workshop 发布项

1. 在 CS2 Workshop Tools 中启动 **Counter-Strike 2 Workshop Manager**。
2. 点击 **New**。
3. 选择 `swift_custom_hud_layout_probe` 作为加载项文件夹。
4. 检查内容预览：至少必须显示 `[vxml_c]: 1 Files` 与 `[vcss_c]: 1 Files`。
5. 填写标题、说明、预览图和可见性，然后提交。

第 4 步是决定性检查。显示这两项已编译资源，即说明 Workshop Manager 已找到 HUD 内容；无需
在窗口中手动选择或上传 `.vpk` 文件。

### 内容未显示时配置 `VpkDirectories`

先关闭 Workshop Manager；它在启动时读取配置。默认客户端配置文件为：

```text
F:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo\gameinfo.gi
```

在 `swift_menu_poc` 中运行：

```powershell
.\powershell\experiments\enable_custom_game_panorama_vpkdirs.ps1
```

若 CS2 安装在其他 Steam 库，传入实际路径：

```powershell
.\powershell\experiments\enable_custom_game_panorama_vpkdirs.ps1 `
  -GameInfoPath "D:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive\game\csgo\gameinfo.gi"
```

脚本首次运行会在原文件同目录创建备份：

```text
gameinfo.gi.swift_custom_game_probe.bak
```

它会在 `AddonConfig` → `VpkDirectories` 中找到已有的：

```text
"include"       "panorama/images/map_icons"
```

并紧跟其后仅追加缺失项。下面是简化结果；其他已有的 `include` 行保持不变：

```text
"VpkDirectories"
{
    // 其他已有的 include 行保持不变。
    "include"       "panorama/images/map_icons"
    "include"       "panorama/layout/custom_game"
    "include"       "panorama/styles/custom_game"
    "include"       "panorama/scripts/custom_game"
}
```

脚本可重复运行：已经存在的行只会被报告，不会重复添加。若要撤销，关闭 Workshop Manager，
将 `.bak` 文件恢复为 `gameinfo.gi` 后再重新打开工具。

`AddonConfig/VpkDirectories` 决定 Workshop Tools 收集哪些文件；它与本地服务器/客户端测试
时用于挂载 VPK 的 `FileSystem/SearchPaths` 是两件不同的事。

## 发布后的测试

1. 用订阅了 Workshop 项的干净客户端连接测试服务器。
2. 确认服务器已部署 `CustomHudProbeSW2`。
3. 输入 `!chud_spawn`，确认 HUD 显示。
4. 输入 `!chud_status`，确认报告活动实体。
5. 输入 `!chud_clear`，确认 HUD 消失。

若实体已创建但 HUD 没有显示，先确认资源 VPK 已同时对服务器和客户端可用，然后保留
`dev_report_info_hud_layout` 的输出用于排错。

## 实验性边界

`custom_hud_layout` 是新引入的实验性功能。本地 VPK 路径及 Workshop Manager 内容预览已经
验证，但真实上传后仍应单独验证订阅下载、服务器资源分发和未来 CS2 更新。每玩家对话状态与
点击回调还需要 SwiftlyS2 尚未提供的绑定。
