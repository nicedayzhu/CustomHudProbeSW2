# CustomHudProbeSW2

[English documentation](README.md)

`CustomHudProbeSW2` 是面向 CS2 实验性 `custom_hud_layout` 实体的 SwiftlyS2 概念验证插件。
它在运行中的服务器上动态创建 HUD 实体，不需要 Hammer 工作流，也不需要在地图中预先放置实体。

## 已验证的能力

- `!chud_spawn`：创建并显示 HUD 探针。
- `!chud_status`：显示插件当前跟踪的实体。
- `!chud_clear`：移除 HUD 探针。
- 客户端能够解析并显示资源 VPK 提供的 HUD 资源。

## 快速开始

安装 .NET 10 SDK 和与服务器兼容的 SwiftlyS2 运行时后，在本目录运行：

```powershell
.\build_and_deploy.ps1 -ServerRoot F:\csgoserver_win\cs2
```

插件会发布到：
`<ServerRoot>\game\csgo\addons\swiftlys2\plugins\CustomHudProbeSW2\`。

## 开发文档

插件部署、创意工坊发布、`gameinfo.gi` 配置、本地测试与排错，请参阅
[中文开发文档](docs/DEVELOPMENT_CN.md)。

## 当前状态

动态实体与 HUD 资源加载链路已经验证。每玩家状态、输入捕获和点击回调，仍需等待 SwiftlyS2
为新 CS2 架构和消息提供绑定后再实现。
