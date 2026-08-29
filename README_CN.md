# CustomHudProbeSW2

[English documentation](README.md)

`CustomHudProbeSW2` 是面向 CS2 实验性 `custom_hud_layout` 实体的 SwiftlyS2 概念验证插件。
它在运行中的服务器上动态创建 HUD 实体，不需要 Hammer 工作流，也不需要在地图中预先放置实体。

## 游戏内效果

![CustomHudProbeSW2 HUD 在 Counter-Strike 2 中显示](assets/Snipaste_2026-08-27_12-52-37.png)

## 已验证的能力

- `!chud_spawn menu`：在唯一探针实体中加载并显示按钮菜单 layout；省略参数时默认使用 `menu`。
- `!chud_spawn card`：切换唯一探针实体，加载独立 3D 卡片 layout。
- `!chud_spawn gallery`：切换至三图片画廊，每张图片都有独立的八区 3D hover 与移动高光效果。
- `!chud_spawn flip`：切换至新增的珊瑚色/米黄色双面翻转卡片；别名为 `flipcard`、`turn`。
- `!chud_open`：重新打开当前已加载的 HUD。
- `!chud_close`：为发起命令的玩家隐藏当前 HUD 并释放输入捕获。
- `!chud_status`：显示插件当前跟踪的实体。
- `!chud_clear`：移除 HUD 探针。
- 客户端能够解析并显示资源 VPK 提供的 HUD 资源。
- 原生 `CustomHudClickedReceiver` 会将三个按钮 ID 路由到对应玩家的菜单：主按钮更新状态，
  次按钮应用强调样式，关闭按钮释放输入捕获并折叠面板。

## 致谢

原生 Custom HUD 点击处理的最初实现思路参考了
[laper32/PanoramaLayout](https://github.com/laper32/PanoramaLayout)，尤其是其通过
`CS_UM_CustomHudClicked` 接收点击的流程。感谢作者公开分享这一实现。

Hover 3D 画廊移植自
[daisyUI Hover 3D Card 示例](https://daisyui.com/components/hover-3d/)。三张演示图片来自该示例引用的
`card-1.webp`、`card-2.webp` 与 `card-3.webp` 素材，并已转换成本地 Panorama 纹理。

双面翻转卡片移植自 Uiverse.io 用户 `joe-watson-sbf` 的
[`little-goat-24` 示例](https://uiverse.io/joe-watson-sbf/little-goat-24)。原始浏览器 HTML/CSS
已转换为声明式 Panorama Custom HUD，同时保留原示例的尺寸、配色与 hover 行为。

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
.\tools\build_hud_resources.ps1 -Action Install -Cs2Root $cs2Root -ServerRoot $serverRoot
```

`Install` 会把同一 VPK 同步复制到客户端 `<Cs2Root>\game\csgo\overrides\` 与服务端
`<ServerRoot>\game\csgo\overrides\`，分别检查 SHA-256，并在两端 `gameinfo.gi` 中加入搜索路径。首次执行会创建
`gameinfo.gi.swift_custom_hud_layout_probe.bak`，停止相关 CS2 进程后可恢复该备份以撤销挂载。

在服务器控制台重载已部署插件（或重启服务器）：

```text
sw plugins reload CustomHudProbeSW2
```

随后连接测试服务器，使用以下聊天命令：

| 命令 | 作用 |
| --- | --- |
| `!chud_spawn menu` | 在唯一探针实体中加载按钮菜单；省略参数时默认使用 `menu`。 |
| `!chud_spawn card` | 销毁旧模式并在唯一探针实体中加载独立 3D 卡片。 |
| `!chud_spawn gallery` | 销毁旧模式并加载三图片 Hover 3D 画廊；别名为 `hover3d`、`images`。 |
| `!chud_spawn flip` | 销毁旧模式并加载双面翻转卡片；别名为 `flipcard`、`turn`。 |
| `!chud_open` | 重新打开发起命令玩家的当前 HUD；探针必须已存在。 |
| `!chud_close` | 隐藏发起命令玩家的当前 HUD并释放输入捕获。 |
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
