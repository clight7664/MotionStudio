# PRD — MotionStudio 产品需求定义

- **文档 ID**：`MS-PRD-001`
- **版本**：`0.2.0`
- **状态**：`Approved for Implementation (AI self-review)` / `Pending user decision`
- **日期**：`2026-11-06`
- **维护角色**：Product Owner (PO)

## 1. 产品定位与 Clean-Room 参考边界

`MotionStudio` 是一款同时支持浏览器访问（ASP.NET Core Blazor Interactive Server）与 Windows 桌面离线运行（Electron + 本地 Loopback Host）的原创专业 2D 矢量与图形动效制作工作台。

### Clean-Room 原型参考与原创边界表

| 参考维度 | 公开交互理念参考来源 | MotionStudio 原创实现决策 | 明确禁止事项 |
|---|---|---|---|
| **工作台布局与流转** | Jitter 公开工作台的信息密度与五区布局（顶栏、左图层树、中画布、右属性面板、底时间轴） | 采用原创石墨暗色令牌 (`#17191D`/`#202329`/`#6B9CFF`)、48px 顶栏 + 240px 图层栏 + 弹性画布 + 280px 检查器 + 240px 时间轴，支持拖拽分割线动态调节尺寸 | 禁止复制第三方 Logo、图标、品牌名、专属预设名称或营销文案 |
| **时间轴与关键帧** | Lottielab 公开的属性轨道展开、整数帧标尺、菱形关键帧 (`◆`) 与缓动曲线控制 | 采用规范化整数 `frameIndex` (`[0, durationFrames - 1]`) 存储关键帧，支持 `transform.x/y/rotation/scaleX/scaleY`、`opacity`、`fill` 七类可动画属性及 `Linear`/`EaseIn`/`EaseOut`/`EaseInOut`/`CubicBezier` 插值 | 禁止复制第三方私有工程格式或未公开算法源码 |
| **未来状态机演进** | Rive 状态机概念（仅作为 P2+ 架构预留参考） | 在 `.motion.json` 与领域模型保持清晰图层/轨道解耦，P0/P1 不伪造未实现的 State Machine 面板 | 禁止用静态假按钮冒充状态机功能 |

## 2. 目标用户与核心场景

1. **UI/UX 动效设计师**：在 1920×1080 / 30fps 画布中快速创建矩形、椭圆、文字图层，编排位移、缩放、旋转、不透明度与色彩过渡。
2. **前端与桌面工程师**：制作确定性关键帧动画，保存为结构化 `.motion.json` 工程文件，或导出干净的静态 SVG / PNG 资产。
3. **技术演示作者**：在无外网环境下启动桌面客户端，加载演示工程，实时预览 30fps/60fps 动效并随时撤销/重做修改。

## 3. 分阶段范围矩阵 (Feature Matrix)

| 能力域 | P0 (最小闭环) | P1 (核心编辑) | P2 (工程化增强) | P3 (系统交付) |
|---|---|---|---|---|
| **工作台与布局** | 顶栏、图层树、画布、Inspector、Timeline 五区联动 | 可拖拽分割条、快捷键、多选 `<Mixed>` 状态 | 安全 SVG 导入面板、动效预设菜单 | 离线桌面窗口与分辨率自适应检验 |
| **图形与图层** | 新建矩形/椭圆、单选、拖拽移动与尺寸编辑 | 新建文字、多选、旋转/缩放、显隐/锁定/排序/分组、复制粘贴 | SVG 图层安全解析与栅格/矢量导出 | 200 图层基准测试与异常恢复 |
| **时间轴与动画** | 帧标尺、播放头、0s/1s 关键帧、Linear 插值、`requestAnimationFrame` 播放 | 7 种可动画属性、`EaseIn/Out/InOut/CubicBezier`、关键帧移动与删除 | 一键动效预设 (`FadeIn`, `SlideInRight`, `PopScale`, `Pulse`) | 跨语言 C#/TS Golden Vector 对齐验证 |
| **工程持久化** | `.motion.json` v1 保存与打开 | Save As、同目录临时文件原子写入、`.bak` 自动备份、Schema 迁移 | 静态 SVG / PNG 导出 (`FR-018`) | 损坏文件拒绝与断电恢复验证 |
| **桌面宿主** | Electron + .NET 10 Loopback Spike | Windows 离线启动、Loopback 令牌校验、退出进程清理 | 沙箱路径限制与外部链接拦截 | `--screenshot` 与 `--self-test` 自动化验收 |
