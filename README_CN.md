# CustomHudProbeSW2

[English documentation](README.md)

`CustomHudProbeSW2` 是面向 CS2 实验性 `custom_hud_layout` 实体的 SwiftlyS2 概念验证插件。
它在运行中的服务器上动态创建 HUD 实体，不需要 Hammer 工作流，也不需要在地图中预先放置实体。

## 游戏内效果

![CustomHudProbeSW2 HUD 在 Counter-Strike 2 中显示](assets/custom-hud-probe-in-game.png)

## 已验证的能力

- `!chud_spawn`：创建并显示 HUD 探针。
- `!chud_status`：显示插件当前跟踪的实体。
- `!chud_clear`：移除 HUD 探针。
- 客户端能够解析并显示资源 VPK 提供的 HUD 资源。
- 原生 `CustomHudClickedReceiver` 会将三个按钮 ID 路由到对应玩家的菜单：主按钮更新状态，
  次按钮应用强调样式，关闭按钮释放输入捕获并折叠面板。

## 快速开始

安装 .NET 10 SDK 和与服务器兼容的 SwiftlyS2 运行时后，在本目录运行：

```powershell
.\build_and_deploy.ps1 -ServerRoot F:\csgoserver_win\cs2
```

插件会发布到：
`<ServerRoot>\game\csgo\addons\swiftlys2\plugins\CustomHudProbeSW2\`。

## 资源分发

HUD 资源已发布至 Steam 创意工坊：
[Workshop 项目 3789924061](https://steamcommunity.com/sharedfiles/filedetails/?id=3789924061)。

正式服务器的目标分发方式是使用
[SwiftlyS2 AddonsManager](https://github.com/SwiftlyS2-Plugins/AddonsManager)，由它下载并挂载
Workshop Addon，使服务器与玩家客户端取得 HUD 资源。配置和验证步骤见下方开发文档。

## 开发文档

插件部署、创意工坊发布、`gameinfo.gi` 配置、本地测试与排错，请参阅
[中文开发文档](docs/DEVELOPMENT_CN.md)。

## 当前状态

动态实体、资源加载、每玩家状态、输入捕获和原生点击接收器均已实现，依赖锁定的
SwiftlyS2 1.4.6-beta.8 / `server.dll` 特征码契约。CS2 更新后必须重新验证桥接特征码，
否则 HUD 不会创建。
