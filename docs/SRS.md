# SRS — MotionStudio 软件需求规格说明

- **文档 ID**：`MS-SRS-001`
- **版本**：`0.2.0`
- **状态**：`Approved for Implementation (AI self-review)`
- **维护角色**：Product Owner / QA Engineer

## 1. 功能需求清单 (Functional Requirements)

| ID | 阶段 | 优先级 | 系统应提供的能力 | Given-When-Then 验收条件与异常防护 |
|---|---|---|---|---|
| **FR-001** | P0 | P0 | 新建工程，可配置画布宽高 (`1..16384`)、FPS (`24/30/60`)、总帧数 (`1..216000`) | **Given** 打开新建工程对话框 **When** 输入 `1920×1080, 30fps, 90 frames` **Then** 创建空工程并重置修订号为 1；输入 `<=0`、`NaN` 或非法 FPS 时拒绝创建并内联提示错误 |
| **FR-002** | P0 | P0 | 画布缩放 (`10%..800%`)、平移、重置视口 (`100%`/`Fit`) 及棋盘格/暗色网格背景切换 | **Given** 工程已打开 **When** 操作缩放按钮或滚轮 **Then** 画布缩放比例实时更新且受 `[0.1, 8.0]` 边界约束，指针坐标逆变换准确命中对象 |
| **FR-003** | P0/P1 | P0 | 创建矩形 (`rectangle`)、椭圆 (`ellipse`) 与文字 (`text`) 图层 | **Given** 处于就绪工作台 **When** 点击工具栏矩形/椭圆/文字按钮 **Then** 生成唯一 ID 图层，默认尺寸与颜色合法，立即同步显示在图层树、Canvas 与 Inspector |
| **FR-004** | P0/P1 | P0 | 选择、多选 (`Ctrl/Shift`)、框选与画布拖拽变换（位移、缩放、旋转） | **Given** 选中未锁定图层 **When** 在画布拖拽后释放指针 **Then** 拖动期间本地 60fps 预览，`pointerup` 提交单条命令并更新 Inspector；锁定或隐藏图层不可在画布拖拽 |
| **FR-005** | P0/P1 | P0 | 图层树显隐 (`visible`)、锁定 (`locked`)、上下重排 (`zIndex`) 与分组 (`group`) | **Given** 存在多个图层 **When** 切换显隐/锁定、调整层级顺序或点击编组 **Then** Canvas 绘制顺序与可交互状态立即同步，且操作支持 Undo/Redo |
| **FR-006** | P0 | P0 | 属性面板 (Inspector) 与画布双向同步，支持多选混合值 (`<Mixed>`) 显示 | **Given** 选中单个或多个图层 **When** 修改 X/Y/Width/Height/Rotation/Scale/Opacity/Fill **Then** Canvas 与 Timeline 立即同步；无选中时显示空状态引导，多选不同值时显示 `<Mixed>` |
| **FR-007** | P0 | P0 | 时间轴帧标尺、轨道展开、播放头拖拽/点击定位与帧/秒时间显示 | **Given** 90 帧 30fps 工程 **When** 将播放头定位到第 15 帧 **Then** 时间显示为 `Frame 15 / 00:00.50`，画布立即按第 15 帧采样所有轨道值；越界帧号自动钳制在 `[0, durationFrames - 1]` |
| **FR-008** | P0/P1 | P0 | 关键帧创建、更新（同帧唯一替换）、移动帧位置与删除 | **Given** 选中图层属性 **When** 在帧 0 添加 `X=0`、在帧 30 添加 `X=300` **Then** 轨道出现两个菱形关键帧；同图层同属性同帧重复添加时覆盖原值而非产生重复关键帧 |
| **FR-009** | P0/P1 | P0 | 计算 `transform.x/y/rotation/scaleX/scaleY`、`opacity`、`fill` 关键帧插值 | **Given** 帧 0 `X=0`、帧 30 `X=300` (`Linear`) **When** 采样帧 15 **Then** `X=150`；支持 `Linear`、`EaseIn`、`EaseOut`、`EaseInOut`、`CubicBezier` 及 Hex 颜色插值 |
| **FR-010** | P0 | P0 | 本地 `requestAnimationFrame` 播放、暂停、停止与循环控制 | **Given** 含关键帧的工程 **When** 点击 Play **Then** 客户端 `requestAnimationFrame` 按 FPS 推进播放头并重绘画布，播放期间不产生逐帧 SignalR 调用；Pause 停在当前帧，Stop 回到第 0 帧 |
| **FR-011** | P1 | P1 | 语义命令级 Undo / Redo 历史栈 | **Given** 连续执行创建图层、移动位置、添加关键帧等 10 步修改 **When** 依次执行 Undo 与 Redo **Then** 文档状态与修订号精确回退与重演；单纯移动播放头不进入 Undo 栈 |
| **FR-012** | P1 | P1 | 图层与轨道复制 (`Copy`) / 粘贴 (`Paste`) | **Given** 选中图层 **When** 执行复制并粘贴 **Then** 生成全新图层 ID 與轨道 ID，位置偏移 `+20px` 避免完全重叠，完整保留原属性与关键帧 |
| **FR-013** | P0/P1 | P0 | Save / Open / Save As UTF-8 `.motion.json` 工程文件（含 Schema v1 校验与原子写入） | **Given** 已编辑的动画工程 **When** 执行 Save 并重新 Open **Then** 所有图层、变换、轨道、关键帧与元数据 100% 语义等价恢复；加载损坏 JSON 时拒绝覆盖当前工程并提示恢复操作 |
| **FR-014** | P1 | P1 | 全局快捷键支持与输入框防误触保护 | **Given** 焦点不在文本输入框 **When** 按下 `Space` (播放/暂停)、`Ctrl+Z`/`Ctrl+Y` (撤销/重做)、`Ctrl+S` (保存)、`Delete` (删除) **Then** 触发对应命令；焦点在 `<input>` 时不劫持按键 |
| **FR-015** | P0 | P0 | 空状态、未保存脏标记 (`*`)、加载态、错误横幅与断连恢复提示 | **Given** 空图层/无选中/保存失败 **When** 状态触发 **Then** 显示行内可关闭恢复提示与结构化错误码，严禁渲染原始 C# 异常堆栈 |
| **FR-016** | P0/P1 | P0 | Windows 桌面客户端封装（Electron 33 + 本地 `.NET 10` Loopback Host） | **Given** 无外网 Windows 环境 **When** 启动桌面程序 **Then** 自动拉起 `127.0.0.1` 随机端口 Blazor 服务并加载工作台，关闭窗口时干净终止 `.NET` 进程 |
| **FR-017** | P2 | P2 | 安全的本地 SVG 素材导入与危险节点净化 | **Given** 用户导入 SVG 内容 **When** 包含 `<script>`、`onload`、`foreignObject` 或外链 **Then** 安全净化器拦截并报错；合法 SVG 几何则转为可动画图层 |
| **FR-018** | P1/P2 | P1 | 导出当前帧静态 SVG 与 PNG 快照 | **Given** 画布存在可见图层 **When** 点击导出 SVG 或 PNG **Then** 按当前播放头采样状态生成符合分辨率且无外链的 SVG/PNG 文件 |
| **FR-019** | P2 | P2 | 应用可配置的动效预设 (`FadeIn`, `SlideInRight`, `PopScale`, `Pulse`) | **Given** 选中图层 **When** 选择动效预设 **Then** 自动生成真实可编辑的轨道与关键帧，支持继续调整、保存与撤销 |
| **FR-020** | P1/P2 | P1 | 关键帧缓动类型与自定义三次贝塞尔 (`CubicBezier`) 控制点编辑 | **Given** 选中关键帧或轨道 **When** 切换 Easing 或调整 `(x1, y1, x2, y2)` **Then** 插值结果与序列化 JSON 完全一致，非法控制点 (`x ∉ [0,1]`) 被拒绝 |

## 2. 非功能需求清单 (Non-Functional Requirements)

| ID | 阶段 | 要求描述 | 验证方法 |
|---|---|---|---|
| **NFR-001** | P1/P3 | 桌面客户端支持完全离线启动、编辑、保存与重新打开，不依赖云端服务 | 本地沙箱无网络环境下运行桌面宿主与 `--self-test` |
| **NFR-002** | P1/P2 | 客户端本地高频渲染；基准测试（200 图层 × 2 轨道）测量帧采样耗时 P50/P95 | 单元测试与性能基准测试记录实测毫秒数 |
| **NFR-003** | P0/P1 | 工程保存采用同目录 `.tmp` 写入 + 原子替换 + `.bak` 备份；损坏文件不破坏已打开工程 | `MotionStudio.Integration.Tests` 故障注入测试 |
| **NFR-004** | G0+ | 全工程启用 `Nullable=enable`、`TreatWarningsAsErrors=true`、TypeScript `strict: true` | `dotnet build -c Release` 与 `npm run typecheck` |
| **NFR-005** | P0/P1 | 拖拽期间零逐像素 SignalR 调用，每次手势仅在 `pointerup` 提交 1 条语义命令 | `TC-006` 交互控制器单元测试与计数断言 |
| **NFR-006** | G0+ | Electron `contextIsolation: true`、`nodeIntegration: false`、Loopback 绑定与 SVG/路径安全校验 | 安全配置审查与 `SvgSanitizerTests` / `PathSecurityTests` |
| **NFR-007** | G0+ | 自动化质量门禁覆盖代码格式、编译、单元/集成/E2E 测试 | CI 配置与 `docs/VERIFICATION.md` 实测命令 |
| **NFR-008** | P1/P3 | 未保存修改 (`IsDirty`) 精确追踪，异常中断可从 `.bak` 自动备份恢复 | 集成测试验证备份恢复流程 |
| **NFR-009** | G0+ | 模块单向依赖（`Web -> Application -> Domain`）与完整双向追溯矩阵 | 架构依赖断言测试与 `docs/REQUIREMENTS_TRACEABILITY.md` |
