# ENGINEERING_STANDARDS — 研发与代码规范

- 维护角色：Tech Lead

## 1. 语言、工程与编译门禁

- **.NET 基线**：`.NET 10 LTS` (`net10.0`)，由根目录 `global.json` (`10.0.100`, `rollForward: latestFeature`) 与 `Directory.Build.props` 统一约束。
- **C# 静态分析**：全局开启 `Nullable=enable`、`ImplicitUsings=enable`、`TreatWarningsAsErrors=true`、`EnableNETAnalyzers=true`、`AnalysisLevel=latest`、`EnforceCodeStyleInBuild=true`。禁止全局关闭分析器；局部合理豁免须有明确理由。
- **TypeScript 基线**：`strict: true`、`noImplicitOverride: true`、`noUncheckedIndexedAccess: true`、`noUnusedLocals: true`、`noUnusedParameters: true`。
- **代码格式**：根目录 `.editorconfig` 为唯一代码格式标准，通过 `dotnet format MotionStudio.slnx --verify-no-changes` 验证。

## 2. 模块分层与单向依赖

```text
MotionStudio.Web ──► MotionStudio.Application ──► MotionStudio.Domain
      │                        ▲
      ▼                        │
MotionStudio.Infrastructure ───┘
```

- `MotionStudio.Domain`：纯领域模型、值对象、业务不变量校验与确定性关键帧插值算法 (`TimelineEvaluator`)。严禁引用 Blazor、ASP.NET Core、Electron 或文件系统 IO。
- `MotionStudio.Application`：用例服务 (`EditorWorkspaceService`)、版本化命令与事件契约 (`EditorCommandEnvelope`)、`DocumentRevision` 并发校验、Undo/Redo 历史栈及端口接口 (`IProjectRepository`, `IDesktopHostBridge`, `ISvgSanitizer`)。
- `MotionStudio.Infrastructure`：UTF-8 `.motion.json` Schema v1 序列化、同目录临时文件原子写入、备份恢复、SVG 安全净化器、静态导出器与桌面宿主桥接实现。
- `MotionStudio.Web`：ASP.NET Core `.NET 10` Blazor Interactive Server 工作台组件、JS Interop 适配层与状态呈现。严禁在 Razor 组件中逐帧执行动画插值或直接读写磁盘文件。
- `MotionStudio.Renderer`：TypeScript + Canvas 2D / Konva 场景图渲染、指针拖拽本地预览 (`pointermove` 零 SignalR 往返)、`pointerup` 单次语义命令提交与 `requestAnimationFrame` 本地播放循环。
- `MotionStudio.Desktop`：Windows 桌面宿主封装、Loopback 端口分配、进程生命周期管理与安全边界控制。

## 3. 异常处理、并发与资源释放

- 所有异步 IO 方法以 `Async` 结尾并接受 `CancellationToken`。
- 领域与输入参数严格校验：`NaN`、`Infinity`、非正宽高、非法 FPS、越界 `frameIndex`、非法颜色值均返回结构化 `DomainValidationException` 或 `CommandResult.Failure`。
- Blazor 组件与 JS Interop 适配器实现 `IAsyncDisposable`，在组件卸载时释放 `IJSObjectReference`、定时器与事件订阅。

## 4. 提交与分支规范

- 遵循 Conventional Commits：`feat|fix|perf|refactor|test|docs|build|ci|chore|revert(scope): summary`。
- 提交正文包含关联的需求编号（如 `Refs: FR-008, FR-009`）。
- 根目录 `.githooks/commit-msg` 自动校验提交消息首行格式。
