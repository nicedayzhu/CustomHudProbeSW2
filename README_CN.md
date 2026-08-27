# CustomHudProbeSW2

[English documentation](README.md)

`CustomHudProbeSW2` 是面向 CS2 实验性 `custom_hud_layout` 实体的 SwiftlyS2 概念验证插件。
它在运行中的服务器上动态创建 HUD 实体，不需要 Hammer 工作流，也不需要在地图中预先放置实体。

## 游戏内效果

![CustomHudProbeSW2 HUD 在 Counter-Strike 2 中显示](assets/Snipaste_2026-08-27_12-52-37.png)

## 已验证的能力

- `!chud_spawn`：创建并显示 HUD 探针。
- `!chud_open`：重新打开并重置发起命令玩家的 HUD 菜单。
- `!chud_status`：显示插件当前跟踪的实体。
- `!chud_clear`：移除 HUD 探针。
- 客户端能够解析并显示资源 VPK 提供的 HUD 资源。
- 原生 `CustomHudClickedReceiver` 会将三个按钮 ID 路由到对应玩家的菜单：主按钮更新状态，
  次按钮应用强调样式，关闭按钮释放输入捕获并折叠面板。

## 致谢

原生 Custom HUD 点击处理的最初实现思路参考了
[laper32/PanoramaLayout](https://github.com/laper32/PanoramaLayout)，尤其是其通过
`CS_UM_CustomHudClicked` 接收点击的流程。感谢作者公开分享这一实现。

## 快速开始：本地 override 测试

插件与 HUD 资源是两个独立产物；本地测试需要两者都就绪：

- .NET 10 SDK 和与服务器兼容的 SwiftlyS2 运行时；
- 含 `resourcecompiler.exe` 的本地 CS2 安装；
- 用于打包资源 VPK 的 [VPKEdit CLI](https://github.com/craftablescience/VPKEdit)。

在项目根目录按本机实际路径设置变量，并部署插件：

```powershell
$serverRoot = 'F:\csgoserver_win\cs2'
$cs2Root = 'F:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive'
$vpkEditCli = 'D:\Tools\VPKEdit\vpkeditcli.exe'

.\build_and_deploy.ps1 -ServerRoot $serverRoot
```

插件会发布到：
`<ServerRoot>\game\csgo\addons\swiftlys2\plugins\CustomHudProbeSW2\`。

构建并挂载本地资源 override。`Install` 依赖 `Build` 生成的 VPK：

```powershell
.\tools\build_hud_resources.ps1 -Action Validate
.\tools\build_hud_resources.ps1 -Action Build -Cs2Root $cs2Root -VpkEditCli $vpkEditCli
.\tools\build_hud_resources.ps1 -Action Install -Cs2Root $cs2Root
```

`Install` 会将 VPK 复制到 `<Cs2Root>\game\csgo\overrides\`，并在该本地安装的
`gameinfo.gi` 中加入搜索路径。首次执行会创建
`gameinfo.gi.swift_custom_hud_layout_probe.bak`，停止相关 CS2 进程后可恢复该备份以撤销挂载。

在服务器控制台重载已部署插件（或重启服务器）：

```text
sw plugins reload CustomHudProbeSW2
```

随后连接测试服务器，使用以下聊天命令：

| 命令 | 作用 |
| --- | --- |
| `!chud_spawn` | 创建探针，并向当前已连接的真人玩家打开菜单。 |
| `!chud_open` | 重新打开并重置发起命令玩家的菜单；探针必须已存在。 |
| `!chud_status` | 报告桥接层与探针实体状态。 |
| `!chud_clear` | 移除探针并释放输入捕获。 |

挂载新的 override VPK 后，需要重启本地 CS2 客户端/服务器。local override 仅用于开发；生产环境请使用下方的 Workshop 分发方式。

## 资源分发

HUD 资源已发布至 Steam 创意工坊：
[Workshop 项目 3789924061](https://steamcommunity.com/sharedfiles/filedetails/?id=3789924061)。

正式服务器的目标分发方式是使用
[SwiftlyS2 AddonsManager](https://github.com/SwiftlyS2-Plugins/AddonsManager)，由它下载并挂载
Workshop Addon，使服务器与玩家客户端取得 HUD 资源。配置和验证步骤见下方开发文档。

## 开发文档

命令参数、插件部署、创意工坊发布、`gameinfo.gi` 配置、本地测试与排错，请参阅
[中文开发文档](docs/DEVELOPMENT_CN.md)。

## 当前状态

动态实体、资源加载、每玩家状态、输入捕获和原生点击接收器均已实现，依赖 SwiftlyS2
1.4.6-beta.8。桥接特征码统一放在插件的
`resources/gamedata/signatures.jsonc`，不再硬编码于 C#；CS2 更新后必须重新验证该文件，
否则 HUD 不会创建。
