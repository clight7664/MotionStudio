# DESIGN_SYSTEM — MotionStudio 工作台设计规范

- **文档版本**：`0.2.0`
- **维护角色**：UX/UI Designer
- **设计定位**：高密度、低视觉疲劳的专业桌面级 2D 动效编辑工具（参考 Jitter 简洁工作台与 Lottielab 专业时间轴，100% 原创实现）。

## 1. 核心 Design Tokens

| CSS 变量名 | 初始值 | 用途与约束 |
|---|---|---|
| `--color-bg` | `#17191D` | 应用全局深石墨底色 |
| `--color-panel` | `#202329` | 顶栏、图层树、Inspector、Timeline 面板背景 |
| `--color-panel-elevated` | `#262A32` | 输入框、悬停行、展开轨道二级背景 |
| `--color-canvas` | `#2B2E34` | 画布视口外围工作区背景 |
| `--color-border` | `#343840` | 1px 精密面板分割线与控件描边 |
| `--color-text` | `#E7E9ED` | 主文本与活动图标颜色（高对比度） |
| `--color-muted` | `#A5AAB5` | 属性标签、帧刻度、次要说明文字 |
| `--color-accent` | `#6B9CFF` | 选中图层、活动工具、关键帧菱形高亮色 |
| `--color-playhead` | `#FF5C6C` | 播放头竖线与帧游标强调色 |
| `--color-warning` | `#F5A623` | 未保存标记、混合值提醒与受限视口提示 |
| `--color-danger` | `#FF6B6B` | 删除操作与内联错误恢复提示 |
| `--radius-control` | `4px` | 按钮、输入框统一紧凑圆角（严禁大圆角卡片） |
| `--topbar-height` | `48px` | 顶部全局操作栏固定高度 |
| `--left-panel-width` | `240px` | 左侧图层树初始宽度（支持 `180px..420px` 拖拽调整） |
| `--right-panel-width` | `280px` | 右侧属性检查器初始宽度（支持 `220px..460px` 拖拽调整） |
| `--timeline-height` | `240px` | 底部时间轴初始高度（支持 `160px..480px` 拖拽调整） |

## 2. 五区工作台布局规范

```text
+-----------------------------------------------------------------------------------+
| Topbar (48px): Project Title* | New Open Save Export | +Rect +Ellipse +Text | Play |
+----------------+-----------------------------------------------+------------------+
| Layers (240px) | Central Canvas Viewport (Elastic)             | Inspector (280px)|
| - Layer Tree   |   +---------------------------------------+   | - Transform      |
| - Eye / Lock   |   | Composition Stage (1920x1080 scaled)  |   |   X, Y, Rot, Scl |
| - Up / Down    |   |   [Selected Shape + Transform Box]    |   | - Geometry W, H  |
| - Group        |   +---------------------------------------+   | - Opacity & Fill |
|                |   [Zoom: 100% | Grid | Checkerboard]          | - Easing & Key ◆ |
+----------------+-----------------------------------------------+------------------+
| Timeline (240px): [|<] [Play/Pause] [Stop] [Loop] | Frame 15 / 90 (0.50s) | +Key◆ |
| Track Header   | 0f      15f (Playhead ▼)     30f             60f             90f |
| ▼ Shape 1      | ◆----------------------------◆---------------------------------- |
|   transform.x  | ◆ (0)                        ◆ (300)                             |
+-----------------------------------------------------------------------------------+
```

## 3. 视口适配与状态反馈红线

- **标准设计视口**：`1440×900`，向下兼容 `1366×768`，向上扩展支持 `1920×1080`。当窗口小于 `1024×640` 时显示非阻塞式受限视口横幅。
- **完备状态设计**：
  - **Empty State**：无图层时图层树与画布给出“点击顶部 +Rect / +Ellipse / +Text 创建第一个图层”引导；未选中图层时 Inspector 显示工程画布概览而非空白或陈旧数据。
  - **Dirty State**：工程发生未保存变更时，标题栏显示 `* Unsaved` 徽标。
  - **Error / Recovery State**：顶部内联通知条显示结构化错误代码、可读原因与一键恢复/关闭按钮，严禁大面积红屏或暴露堆栈。
