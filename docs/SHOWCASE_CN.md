# 游戏内效果展示

[English showcase](SHOWCASE.md) · [返回中文说明](../README_CN.md)

这里集中展示尺寸较大的游戏内截图，避免主 README 过于拥挤。以下三个效果均由 Panorama 渲染在 Counter-Strike 2 的世界空间 HUD 面板上。

| 模式 | 聊天命令 | 交互效果 |
| --- | --- | --- |
| 赛博卡片 | `!chud_spawn card` | Hover 驱动的 3D 倾斜与光照 |
| Hover 3D 画廊 | `!chud_spawn gallery` | 单张卡片独立响应 Hover 与景深变化 |
| 双面翻转卡片 | `!chud_spawn flip` | Hover 时切换卡片正反面 |

## 赛博卡片

赛博卡片在单个面板中组合了多层边框、辉光、高光、粒子以及随指针变化的 3D 动效。

![Counter-Strike 2 游戏内的赛博卡片效果](../assets/showcase-cyber-card.png)

## Hover 3D 画廊

画廊在同一个世界空间 HUD 中排列三张可独立交互的卡片，并在指针切换时保持稳定的间距与景深关系。

![Counter-Strike 2 游戏内的 Hover 3D 画廊效果](../assets/showcase-hover3d-gallery.png)

## 双面翻转卡片

双面卡片在 Hover 时平滑切换正反面；翻转过程中保持圆角与渐变稳定，不再出现突兀的矩形透明框或离开 Hover 后的明暗跳变。

![Counter-Strike 2 游戏内的双面翻转卡片效果](../assets/showcase-flip-card.png)
