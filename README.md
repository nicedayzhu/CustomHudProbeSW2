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
