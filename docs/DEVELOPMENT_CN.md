# CustomHudProbeSW2 开发文档

[English development guide](DEVELOPMENT.md) · [项目介绍](../README_CN.md)

本文档说明独立 SwiftlyS2 插件及其配套 HUD 资源创意工坊项的技术工作流。

HUD 源文件与全部 Custom HUD 构建脚本统一保存在本项目：`hud/layout`、
`hud/styles` 与 `tools/build_hud_resources.ps1`。

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
- 含 `game\bin\win64\resourcecompiler.exe` 的 CS2 安装；
- 用于打包 VPK 的 [VPKEdit CLI](https://github.com/craftablescience/VPKEdit)。

在仓库根目录构建并部署插件：

```powershell
.\build_and_deploy.ps1 -ServerRoot F:\csgoserver_win\cs2
```

插件会发布到：

```text
<ServerRoot>\game\csgo\addons\swiftlys2\plugins\CustomHudProbeSW2\
```

脚本刻意不会挂载 VPK，也不会修改任何 `gameinfo.gi` 文件。
部署后在服务器控制台重载插件，或重启服务器：

```text
sw plugins reload CustomHudProbeSW2
```

## 构建 HUD 资源包

先按本机设置本地 CS2 与 VPKEdit 可执行文件路径，再运行资源工具：

```powershell
$cs2Root = 'F:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive'
$vpkEditCli = 'D:\Tools\VPKEdit\vpkeditcli.exe'

.\tools\build_hud_resources.ps1 -Action Validate
.\tools\build_hud_resources.ps1 -Action Build -Cs2Root $cs2Root -VpkEditCli $vpkEditCli
```

这会创建可上传 Addon `swift_custom_hud_layout_probe`，并在
`dist\swift_custom_hud_layout_probe.vpk` 写入本地 VPK。Workshop Manager 使用 Addon
目录中的已编译输出，不会替你编译原始 Panorama 源文件。

`Validate` 仅检查布局、样式、插件集成契约与必需的 GameData 项，不进行编译；`Compile`
只执行 ResourceCompiler，`Pack` 只从已编译 Addon 写入 VPK，`Build` 依次执行
`Compile` 与 `Pack`。`-Cs2Root` 与 `-VpkEditCli` 的默认值是开发机路径，其他机器必须
显式传入上述实际路径。仅在确实需要不同 Addon 名称时才使用 `-AddonName`。

## 本地 override 开发测试

此路径适合在上传 Workshop 前测试。它只修改 `-Cs2Root` 指向的本地 CS2 安装；服务器插件仍须
按上文单独部署。`Build` 成功后安装其中的 VPK：

```powershell
.\tools\build_hud_resources.ps1 -Action Install -Cs2Root $cs2Root
```

`Install` 会将 `dist\swift_custom_hud_layout_probe.vpk` 复制到
`<Cs2Root>\game\csgo\overrides\`，并在 `<Cs2Root>\game\csgo\gameinfo.gi` 中插入其
VPK 搜索路径。首次安装会创建备份：

```text
gameinfo.gi.swift_custom_hud_layout_probe.bak
```

撤销挂载前先停止相关 CS2 进程，恢复该备份；若不再需要，也应移除复制出的 VPK。安装新 override
后需重启受影响的 CS2 客户端/服务器。若目标 `game\csgo` 不在 `-Cs2Root` 下，可改传
`-CsgoPath`。

## 发布到创意工坊

### 创建 Workshop 发布项

1. 在 CS2 Workshop Tools 中启动 **Counter-Strike 2 Workshop Manager**。
2. 点击 **New**。
3. 选择 `swift_custom_hud_layout_probe` 作为加载项文件夹。
4. 检查内容预览：至少必须显示 `[vxml_c]: 4 Files` 与 `[vcss_c]: 1 Files`。
5. 填写标题、说明、预览图和可见性，然后提交。

第 4 步是决定性检查。显示这两项已编译资源，即说明 Workshop Manager 已找到 HUD 内容；无需
在窗口中手动选择或上传 `.vpk` 文件。

### 内容未显示时配置 `VpkDirectories`

先关闭 Workshop Manager；它在启动时读取配置。默认客户端配置文件为：

```text
F:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo\gameinfo.gi
```

在本项目中运行：

```powershell
.\tools\enable_workshop_vpk_directories.ps1
```

若 CS2 安装在其他 Steam 库，传入实际路径：

```powershell
.\tools\enable_workshop_vpk_directories.ps1 `
  -GameInfoPath "D:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive\game\csgo\gameinfo.gi"
```

脚本首次运行会在原文件同目录创建备份：

```text
gameinfo.gi.swift_custom_hud_layout_probe.vpkdirs.bak
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
}
```

脚本可重复运行：已经存在的行只会被报告，不会重复添加。若要撤销，关闭 Workshop Manager，
将 `.bak` 文件恢复为 `gameinfo.gi` 后再重新打开工具。

`AddonConfig/VpkDirectories` 决定 Workshop Tools 收集哪些文件；它与本地服务器/客户端测试
时用于挂载 VPK 的 `FileSystem/SearchPaths` 是两件不同的事。

## 通过 AddonsManager 分发已发布资源

生产服务器应使用上游
[SwiftlyS2 AddonsManager](https://github.com/SwiftlyS2-Plugins/AddonsManager) 下载并挂载已发布的
Workshop 资源，而不是依赖本地开发用的 VPK 安装方式。本项目的 Workshop 项为：

```text
https://steamcommunity.com/sharedfiles/filedetails/?id=3789924061
```

按其上游 README 安装并部署 AddonsManager，然后打开它的配置文件。通常路径为：

```text
<ServerRoot>\game\csgo\addons\swiftlys2\configs\plugins\AddonsManager\config.jsonc
```

在 `Main.Addons` 中加入本项目的 Workshop ID，并保留已有 ID：

```jsonc
{
  "Main": {
    "Addons": [
      "3789924061"
    ]
  }
}
```

修改配置后重启或重载 AddonsManager。上游插件还提供以下服务器控制台命令：

```text
sw_downloadaddon 3789924061  // 请求下载本 Workshop Addon
sw_searchpath                // 列出当前挂载的 VPK 搜索路径
```

运行 `!chud_spawn` 前使用 `sw_searchpath` 确认资源已挂载。AddonsManager 是目标生产分发方案；
在 Workshop 项首次发布或更新后，仍应在目标服务器上完成端到端下载与分发验证。

## 游戏内命令速查

插件加载后，在玩家聊天中使用以下命令：

| 命令 | 结果 |
| --- | --- |
| `!chud_spawn` | 创建一个动态 `custom_hud_layout` 探针，并向每位已连接真人玩家打开菜单。 |
| `!chud_spawn menu` | 加载按钮菜单；这是默认模式。 |
| `!chud_spawn card` | 加载独立的 25 区域 cyber 卡片。 |
| `!chud_spawn gallery` | 加载三图片 Hover 3D 画廊；别名为 `hover3d`、`images`。 |
| `!chud_spawn flip` | 加载双面翻转卡片；别名为 `flipcard`、`turn`。 |
| `!chud_open` | 为发起命令的玩家重新打开当前 HUD；探针必须已激活。 |
| `!chud_close` | 隐藏发起命令玩家的当前 HUD，并释放输入捕获。 |
| `!chud_status` | 报告原生桥接层是否就绪，以及探针是否处于活动状态。 |
| `!chud_clear` | 移除活动探针，并释放已捕获的输入。 |

如果 `!chud_spawn` 提示原生桥接层不可用，先检查服务器日志，而不要先排查资源 VPK：插件会在
`server.dll` 构建未经验证时拒绝创建 HUD。

## 发布后的测试

1. 用订阅了 Workshop 项的干净客户端连接测试服务器。
2. 确认服务器已部署 `CustomHudProbeSW2`。
3. 依次输入 `!chud_spawn menu`、`card`、`gallery`、`flip`，确认每个 HUD 都会替换上一模式并正确显示。
4. 测试菜单按钮、两种 hover 卡片、画廊方向效果以及 flip 卡片正反面过渡；在 `!chud_close` 后使用 `!chud_open` 重新打开。
5. 输入 `!chud_status`，确认报告活动实体。
6. 输入 `!chud_clear`，确认 HUD 消失。

若实体已创建但 HUD 没有显示，先确认资源 VPK 已同时对服务器和客户端可用，然后保留
`dev_report_info_hud_layout` 的输出用于排错。

## 实验性边界

`custom_hud_layout` 是新引入的实验性功能。本地 VPK 路径及 Workshop Manager 内容预览已经
验证，但真实上传后仍应单独验证订阅下载、服务器资源分发和未来 CS2 更新。每玩家对话状态、
输入捕获与按钮回调通过 `resources/gamedata/signatures.jsonc` 解析构建相关地址。若 CS2 更新后，
启动日志或 `!chud_status` 报告原生桥接层不可用，不要将旧特征码继续用于新版本：应针对该服务器的
`server.dll` 重新验证全部四个特征码，更新 GameData 文件，运行 `-Action Validate`，再部署并重载
插件。经验证的纯特征码更新不需要修改 C#。
