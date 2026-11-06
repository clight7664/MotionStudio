# ADR-0002 — 客户端 Canvas 2D + TypeScript/Konva 场景图与时间轴内核

- **Status**: `Accepted`
- **Date**: `2026-11-06`
- **Related Requirements**: `FR-002`, `FR-003`, `FR-004`, `FR-009`, `FR-010`, `NFR-002`, `NFR-005`
- **Related Risks**: `R-002`, `R-006`

## 1. Context

Blazor Interactive Server 适合管理工程状态、属性表单、图层树与命令历史，但若在鼠标每次 `pointermove` 或播放期间每帧 `requestAnimationFrame` 都通过 SignalR 往返服务端，会产生不可接受的延迟与网络开销。此外，构建环境需要在离线无外网条件下执行 `npm run typecheck`、`npm run lint`、`npm run test` 与 `npm run build`。

## 2. Decision

1. **职责边界**：
   - `MotionStudio.Renderer` (TypeScript) 独占负责 `requestAnimationFrame` 播放帧循环、Canvas 2D 场景图绘制、视口缩放/平移、命中测试 (`hitTest`) 以及指针拖拽期间的实时本地预览 (`previewTransform`)。
   - 拖拽期间向 Blazor 发送 **0** 次请求；仅在 `pointerup` 时向 C# 提交 **1** 条版本化语义命令 (`MoveLayerTransform`)。
2. **Clean-Room Konva 场景图模块 (`src/MotionStudio.Renderer/src/vendor/konva.ts`)**：
   - 提供类型严格 (`strict: true`) 的 `Konva` (`Stage`, `Layer`, `Group`, `Rect`, `Ellipse`, `Text`, `Line`, `Transformer`) Canvas 2D 场景图实现，通过 `tsconfig.json` `paths` 与 Vite `resolve.alias` 映射为标准 `'konva'` 模块导入，确保离线环境下零外部网络依赖即可完成构建与测试。
3. **跨语言插值一致性**：
   - C# `TimelineEvaluator` 与 TypeScript `evaluateTrackAtFrame` 采用完全一致的整数帧索引与缓动数学公式，通过双端 Golden Vector 单元测试强制对齐。

## 3. Consequences

- 拖拽与 30/60fps 播放完全在客户端 Canvas 2D 内闭环完成，达到亚毫秒级帧采样响应；
- 权威状态始终唯一保存在 .NET `ProjectDocument` 中，彻底避免双端历史状态分裂。
